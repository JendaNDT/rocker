// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include "media_clip.h"
#include <functional>
namespace rocker::media::detail {
struct PcmShape { double sampleRate; uint32_t channels; uint64_t frames; };
using ChunkSink=std::function<void(const float*,size_t,uint32_t,double)>;
using CancelCheck=std::function<void()>;
PcmShape decodeWebmChunks(const std::filesystem::path&,Limits,const ChunkSink&,const CancelCheck&);
PcmShape decodeMp4Chunks(const std::filesystem::path&,Limits,const ChunkSink&,const CancelCheck&);
Clip decodeWebm(const std::filesystem::path&, Limits);
Clip decodeMp4(const std::filesystem::path&, Limits);
}
