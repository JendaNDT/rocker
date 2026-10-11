// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include <cstdint>
#include <filesystem>
#include <stdexcept>
#include <vector>
#include <array>
#include <atomic>
#include <memory>

namespace rocker::media {
// Offline worker API. Never decode or stretch in the real-time audio callback.
// Owned planar PCM preserves source channels and rate; routing/resampling belongs
// to the common transport, not a second device engine.
class PcmSource {
public:
    virtual ~PcmSource()=default;
    virtual void beginBlock() noexcept {}
    virtual bool ownsEnvelope() const noexcept{return false;}
    virtual bool read(uint64_t frame,std::array<float,2>& values) noexcept=0;
    virtual void request(uint64_t frame) noexcept=0;
    virtual void loopHint(uint64_t frame) noexcept=0;
    virtual uint64_t frames() const noexcept=0;
    virtual uint32_t channels() const noexcept=0;
    virtual bool failed() const noexcept=0;
    virtual const std::vector<float>& waveform() const noexcept=0;
};
struct Clip {
    double sampleRate = 0;
    std::vector<std::vector<float>> channels;
    std::shared_ptr<PcmSource> stream;
    std::size_t frames() const noexcept { return stream?static_cast<size_t>(stream->frames()):channels.empty()?0:channels.front().size(); }
    size_t channelCount() const noexcept{return stream?stream->channels():channels.size();}
};
enum class Error { invalidInput, unsupportedFormat, decodeFailed, limitExceeded, invalidRate, tooShort, platformUnavailable };
class Failure : public std::runtime_error {
public:
    Failure(Error code, const char* message) : std::runtime_error(message), code(code) {}
    const Error code;
};
struct Limits {
    std::uint64_t encodedBytes = 64ull * 1024 * 1024;
    std::uint64_t pcmBytes = 64ull * 1024 * 1024;
    std::uint64_t combinedPcmBytes = 128ull * 1024 * 1024;
};
Clip decode(const std::filesystem::path&, Limits limits = {});
// Bounded chunk decode/stretch to a temporary PCM cache; its read-ahead worker
// owns disk access. Cancellation is checked between chunks, never on the RT thread.
Clip prepareFile(const std::filesystem::path&,double speed,const std::atomic<uint64_t>& generation,uint64_t expected);
std::vector<float> waveform(const Clip&,size_t bins=1024);
// 0.25x–2x speed; 1x is an exact copy. Samples of non-unity speeds are spectral
// reconstructions, not bit-exact. Very short clips are explicitly rejected.
Clip stretch(const Clip&, double speed, Limits limits = {});
// Bounded offline procedural plucked-string synthesis; no SoundFont or second
// device is opened. SMF 0/1, note-on/off, tempo and sustain are supported.
Clip synthesizeMidi(const uint8_t* bytes, uint32_t count, Limits limits = {});
}
