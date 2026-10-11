// SPDX-License-Identifier: AGPL-3.0-only
#include "audio_driver.h"
#include <juce_audio_devices/juce_audio_devices.h>
#include <future>
#include <thread>
#include <type_traits>

namespace rocker {
namespace {
std::atomic<bool> runtimeClaimed{false};
struct RuntimeLease {
    RuntimeLease() {
        if (runtimeClaimed.exchange(true)) throw std::runtime_error("Only one real audio host is supported per process.");
    }
    ~RuntimeLease() { runtimeClaimed.store(false); }
};
}
class JuceDriver final : public AudioDriver, private juce::AudioIODeviceCallback {
public:
    JuceDriver() {
        std::promise<void> ready;
        auto initialized = ready.get_future();
        worker_ = std::thread([this, &ready] {
            bool announced = false;
            try {
                juce::ScopedJuceInitialiser_GUI runtime;
                {
                    juce::AudioDeviceManager manager;
                    manager_ = &manager;
                    announced = true;
                    ready.set_value();
                    juce::MessageManager::getInstance()->runDispatchLoop();
                    stopOnControl();
                    manager_ = nullptr;
                }
            } catch (...) {
                if (!announced) ready.set_exception(std::current_exception());
                failed_.store(true);
            }
        });
        try { initialized.get(); }
        catch (...) { worker_.join(); throw; }
    }
    ~JuceDriver() override {
        stop();
        if (auto* messages = juce::MessageManager::getInstanceWithoutCreating()) messages->stopDispatchLoop();
        if (worker_.joinable()) worker_.join();
    }
    bool start(ToneProcessor& tone, std::string& error) override {
        return onControl([&] {
            stopOnControl();
            failed_.store(false);
            // No input is opened in W03. Guitar routing and ASIO selection belong to W14/W15.
           #if JUCE_WINDOWS
            bool wasapi = false;
            for (auto* type : manager_->getAvailableDeviceTypes())
                if (type->getTypeName() == "Windows Audio") wasapi = true;
            if (!wasapi) { error = "WASAPI is unavailable."; return false; }
            manager_->setCurrentAudioDeviceType("Windows Audio", false);
           #endif
            const auto message = manager_->initialise(0, 2, nullptr, true);
            if (message.isNotEmpty()) { error = message.toStdString(); manager_->closeAudioDevice(); return false; }
            auto* device = manager_->getCurrentAudioDevice();
            if (!device || device->getActiveOutputChannels().isZero()) {
                error = "No output audio device is available."; return false;
            }
            tone_ = &tone;
            stopping_.store(false);
            manager_->addAudioCallback(this);
            attached_ = true;
            return true;
        });
    }
    void stop() noexcept override {
        try { onControl([this] { stopOnControl(); }); }
        catch (...) { failed_.store(true); }
    }
    DriverSnapshot snapshot() const noexcept override {
        return {sampleRate_.load(), bufferFrames_.load(), channels_.load(), failed_.load()};
    }
private:
    // JUCE owns a separate message loop: even SafeHandle finalization never
    // destroys device/message resources on the managed GC or WinUI thread.
    template <typename Operation> auto onControl(Operation operation) -> decltype(operation()) {
        using Result = decltype(operation());
        auto completion = std::make_shared<std::promise<Result>>();
        auto result = completion->get_future();
        if (!juce::MessageManager::callAsync([completion, operation = std::move(operation)]() mutable {
            try {
                if constexpr (std::is_void_v<Result>) { operation(); completion->set_value(); }
                else completion->set_value(operation());
            } catch (...) { completion->set_exception(std::current_exception()); }
        })) throw std::runtime_error("Audio control thread is unavailable.");
        return result.get();
    }
    void stopOnControl() noexcept {
        stopping_.store(true);
        if (attached_) { manager_->removeAudioCallback(this); attached_ = false; }
        manager_->closeAudioDevice();
        tone_ = nullptr;
        sampleRate_.store(0); bufferFrames_.store(0); channels_.store(0);
    }
    void audioDeviceAboutToStart(juce::AudioIODevice* device) override {
        const auto rate = device->getCurrentSampleRate();
        sampleRate_.store(rate);
        bufferFrames_.store(static_cast<uint32_t>(device->getCurrentBufferSizeSamples()));
        channels_.store(static_cast<uint32_t>(device->getActiveOutputChannels().countNumberOfSetBits()));
        if (rate <= 0 || !std::isfinite(rate)) { failed_.store(true); tone_->enable(false); }
        tone_->prepare(rate > 0 && std::isfinite(rate) ? rate : 48000);
    }
    void audioDeviceIOCallbackWithContext(const float* const*, int, float* const* output,
        int channels, int frames, const juce::AudioIODeviceCallbackContext&) override {
        tone_->render(output, channels, frames);
    }
    void audioDeviceStopped() override { if (!stopping_.load()) failed_.store(true); }
    void audioDeviceError(const juce::String&) override { failed_.store(true); }
    static_assert(std::atomic<double>::is_always_lock_free);
    RuntimeLease lease_;
    std::thread worker_;
    juce::AudioDeviceManager* manager_ = nullptr;
    ToneProcessor* tone_ = nullptr;
    bool attached_ = false;
    std::atomic<bool> stopping_{true}, failed_{false};
    std::atomic<double> sampleRate_{0};
    std::atomic<uint32_t> bufferFrames_{0}, channels_{0};
};
std::unique_ptr<AudioDriver> makeAudioDriver() { return std::make_unique<JuceDriver>(); }
}
