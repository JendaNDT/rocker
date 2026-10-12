// SPDX-License-Identifier: AGPL-3.0-only
#include "render_processor.h"
#include "project_mixer.h"
#include <chrono>
#include <cstdlib>
#include <fstream>
#include <iostream>
#include <new>
#ifdef _WIN32
#include <windows.h>
#include <psapi.h>
#else
#include <unistd.h>
#endif
namespace {
thread_local bool audited=false;
uint64_t allocated=0,released=0;
uint64_t resident(){
#ifdef _WIN32
    PROCESS_MEMORY_COUNTERS value{};return GetProcessMemoryInfo(GetCurrentProcess(),&value,sizeof(value))?value.WorkingSetSize:0;
#else
    std::ifstream input("/proc/self/statm");uint64_t size=0,rss=0;input>>size>>rss;return rss*static_cast<uint64_t>(sysconf(_SC_PAGESIZE));
#endif
}
void require(bool condition,const char* text){if(!condition)throw std::runtime_error(text);}
}
void* operator new(std::size_t bytes){if(audited)++allocated;if(void* p=std::malloc(bytes?bytes:1))return p;throw std::bad_alloc();}
void* operator new[](std::size_t bytes){return ::operator new(bytes);}
void operator delete(void* p) noexcept{if(audited && p)++released;std::free(p);}
void operator delete[](void* p) noexcept{::operator delete(p);}
void operator delete(void* p,std::size_t) noexcept{::operator delete(p);}
void operator delete[](void* p,std::size_t) noexcept{::operator delete(p);}
int main(int argc,char** argv){try{
    const uint32_t seconds=argc>2?static_cast<uint32_t>(std::stoul(argv[2])):3600;
    require(seconds>=3 && seconds<=3600,"Duration must be 3..3600 rendered seconds");
    std::ofstream report(argc>1?argv[1]:"performance-checks.json");require(static_cast<bool>(report),"Report destination unavailable");
    report<<"{\"result\":\"passed\",\"mode\":\"accelerated offline production graph\",\"hardwareVerified\":false,\"wallClockHourVerified\":false,\"sampleRate\":48000,\"bufferFrames\":256,\"recordingConsumer\":\"synchronous queue drain; disk speed excluded\",\"pitchWorker\":\"queue drained without analysis; worker tested separately\",\"scenarios\":[";
    const auto allStart=std::chrono::steady_clock::now();
    for(int scenario=0;scenario<3;++scenario){
        rocker::RenderProcessor processor(440,.02);processor.prepare(48000);processor.selectLive();processor.enable(true);processor.input().monitoring(true,.15);
        rocker::AmpParameters amp; amp.enabled=true;amp.values={.4,.6,.3,.3,.3,.25,.25,.2,.4};processor.input().amp().configure(amp,true);
        rocker::MetronomeParameters metro;metro.enabled=true;metro.bpm=120;metro.subdivision=4;processor.input().metronome().configure(metro,true);
        std::unique_ptr<rocker::LoopBuffer> loop;
        if(scenario==1){loop=std::make_unique<rocker::LoopBuffer>(48000);rocker::LoopCommand command;command.buffer=loop.get();require(processor.input().looper().command(command),"Looper install queue");
            for(uint32_t layer=0;layer<8;++layer){for(uint64_t frame=0;frame<48000;++frame)loop->sample(layer,frame)=static_cast<float>(.02*std::sin(frame*2*std::numbers::pi*(110+layer*11)/48000));command.kind=9;command.layer=layer;command.frames=48000;command.rate=48000;command.gain=.5;require(processor.input().looper().command(command),"Looper layer queue");}command.kind=5;require(processor.input().looper().command(command),"Looper resume queue");}
        if(scenario==2){std::vector<rocker::media::ProjectTrackAudio> tracks;for(int index=0;index<8;++index){rocker::media::Clip clip;clip.sampleRate=48000;clip.channels={std::vector<float>(48000)};for(uint64_t frame=0;frame<48000;++frame)clip.channels[0][frame]=static_cast<float>(.02*std::sin(frame*2*std::numbers::pi*(110+index*13)/48000));rocker::media::ProjectTrackSpec spec;spec.gain=.5;spec.pan=(index%2)?.4:-.4;tracks.push_back({spec,std::move(clip)});}rocker::media::Clip project;project.sampleRate=48000;project.stream=std::make_shared<rocker::media::ProjectSource>(std::move(tracks),48000);processor.install(std::move(project),1);processor.selectClip();require(processor.loop(0,1,true),"Project range");}
        auto& tap=processor.input().recording();require(tap.arm({1,1,0,48000,processor.input().clock().observation().epoch}),"Recording arm");
        std::array<float,256> input{},left{},right{};for(size_t i=0;i<input.size();++i)input[i]=static_cast<float>(.08*std::sin(i*2*std::numbers::pi*220/48000));const float* inputs[]{input.data()};float* outputs[]{left.data(),right.data()};
        const uint64_t frames=static_cast<uint64_t>(seconds/3)*48000,blocks=(frames+255)/256;
        std::vector<double> timings(blocks);uint64_t consumed=0,misses=0;double checksum=0;const auto initialAlloc=allocated,initialRelease=released;
        const auto rssStart=resident();const auto start=std::chrono::steady_clock::now();
        for(uint64_t block=0;block<blocks;++block){int count=static_cast<int>(std::min<uint64_t>(256,frames-block*256));auto before=std::chrono::steady_clock::now();audited=true;processor.process(inputs,1,outputs,2,count);audited=false;double elapsed=std::chrono::duration<double,std::micro>(std::chrono::steady_clock::now()-before).count();timings[block]=elapsed;if(elapsed>count*1e6/48000)++misses;
            rocker::RecordingBlock capture;while(tap.queue().pop(capture))consumed+=capture.frames;rocker::AnalysisBlock analysis;while(processor.input().analysis().pop(analysis)){}
            require(std::isfinite(left[count-1]) && std::isfinite(right[count-1]),"Nonfinite production mix");checksum+=left[count-1];
        }
        const double wall=std::chrono::duration<double>(std::chrono::steady_clock::now()-start).count();const auto rssEnd=resident();
        tap.finishJoined(0);rocker::RecordingBlock capture;while(tap.queue().pop(capture))consumed+=capture.frames;
        require(consumed==frames && tap.observation().failure==0,"Missing recording samples");require(allocated==initialAlloc && released==initialRelease,"Callback ownership allocation/release");require(processor.input().meters().dropped==0 && processor.input().meters().invalid==0,"Dropped queue or invalid DSP sample");if(scenario==1)require(processor.input().looper().observation().layers==8,"Eight loop layers preserved");
        std::sort(timings.begin(),timings.end());auto percentile=[&](double value){return timings[static_cast<size_t>((timings.size()-1)*value)];};const double budget=256e6/48000;
        require(percentile(.99)<budget*.5,"P99 callback lacks 50 percent block headroom");
        if(scenario)report<<',';report<<"{\"name\":\""<<(scenario==0?"amp-metronome-recording":scenario==1?"eight-loop-layers":"eight-project-tracks")<<"\",\"renderedSeconds\":"<<frames/48000<<",\"frames\":"<<frames<<",\"recordedFrames\":"<<consumed<<",\"wallSeconds\":"<<wall<<",\"callbackP50Us\":"<<percentile(.5)<<",\"callbackP95Us\":"<<percentile(.95)<<",\"callbackP99Us\":"<<percentile(.99)<<",\"callbackMaxUs\":"<<timings.back()<<",\"blockBudgetUs\":"<<budget<<",\"offlineDeadlineExceedances\":"<<misses<<",\"rssBeforeBytes\":"<<rssStart<<",\"rssAfterBytes\":"<<rssEnd<<",\"callbackAllocations\":0,\"callbackReleases\":0,\"checksum\":"<<checksum<<'}';
        std::cout<<"PASS scenario "<<scenario<<": "<<frames/48000<<" rendered seconds, p99 "<<percentile(.99)<<" us, max "<<timings.back()<<" us\n";
    }
    report<<"],\"wallSeconds\":"<<std::chrono::duration<double>(std::chrono::steady_clock::now()-allStart).count()<<"}\n";
    require(static_cast<bool>(report),"Cannot write performance evidence");
}catch(const std::exception& error){std::cerr<<error.what()<<'\n';return 1;}}
