// SPDX-License-Identifier: AGPL-3.0-only
#include "audio_driver.h"
#include "rocker_audio.h"
#include <array>
#include <thread>

namespace { std::atomic<uint32_t> startDelay{0}; std::atomic<double> lastPeak{0}; std::atomic<bool> failDevice{false}; std::atomic<uint32_t> testFrames{1024}; std::atomic<uint64_t> starts{0}; }
extern "C" ROCKER_API void ROCKER_CALL rocker_test_set_failure(int enabled) { failDevice.store(enabled != 0); }
extern "C" ROCKER_API void ROCKER_CALL rocker_test_set_frames(uint32_t frames) { testFrames.store(std::clamp(frames, 1u, 16384u)); }
extern "C" ROCKER_API uint64_t ROCKER_CALL rocker_test_start_count() { return starts.load(); }

extern "C" ROCKER_API void ROCKER_CALL rocker_test_set_start_delay(uint32_t milliseconds) { startDelay.store(std::min(milliseconds,2000u)); }
extern "C" ROCKER_API double ROCKER_CALL rocker_test_last_peak() { return lastPeak.load(); }

namespace rocker {
class FakeDriver final : public AudioDriver {
public:
    bool start(RenderProcessor& tone, std::string& error) override {
        std::this_thread::sleep_for(std::chrono::milliseconds(startDelay.load()));
        if (failDevice.load()) {
            error = "Test device unavailable: zařízení";
            return false;
        }
        tone.prepare(48000);
        starts.fetch_add(1);
        std::array<float, 16384> left{}, right{};
        float* channels[]{left.data(), right.data()};
        tone.render(channels, 2, static_cast<int>(testFrames.load()));
        double peak=0;for(size_t i=0;i<testFrames.load();++i)peak=std::max(peak,std::abs(static_cast<double>(left[i])));lastPeak.store(peak);
        running_ = true;
        return true;
    }
    void stop() noexcept override { running_ = false; }
    DriverSnapshot snapshot() const noexcept override {
        return running_ ? DriverSnapshot{48000, testFrames.load(), 2, false} : DriverSnapshot{};
    }
private:
    bool running_ = false;
};
std::unique_ptr<AudioDriver> makeAudioDriver() { return std::make_unique<FakeDriver>(); }
}
