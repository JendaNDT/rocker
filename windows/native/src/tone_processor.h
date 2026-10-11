// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdint>
#include <numbers>

namespace rocker {
class ToneProcessor {
public:
    ToneProcessor(double frequency, double amplitude) : frequency_(frequency), amplitude_(amplitude) {}
    // Device lifecycle must exclude render while prepare resets callback-owned state.
    void prepare(double rate) noexcept {
        sampleRate_ = rate;
        phase_ = 0;
        gain_ = 0;
        frames_.store(0, std::memory_order_relaxed);
    }
    void enable(bool enabled) noexcept { enabled_.store(enabled, std::memory_order_relaxed); }
    void render(float* const* outputs, int channels, int frames) noexcept {
        if (channels <= 0 || frames <= 0 || !outputs) return;
        const auto target = enabled_.load(std::memory_order_relaxed) ? amplitude_ : 0.0;
        const auto increment = 2.0 * std::numbers::pi * frequency_ / sampleRate_;
        const auto ramp = amplitude_ / (sampleRate_ * 0.01);
        for (int i = 0; i < frames; ++i) {
            gain_ += std::clamp(target - gain_, -ramp, ramp);
            const auto value = static_cast<float>(std::sin(phase_) * gain_);
            for (int c = 0; c < channels; ++c) if (outputs[c]) outputs[c][i] = value;
            phase_ += increment;
            if (phase_ >= 2.0 * std::numbers::pi) phase_ -= 2.0 * std::numbers::pi;
        }
        frames_.fetch_add(static_cast<uint64_t>(frames), std::memory_order_relaxed);
    }
    uint64_t frames() const noexcept { return frames_.load(std::memory_order_relaxed); }
private:
    static_assert(std::atomic<uint64_t>::is_always_lock_free);
    static_assert(std::atomic<bool>::is_always_lock_free);
    const double frequency_, amplitude_;
    double sampleRate_ = 48000, phase_ = 0, gain_ = 0;
    std::atomic<bool> enabled_{false};
    std::atomic<uint64_t> frames_{0};
};
}
