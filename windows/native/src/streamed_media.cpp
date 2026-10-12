// SPDX-License-Identifier: AGPL-3.0-only
#include "media_clip.h"
#include "media_decoders.h"
#include <cstring>
#include <juce_audio_formats/juce_audio_formats.h>
#include <signalsmith-stretch/signalsmith-stretch.h>
#include <algorithm>
#include <chrono>
#include <cmath>
#include <fstream>
#include <thread>

namespace rocker::media {
namespace {
constexpr uint64_t pageFrames=4096, pageCount=64, maximumCacheBytes=8ull*1024*1024*1024;
class CachedSource final:public PcmSource {
    struct Page {std::atomic<uint32_t> state{0};uint64_t key=UINT64_MAX;std::array<float,pageFrames*2> data{};};
public:
    CachedSource(std::filesystem::path path,uint64_t frames,uint32_t channels,std::vector<float> peaks)
        :path_(std::move(path)),frames_(frames),channels_(channels),peaks_(std::move(peaks)),file_(path_,std::ios::binary) {
        if(!file_)throw Failure(Error::decodeFailed,"Cannot reopen the decoded PCM cache");
        for(uint64_t p=0;p<16 && p*pageFrames<frames_;++p)fill(p);
        if(failed_)throw Failure(Error::decodeFailed,"Cannot prime the decoded PCM cache");
        worker_=std::jthread([this](std::stop_token stop){
            while(!stop.stop_requested()){
                const auto wanted=request_.load(std::memory_order_acquire)/pageFrames;
                for(uint64_t p=0;p<16 && (wanted+p)*pageFrames<frames_ && !stop.stop_requested();++p)fill(wanted+p);
                const auto hint=loopHint_.load();if(hint!=UINT64_MAX)for(uint64_t p=0;p<4 && (hint/pageFrames+p)*pageFrames<frames_;++p){
                    const auto key=hint/pageFrames+p;
                    // A loop-start prefetch must never evict the current read-ahead range.
                    const auto slot=key%pageCount;bool collision=false;
                    for(uint64_t i=0;i<16;++i)if((wanted+i)%pageCount==slot && wanted+i!=key)collision=true;
                    if(!collision)fill(key);
                }
                std::this_thread::sleep_for(std::chrono::milliseconds(2));
            }
        });
    }
    ~CachedSource() override {
        worker_.request_stop();if(worker_.joinable())worker_.join();file_.close();
        std::error_code ignored;std::filesystem::remove(path_,ignored);
    }
    bool read(uint64_t frame,std::array<float,2>& values) noexcept override {
        if(frame>=frames_)return false;
        const auto key=frame/pageFrames;auto& page=pages_[key%pageCount];uint32_t expected=2;
        if(!page.state.compare_exchange_strong(expected,3,std::memory_order_acquire))return false;
        const bool valid=page.key==key;
        if(valid){const auto at=(frame%pageFrames)*channels_;values[0]=page.data[at];values[1]=channels_==1?values[0]:page.data[at+1];}
        page.state.store(2,std::memory_order_release);return valid;
    }
    void request(uint64_t frame) noexcept override {request_.store(std::min(frame,frames_-1),std::memory_order_release);}
    void loopHint(uint64_t frame) noexcept override {loopHint_.store(frame);}
    uint64_t frames() const noexcept override{return frames_;}
    uint32_t channels() const noexcept override{return channels_;}
    bool failed() const noexcept override{return failed_.load();}
    const std::vector<float>& waveform() const noexcept override{return peaks_;}
private:
    void fill(uint64_t key) noexcept {
        auto& page=pages_[key%pageCount];uint32_t state=page.state.load(std::memory_order_acquire);
        if(state==1 || state==3 || (state==2 && page.key==key))return;
        if(!page.state.compare_exchange_strong(state,1,std::memory_order_acquire))return;
        const auto count=std::min(pageFrames,frames_-key*pageFrames)*channels_;
        try{
            file_.clear();file_.seekg(static_cast<std::streamoff>(key*pageFrames*channels_*sizeof(float)));
            file_.read(reinterpret_cast<char*>(page.data.data()),static_cast<std::streamsize>(count*sizeof(float)));
            if(file_.gcount()!=static_cast<std::streamsize>(count*sizeof(float)))throw std::runtime_error("PCM cache read failed");
            page.key=key;page.state.store(2,std::memory_order_release);
        }catch(...){failed_.store(true);page.state.store(0,std::memory_order_release);}
    }
    std::filesystem::path path_;uint64_t frames_;uint32_t channels_;std::vector<float> peaks_;
    std::array<Page,pageCount> pages_;
    std::atomic<uint64_t> request_{0},loopHint_{UINT64_MAX};std::atomic<bool> failed_{false};
    std::ifstream file_;std::jthread worker_;
};
struct CacheFile {
    std::filesystem::path path=std::filesystem::temp_directory_path()/("rocker-pcm-"+juce::Uuid().toString().toStdString()+".tmp");
    bool transferred=false;
    ~CacheFile(){if(!transferred){std::error_code ignored;std::filesystem::remove(path,ignored);}}
};
class DiskPcmReader final:public juce::AudioFormatReader {
public:
    DiskPcmReader(juce::InputStream* input,const detail::PcmShape& shape):juce::AudioFormatReader(input,"Rocker decoded PCM"){sampleRate=shape.sampleRate;numChannels=shape.channels;lengthInSamples=static_cast<juce::int64>(shape.frames);bitsPerSample=32;usesFloatingPointData=true;}
    bool readSamples(int* const* destination,int destinationChannels,int offset,juce::int64 start,int count) override {
        if(start<0 || count<0 || start>lengthInSamples || count>lengthInSamples-start || !input->setPosition(start*numChannels*sizeof(float)))return false;
        std::array<float,4096*2> scratch{};int done=0;while(done<count){int n=std::min(4096,count-done);int bytes=static_cast<int>(n*numChannels*sizeof(float));if(input->read(scratch.data(),bytes)!=bytes)return false;for(int c=0;c<destinationChannels;++c)if(destination[c]){auto* target=reinterpret_cast<float*>(destination[c])+offset+done;for(int i=0;i<n;++i)target[i]=c<static_cast<int>(numChannels)?scratch[static_cast<size_t>(i)*numChannels+c]:0;}done+=n;}return true;
    }
};
void cancelled(const std::atomic<uint64_t>& generation,uint64_t expected){if(generation.load()!=expected)throw Failure(Error::decodeFailed,"Media preparation was superseded or cancelled");}
}
std::vector<float> waveform(const Clip& clip,size_t bins){
    if(clip.stream)return clip.stream->waveform();
    std::vector<float> peaks(bins,0);
    if(!clip.frames() || !bins)return peaks;
    for(size_t i=0;i<clip.frames();++i)for(const auto& channel:clip.channels)peaks[std::min(bins-1,i*bins/clip.frames())]=std::max(peaks[std::min(bins-1,i*bins/clip.frames())],std::abs(channel[i]));
    return peaks;
}
Clip prepareFile(const std::filesystem::path& path,double speed,const std::atomic<uint64_t>& generation,uint64_t expected){
    if(!std::isfinite(speed) || speed<.25 || speed>2)throw Failure(Error::invalidRate,"Playback speed must be within 0.25x-2x");
    cancelled(generation,expected);
#ifdef _WIN32
    const juce::File file(juce::String(std::filesystem::absolute(path).c_str()));
#else
    const juce::File file(juce::String::fromUTF8(std::filesystem::absolute(path).c_str()));
#endif
    if(!file.existsAsFile() || file.getSize()<=0)throw Failure(Error::invalidInput,"Audio file is missing or empty");
    if(static_cast<uint64_t>(file.getSize())>1024ull*1024*1024)throw Failure(Error::limitExceeded,"Encoded audio exceeds 1 GiB limit");
    juce::AudioFormatManager formats;formats.registerBasicFormats();
    auto encoded=file.createInputStream();if(!encoded)throw Failure(Error::decodeFailed,"Cannot open the encoded audio stream");
    if(encoded->getTotalLength()>1024ll*1024*1024)throw Failure(Error::limitExceeded,"Opened audio exceeds 1 GiB limit");
    CacheFile decodedCache;
    std::unique_ptr<juce::AudioFormatReader> reader(formats.createReaderFor(std::move(encoded)));
    if(!reader){
        unsigned char signature[12]{};auto probe=file.createInputStream();if(!probe)throw Failure(Error::decodeFailed,"Cannot reopen encoded audio");int bytes=probe->read(signature,sizeof(signature));bool webm=bytes>=4 && signature[0]==0x1a && signature[1]==0x45 && signature[2]==0xdf && signature[3]==0xa3;bool mp4=bytes>=8 && std::memcmp(signature+4,"ftyp",4)==0;if(!webm && !mp4)throw Failure(Error::unsupportedFormat,"No decoder recognized audio content");
        std::ofstream decoded(decodedCache.path,std::ios::binary|std::ios::trunc);if(!decoded)throw Failure(Error::decodeFailed,"Cannot create decoded container cache");uint64_t frames=0;uint32_t channels=0;double rate=0;
        auto check=[&]{cancelled(generation,expected);};detail::ChunkSink sink=[&](const float* data,size_t count,uint32_t ch,double hz){check();if(ch<1 || ch>2 || count>maximumCacheBytes/(ch*sizeof(float))-frames || (channels && (channels!=ch || rate!=hz)))throw Failure(Error::limitExceeded,"Container PCM exceeds cache budget or changed shape");channels=ch;rate=hz;decoded.write(reinterpret_cast<const char*>(data),static_cast<std::streamsize>(count*ch*sizeof(float)));if(!decoded)throw Failure(Error::decodeFailed,"Container cache write failed; check disk space");frames+=count;};
        Limits limits{1024ull*1024*1024,maximumCacheBytes,maximumCacheBytes};auto shape=webm?detail::decodeWebmChunks(path,limits,sink,check):detail::decodeMp4Chunks(path,limits,sink,check);decoded.flush();if(!decoded || shape.frames!=frames || shape.channels!=channels || shape.sampleRate!=rate)throw Failure(Error::decodeFailed,"Incomplete decoded container cache");decoded.close();check();
#ifdef _WIN32
        juce::File cacheFile(juce::String(decodedCache.path.c_str()));
#else
        juce::File cacheFile(juce::String::fromUTF8(decodedCache.path.c_str()));
#endif
        auto input=cacheFile.createInputStream();if(!input)throw Failure(Error::decodeFailed,"Cannot reopen container PCM cache");reader=std::make_unique<DiskPcmReader>(input.release(),shape);
    }
    if(reader->lengthInSamples<=0 || reader->numChannels<1 || reader->numChannels>2 || !std::isfinite(reader->sampleRate) || reader->sampleRate<8000 || reader->sampleRate>192000)throw Failure(Error::invalidInput,"Unsupported PCM shape");
    const auto sourceFrames=static_cast<uint64_t>(reader->lengthInSamples);
    const auto total=static_cast<uint64_t>(std::llround(sourceFrames/speed));const auto channels=reader->numChannels;
    if(!total || total>UINT32_MAX || total>maximumCacheBytes/(channels*sizeof(float)))throw Failure(Error::limitExceeded,"Decoded PCM exceeds the 8 GiB cache limit");
    CacheFile cache;std::ofstream output(cache.path,std::ios::binary|std::ios::trunc);
    if(!output)throw Failure(Error::decodeFailed,"Cannot create the decoded PCM cache");
    constexpr int block=4096;std::vector<std::vector<float>> input(channels,std::vector<float>(65536)),processed(channels,std::vector<float>(65536));
    std::array<float*,2> inputs{},outputs{};for(unsigned c=0;c<channels;++c){inputs[c]=input[c].data();outputs[c]=processed[c].data();}
    std::vector<float> interleaved(65536*channels),peaks(1024,0);uint64_t written=0,sourceAt=0,processedInput=0,processedOutput=0;
    auto read=[&](int n){cancelled(generation,expected);if(!reader->read(inputs.data(),static_cast<int>(channels),static_cast<juce::int64>(sourceAt),n))throw Failure(Error::decodeFailed,"Audio decoder failed to read a chunk");sourceAt+=n;};
    auto write=[&](float* const* pcm,int n){
        cancelled(generation,expected);if(n<0 || written+static_cast<uint64_t>(n)>total)throw Failure(Error::decodeFailed,"Invalid streamed stretch frame count");
        for(int i=0;i<n;++i)for(unsigned c=0;c<channels;++c){const auto sample=pcm[c][i];if(!std::isfinite(sample))throw Failure(Error::decodeFailed,"Non-finite decoded audio");interleaved[static_cast<size_t>(i)*channels+c]=sample;const auto bin=std::min(uint64_t{1023},(written+i)*1024/total);peaks[bin]=std::max(peaks[bin],std::abs(sample));}
        output.write(reinterpret_cast<const char*>(interleaved.data()),static_cast<std::streamsize>(n*channels*sizeof(float)));if(!output)throw Failure(Error::decodeFailed,"Decoded PCM cache write failed; check disk space");written+=n;
    };
    if(speed==1){while(sourceAt<sourceFrames){const auto n=static_cast<int>(std::min(uint64_t{block},sourceFrames-sourceAt));read(n);write(inputs.data(),n);}}
    else{
        signalsmith::stretch::SignalsmithStretch<float,std::mt19937> processor(0);processor.presetDefault(static_cast<int>(channels),static_cast<float>(reader->sampleRate));processor.setTransposeFactor(1);
        const int seek=processor.outputSeekLength(static_cast<float>(speed));
        if(seek>65536 || sourceFrames<static_cast<uint64_t>(seek))throw Failure(Error::tooShort,"Clip is too short for pitch-preserving stretching");
        read(seek);processor.outputSeek(inputs.data(),seek);
        const auto bodyOutput=total-static_cast<uint64_t>(seek/speed);
        const auto bodyInput=sourceFrames-seek;
        while(sourceAt<sourceFrames){const auto n=static_cast<int>(std::min(uint64_t{block},sourceFrames-sourceAt));read(n);processedInput+=n;
            const auto cumulative=processedInput==bodyInput?bodyOutput:static_cast<uint64_t>(std::llround(processedInput*static_cast<double>(bodyOutput)/bodyInput));
            const auto count=static_cast<int>(cumulative-processedOutput);processor.process(inputs.data(),n,outputs.data(),count);write(outputs.data(),count);processedOutput=cumulative;
        }
        const auto tail=static_cast<int>(total-written);if(tail>65536)throw Failure(Error::limitExceeded,"Stretch tail exceeds cache block budget");processor.flush(outputs.data(),tail,static_cast<float>(speed));write(outputs.data(),tail);
    }
    output.flush();if(!output || written!=total)throw Failure(Error::decodeFailed,"Incomplete decoded PCM cache");output.close();cancelled(generation,expected);
    Clip result;result.sampleRate=reader->sampleRate;result.stream=std::make_shared<CachedSource>(cache.path,total,channels,std::move(peaks));cache.transferred=true;return result;
}
}
