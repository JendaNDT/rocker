// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include <cstdint>
#include <filesystem>
#include <stdexcept>
#include <vector>

namespace rocker::media {
// Offline worker API. Never decode or stretch in the real-time audio callback.
// Owned planar PCM preserves source channels and rate; routing/resampling belongs
// to the common transport, not a second device engine.
struct Clip {
    double sampleRate = 0;
    std::vector<std::vector<float>> channels;
    std::size_t frames() const noexcept { return channels.empty() ? 0 : channels.front().size(); }
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
// 0.25x–2x speed; 1x is an exact copy. Samples of non-unity speeds are spectral
// reconstructions, not bit-exact. Very short clips are explicitly rejected.
Clip stretch(const Clip&, double speed, Limits limits = {});
}
