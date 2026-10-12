// SPDX-License-Identifier: AGPL-3.0-only
#include "media_decoders.h"
#ifdef _WIN32
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#include <mfapi.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <wrl/client.h>
#include <cmath>
#include <cstring>
#include <limits>

namespace rocker::media::detail {
namespace {
using Microsoft::WRL::ComPtr;
void checked(HRESULT result, const char* message) {
    if (FAILED(result)) throw Failure(Error::decodeFailed, message);
}
struct Module {
    explicit Module(const wchar_t* name) : handle(LoadLibraryExW(name, nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32)) {
        if (!handle) throw Failure(Error::platformUnavailable, "Windows Media Foundation is unavailable");
    }
    ~Module() { FreeLibrary(handle); }
    Module(const Module&) = delete;
    Module& operator=(const Module&) = delete;
    template<class Function> Function function(const char* name) {
        const auto address = GetProcAddress(handle, name);
        if (!address) throw Failure(Error::platformUnavailable, "Media Foundation entry point is unavailable");
        return reinterpret_cast<Function>(address);
    }
    HMODULE handle;
};
struct ComApartment {
    ComApartment() {
        const auto result = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
        if (FAILED(result)) throw Failure(Error::platformUnavailable, "Decode AAC on an MTA worker thread");
    }
    ~ComApartment() { CoUninitialize(); }
};
struct Foundation {
    Module platform{L"mfplat.dll"};
    Module reader{L"mfreadwrite.dll"};
    ComApartment apartment;
    decltype(&MFShutdown) shutdown = platform.function<decltype(&MFShutdown)>("MFShutdown");
    decltype(&MFCreateMediaType) createType = platform.function<decltype(&MFCreateMediaType)>("MFCreateMediaType");
    decltype(&MFCreateSourceReaderFromURL) createReader = reader.function<decltype(&MFCreateSourceReaderFromURL)>("MFCreateSourceReaderFromURL");
    Foundation() {
        const auto startup = platform.function<decltype(&MFStartup)>("MFStartup");
        if (FAILED(startup(MF_VERSION, MFSTARTUP_FULL)))
            throw Failure(Error::platformUnavailable, "Cannot initialize Windows Media Foundation");
    }
    ~Foundation() { shutdown(); }
};
struct BufferLock {
    explicit BufferLock(IMFMediaBuffer* buffer) : buffer(buffer) {
        checked(buffer->Lock(&data, nullptr, &length), "Cannot lock decoded AAC buffer");
    }
    ~BufferLock() { buffer->Unlock(); }
    IMFMediaBuffer* buffer;
    BYTE* data = nullptr;
    DWORD length = 0;
};
}
PcmShape decodeMp4Chunks(const std::filesystem::path& path, Limits limits,const ChunkSink& sink,const CancelCheck& cancellation) {
    if(cancellation)cancellation();
    if(std::filesystem::file_size(path)>limits.encodedBytes)throw Failure(Error::limitExceeded,"MP4 exceeds encoded budget");
    // No eager mfplat/mfreadwrite imports: optional Windows N components may be
    // absent, without preventing the native host/window from loading.
    Foundation system;
    ComPtr<IMFSourceReader> reader;
    checked(system.createReader(std::filesystem::absolute(path).c_str(), nullptr, &reader), "Cannot open MP4 audio");
    checked(reader->SetStreamSelection(MF_SOURCE_READER_ALL_STREAMS, FALSE), "Cannot deselect MP4 streams");
    checked(reader->SetStreamSelection(MF_SOURCE_READER_FIRST_AUDIO_STREAM, TRUE), "MP4 has no audio stream");
    ComPtr<IMFMediaType> requested;
    checked(system.createType(&requested), "Cannot create AAC output type");
    checked(requested->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio), "Cannot set audio type");
    checked(requested->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_Float), "Cannot request float PCM");
    checked(reader->SetCurrentMediaType(MF_SOURCE_READER_FIRST_AUDIO_STREAM, nullptr, requested.Get()), "AAC decoder cannot produce float PCM");
    ComPtr<IMFMediaType> actual;
    checked(reader->GetCurrentMediaType(MF_SOURCE_READER_FIRST_AUDIO_STREAM, &actual), "Cannot read AAC output type");
    UINT32 rate = 0, channels = 0, bits = 0, alignment = 0;
    checked(actual->GetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, &rate), "Missing AAC sample rate");
    checked(actual->GetUINT32(MF_MT_AUDIO_NUM_CHANNELS, &channels), "Missing AAC channels");
    checked(actual->GetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, &bits), "Missing AAC PCM depth");
    checked(actual->GetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, &alignment), "Missing AAC PCM alignment");
    if (channels < 1 || channels > 2 || rate < 8000 || rate > 192000 || bits != 32 || alignment != channels * sizeof(float))
        throw Failure(Error::unsupportedFormat, "Unsupported decoded AAC shape");
    uint64_t totalFrames=0;
    unsigned noProgress = 0;
    while (true) {
        if(cancellation)cancellation();
        DWORD flags = 0;
        ComPtr<IMFSample> sample;
        checked(reader->ReadSample(MF_SOURCE_READER_FIRST_AUDIO_STREAM, 0, nullptr, &flags, nullptr, &sample), "AAC read failed");
        if (flags & (MF_SOURCE_READERF_ERROR | MF_SOURCE_READERF_CURRENTMEDIATYPECHANGED | MF_SOURCE_READERF_STREAMTICK))
            throw Failure(Error::decodeFailed, "AAC stream changed format or has a timeline gap");
        if (sample) {
            ComPtr<IMFMediaBuffer> buffer;
            checked(sample->ConvertToContiguousBuffer(&buffer), "Cannot access AAC PCM");
            BufferLock locked(buffer.Get());
            if (locked.length % alignment != 0) throw Failure(Error::decodeFailed, "Misaligned AAC PCM");
            const std::size_t count = locked.length / alignment;
            if (totalFrames + count > limits.pcmBytes / alignment || totalFrames + count > static_cast<std::size_t>(std::numeric_limits<uint32_t>::max()))
                throw Failure(Error::limitExceeded, "AAC PCM exceeds budget");
            if(count>262144)throw Failure(Error::limitExceeded,"AAC chunk exceeds bounded worker budget");
            std::vector<float> chunk(count*channels);std::memcpy(chunk.data(),locked.data,locked.length);const auto* pcm=chunk.data();
            for(size_t n=0;n<count*channels;++n)if(!std::isfinite(pcm[n]))throw Failure(Error::decodeFailed,"Non-finite AAC PCM");
            if(cancellation)cancellation();if(count)sink(pcm,count,channels,rate);totalFrames+=count;
            noProgress = count ? 0 : noProgress + 1;
        } else ++noProgress;
        if (flags & MF_SOURCE_READERF_ENDOFSTREAM) break;
        if (noProgress > 1024) throw Failure(Error::decodeFailed, "AAC decoder made no progress");
    }
    if (totalFrames == 0) throw Failure(Error::decodeFailed, "Empty AAC stream");
    return {static_cast<double>(rate),channels,totalFrames};
}
Clip decodeMp4(const std::filesystem::path& path,Limits limits){
    Clip clip;auto shape=decodeMp4Chunks(path,limits,[&](const float* data,size_t count,uint32_t channels,double rate){clip.sampleRate=rate;if(clip.channels.empty())clip.channels.resize(channels);for(uint32_t c=0;c<channels;++c)for(size_t n=0;n<count;++n)clip.channels[c].push_back(data[n*channels+c]);},{});(void)shape;return clip;
}

}
#else
namespace rocker::media::detail {
PcmShape decodeMp4Chunks(const std::filesystem::path&,Limits,const ChunkSink&,const CancelCheck&) {
    throw Failure(Error::platformUnavailable,"MP4/AAC requires Windows Media Foundation");
}
Clip decodeMp4(const std::filesystem::path&, Limits) {
    throw Failure(Error::platformUnavailable, "MP4/AAC requires Windows Media Foundation");
}
}
#endif
