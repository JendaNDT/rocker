// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include <array>
#include <algorithm>
#include <cmath>
#include <cstdint>
#include <filesystem>
#include <stdexcept>
#ifdef _WIN32
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#else
#include <cerrno>
#include <fcntl.h>
#include <unistd.h>
#endif
namespace rocker {
// Disk-only PCM24 writer. Exclusive creation prevents replacing any prior take.
// Header checkpoints make the saved prefix playable while the recording grows.
class WaveWriter {
public:
    static constexpr uint64_t maximumBytes=1024ull*1024*1024;
    WaveWriter(const std::filesystem::path& path,uint32_t rate,uint32_t channels,uint64_t limit=maximumBytes):rate_(rate),channels_(channels),limit_(limit) {
        if(rate<8000 || rate>192000 || channels<1 || channels>2 || limit<44 || limit>maximumBytes)throw std::invalid_argument("Invalid WAV capture format");
#ifdef _WIN32
        handle_=CreateFileW(path.c_str(),GENERIC_WRITE,FILE_SHARE_READ,nullptr,CREATE_NEW,FILE_ATTRIBUTE_NORMAL,nullptr);
        if(handle_==INVALID_HANDLE_VALUE)throw std::runtime_error("Cannot create a new recording file");
#else
        handle_=::open(path.c_str(),O_WRONLY|O_CREAT|O_EXCL,0600);
        if(handle_<0)throw std::runtime_error("Cannot create a new recording file");
#endif
        try{checkpoint();}catch(...){close();throw;}
    }
    ~WaveWriter(){close();}
    WaveWriter(const WaveWriter&)=delete;WaveWriter& operator=(const WaveWriter&)=delete;
    void append(const float* samples,uint32_t frames){
        if(!samples || frames>256 || ((written_+frames)*channels_*3+44+(((written_+frames)*channels_*3)&1))>limit_)throw std::runtime_error("Recording exceeds the supported 1 GiB file limit");
        std::array<uint8_t,256*2*3> bytes{};
        for(uint32_t i=0;i<frames*channels_;++i){if(!std::isfinite(samples[i]))throw std::runtime_error("Non-finite recording sample");const double value=std::clamp(static_cast<double>(samples[i]),-1.,1.);const int32_t pcm=static_cast<int32_t>(std::llround(value*(value<0?8388608.:8388607.)));for(int b=0;b<3;++b)bytes[i*3+b]=static_cast<uint8_t>(static_cast<uint32_t>(pcm)>>(8*b));}
        write(bytes.data(),frames*channels_*3);written_+=frames;
        if(written_-checkpointFrames_>=rate_)checkpoint();
    }
    void checkpoint(){
        std::array<uint8_t,44> header{};
        auto text=[&](int at,const char* value){for(int i=0;i<4;++i)header[at+i]=static_cast<uint8_t>(value[i]);};
        auto u16=[&](int at,uint16_t value){for(int i=0;i<2;++i)header[at+i]=static_cast<uint8_t>(value>>(i*8));};
        auto u32=[&](int at,uint32_t value){for(int i=0;i<4;++i)header[at+i]=static_cast<uint8_t>(value>>(i*8));};
        const auto size=static_cast<uint32_t>(written_*channels_*3);text(0,"RIFF");u32(4,size+36+(size&1));text(8,"WAVE");text(12,"fmt ");u32(16,16);u16(20,1);u16(22,static_cast<uint16_t>(channels_));u32(24,rate_);u32(28,rate_*channels_*3);u16(32,static_cast<uint16_t>(channels_*3));u16(34,24);text(36,"data");u32(40,size);
        seek(0);write(header.data(),44);seek(44+size);if(size&1){const uint8_t pad=0;write(&pad,1);seek(44+size);}sync();checkpointFrames_=written_;
    }
    uint64_t frames() const noexcept{return written_;}
private:
    void close() noexcept{
#ifdef _WIN32
        if(handle_!=INVALID_HANDLE_VALUE){CloseHandle(handle_);handle_=INVALID_HANDLE_VALUE;}
#else
        if(handle_>=0){::close(handle_);handle_=-1;}
#endif
    }
    void seek(uint64_t offset){
#ifdef _WIN32
        LARGE_INTEGER at;at.QuadPart=static_cast<LONGLONG>(offset);if(!SetFilePointerEx(handle_,at,nullptr,FILE_BEGIN))throw std::runtime_error("Recording seek failed");
#else
        if(::lseek(handle_,static_cast<off_t>(offset),SEEK_SET)<0)throw std::runtime_error("Recording seek failed");
#endif
    }
    void write(const uint8_t* bytes,uint32_t count){
        uint32_t at=0;while(at<count){
#ifdef _WIN32
            DWORD done=0;if(!WriteFile(handle_,bytes+at,count-at,&done,nullptr) || !done)throw std::runtime_error("Recording disk write failed");at+=done;
#else
            const auto done=::write(handle_,bytes+at,count-at);if(done<0 && errno==EINTR)continue;if(done<=0)throw std::runtime_error("Recording disk write failed");at+=static_cast<uint32_t>(done);
#endif
        }
    }
    void sync(){
#ifdef _WIN32
        if(!FlushFileBuffers(handle_))throw std::runtime_error("Recording disk flush failed");
#else
        if(::fsync(handle_)<0)throw std::runtime_error("Recording disk flush failed");
#endif
    }
#ifdef _WIN32
    HANDLE handle_=INVALID_HANDLE_VALUE;
#else
    int handle_=-1;
#endif
    uint32_t rate_,channels_;uint64_t limit_,written_=0,checkpointFrames_=0;
};
}
