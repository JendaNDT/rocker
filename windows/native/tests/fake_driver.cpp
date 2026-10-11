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
        tone.prepare(choice_.rate?choice_.rate:48000);
        starts.fetch_add(1);
        std::array<float, 16384> left{}, right{};
        float* channels[]{left.data(), right.data()};
        tone.render(channels, 2, static_cast<int>(choice_.frames?choice_.frames:testFrames.load()));
        double peak=0;for(size_t i=0;i<(choice_.frames?choice_.frames:testFrames.load());++i)peak=std::max(peak,std::abs(static_cast<double>(left[i])));lastPeak.store(peak);
        running_ = true;
        return true;
    }
    std::string catalog() override { return R"({"schemaVersion":1,"backends":[{"name":"Test ASIO","inputs":["Test interface"],"outputs":["Test interface"],"separateDevices":false}]})"; }
    std::string capabilities(const DeviceSelection& choice) override {
        if(choice.backend!="Test ASIO" || choice.output!="Test interface" || (!choice.input.empty() && choice.input!="Test interface"))throw std::runtime_error("Missing selected device");
        return R"({"schemaVersion":1,"inputs":["Input 1","Input 2"],"outputs":["Left","Right"],"rates":[44100,48000],"buffers":[64,128,256,1024],"controlPanel":true})";
    }
    bool configure(const DeviceSelection& choice,std::string& error) override {
        try{capabilities(choice);}catch(const std::exception& e){error=e.what();return false;}
        if(failDevice.load() || (choice.rate!=0 && choice.rate!=44100 && choice.rate!=48000) || (choice.frames!=0 && choice.frames!=64 && choice.frames!=128 && choice.frames!=256 && choice.frames!=1024) || choice.inputChannel>1 || choice.outputFirst!=0){error="Unsupported test device configuration";return false;}
        choice_=choice;return true;
    }
    std::string details() override { return std::string(R"({"schemaVersion":1,"backend":"Test ASIO","input":"Test interface","output":"Test interface","sampleRate":)")+std::to_string(choice_.rate?choice_.rate:48000)+",\"bufferFrames\":"+std::to_string(choice_.frames?choice_.frames:1024)+R"(,"inputChannels":1,"outputChannels":2,"inputLatencyFrames":32,"outputLatencyFrames":64,"controlPanel":true})"; }
    bool controlPanel(std::string&) override { return true; }
    void stop() noexcept override { running_ = false; }
    DriverSnapshot snapshot() const noexcept override {
        return running_ ? DriverSnapshot{choice_.rate?choice_.rate:48000, choice_.frames?choice_.frames:testFrames.load(), 2, false} : DriverSnapshot{};
    }
private:
    bool running_ = false;
    DeviceSelection choice_;
};
std::unique_ptr<AudioDriver> makeAudioDriver() { return std::make_unique<FakeDriver>(); }
}
