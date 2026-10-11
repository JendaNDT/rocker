// SPDX-License-Identifier: AGPL-3.0-only
#include "audio_driver.h"
#include "rocker_audio.h"
#include <array>

namespace { std::atomic<bool> failDevice{false}; }
extern "C" ROCKER_API void ROCKER_CALL rocker_test_set_failure(int enabled) { failDevice.store(enabled != 0); }

namespace rocker {
class FakeDriver final : public AudioDriver {
public:
    bool start(ToneProcessor& tone, std::string& error) override {
        if (failDevice.load()) {
            error = "Test device unavailable: zařízení";
            return false;
        }
        tone.prepare(48000);
        std::array<float, 1024> left{}, right{};
        float* channels[]{left.data(), right.data()};
        tone.render(channels, 2, 1024);
        running_ = true;
        return true;
    }
    void stop() noexcept override { running_ = false; }
    DriverSnapshot snapshot() const noexcept override {
        return running_ ? DriverSnapshot{48000, 1024, 2, false} : DriverSnapshot{};
    }
private:
    bool running_ = false;
};
std::unique_ptr<AudioDriver> makeAudioDriver() { return std::make_unique<FakeDriver>(); }
}
