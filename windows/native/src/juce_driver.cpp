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
    RuntimeLease() { if (runtimeClaimed.exchange(true)) throw std::runtime_error("Only one real audio host is supported per process."); }
    ~RuntimeLease() { runtimeClaimed.store(false); }
};
juce::var object() { return new juce::DynamicObject(); }
void field(juce::var& value, const char* key, const juce::var& data) { value.getDynamicObject()->setProperty(key, data); }
template<class List> juce::var array(const List& items) { juce::Array<juce::var> result; for(const auto& item:items) result.add(juce::var(item)); return result; }
std::string json(const juce::var& value) { return juce::JSON::toString(value, true).toStdString(); }
}
class JuceDriver final : public AudioDriver, private juce::AudioIODeviceCallback {
public:
    JuceDriver() {
        std::promise<void> ready; auto initialized = ready.get_future();
        worker_ = std::thread([this, &ready] {
            bool announced = false;
            try {
                juce::ScopedJuceInitialiser_GUI runtime;
                juce::AudioDeviceManager enumerator;
                manager_ = &enumerator; announced = true; ready.set_value();
                juce::MessageManager::getInstance()->runDispatchLoop();
                stopOnControl(); manager_ = nullptr;
            } catch (...) { if (!announced) ready.set_exception(std::current_exception()); failed_.store(true); }
        });
        try { initialized.get(); } catch (...) { worker_.join(); throw; }
    }
    ~JuceDriver() override {
        stop();
        if (auto* messages = juce::MessageManager::getInstanceWithoutCreating()) messages->stopDispatchLoop();
        if (worker_.joinable()) worker_.join();
    }
    std::string catalog() override { return onControl([this] {
        auto root=object(); field(root,"schemaVersion",1); juce::Array<juce::var> backends;
        for(auto* type:manager_->getAvailableDeviceTypes()) {
            type->scanForDevices(); auto item=object(); field(item,"name",type->getTypeName());
            field(item,"inputs",array(type->getDeviceNames(true))); field(item,"outputs",array(type->getDeviceNames(false)));
            field(item,"separateDevices",type->hasSeparateInputsAndOutputs()); backends.add(item);
        }
        field(root,"backends",backends); return json(root);
    }); }
    std::string capabilities(const DeviceSelection& choice) override { return onControl([&] {
        // No open/start: inspection never interrupts the current callback.
        auto device=create(choice); auto root=object(); field(root,"schemaVersion",1);
        field(root,"inputs",array(device->getInputChannelNames())); field(root,"outputs",array(device->getOutputChannelNames()));
        auto rates=device->getAvailableSampleRates();for(int i=rates.size()-1;i>=0;--i)if(rates[i]<8000 || rates[i]>192000)rates.remove(i);
        auto buffers=device->getAvailableBufferSizes();for(int i=buffers.size()-1;i>=0;--i)if(buffers[i]<16 || buffers[i]>8192)buffers.remove(i);
        field(root,"rates",array(rates)); field(root,"buffers",array(buffers));
        field(root,"controlPanel",device->hasControlPanel()); return json(root);
    }); }
    bool configure(const DeviceSelection& choice,std::string& error) override { return onControl([&] {
        stopOnControl(); failed_.store(false);
        // A first failed explicit choice must not fall back to a default later.
        if(!selected_){selection_=choice;selected_=true;}
        try {
            auto probe=create(choice);
            if(!open(*probe,choice,error)) { probe->close();return false; }
            lastDetails_=describe(*probe,choice); probe->close(); selection_=choice; selected_=true; return true;
        } catch(const std::exception& failure){error=failure.what();return false;}
    }); }
    std::string details() override { return onControl([this] { return device_?describe(*device_,selection_):lastDetails_; }); }
    bool controlPanel(std::string& error) override { return onControl([&] {
        if(device_) { error="Stop audio before opening the driver control panel.";return false; }
        if(!selected_) {error="Select an audio device first.";return false;}
        try {
            auto probe=create(selection_); if(!open(*probe,selection_,error)){probe->close();return false;}
            if(!probe->hasControlPanel()){probe->close();error="This driver has no control panel.";return false;}
            const bool shown=probe->showControlPanel(); lastDetails_=describe(*probe,selection_);probe->close();
            if(!shown)error="The driver could not display its control panel."; return shown;
        }catch(const std::exception& failure){error=failure.what();return false;}
    }); }
    bool start(RenderProcessor& renderer, std::string& error) override { return onControl([&] {
        stopOnControl(); failed_.store(false);
        try {
            if(!selected_) {
                // The first explicit start resolves a default once. Later starts
                // keep that exact name; hotplug never opens laptop speakers.
                for(auto* type:manager_->getAvailableDeviceTypes()) {
#if JUCE_WINDOWS
                    if(type->getTypeName()!="Windows Audio")continue;
#endif
                    type->scanForDevices(); const auto outputs=type->getDeviceNames(false); const int index=type->getDefaultDeviceIndex(false);
                    if(index>=0 && index<outputs.size()){selection_.backend=type->getTypeName().toStdString();selection_.output=outputs[index].toStdString();selected_=true;break;}
                }
                if(!selected_){error="No output audio device is available.";return false;}
            }
            if(renderer.mode()==2 && selection_.inputChannel<0){error="Select a physical guitar input before starting Live.";return false;}
            auto candidate=create(selection_); if(!open(*candidate,selection_,error)){candidate->close();return false;}
            lastDetails_=describe(*candidate,selection_);device_=std::move(candidate);tone_=&renderer;stopping_.store(false);device_->start(this);
            if(!device_->isPlaying()){error="The audio driver did not start callbacks.";stopOnControl();return false;}
            return true;
        }catch(const std::exception& failure){error=failure.what();stopOnControl();return false;}
    }); }
    void stop() noexcept override { try { onControl([this]{stopOnControl();}); } catch(...){failed_.store(true);} }
    DriverSnapshot snapshot() const noexcept override { return {sampleRate_.load(), bufferFrames_.load(), channels_.load(), failed_.load()}; }
private:
    juce::AudioIODeviceType* findType(const std::string& backend) {
        for(auto* type:manager_->getAvailableDeviceTypes()) if(type->getTypeName()==juce::String::fromUTF8(backend.c_str())){type->scanForDevices();return type;}
        throw std::runtime_error("The selected driver is unavailable. Choose a replacement explicitly.");
    }
    std::unique_ptr<juce::AudioIODevice> create(const DeviceSelection& choice) {
        auto* type=findType(choice.backend);const auto input=juce::String::fromUTF8(choice.input.c_str()),output=juce::String::fromUTF8(choice.output.c_str());
        if(output.isEmpty() || !type->getDeviceNames(false).contains(output) || (input.isNotEmpty() && !type->getDeviceNames(true).contains(input)))
            throw std::runtime_error("The selected audio device is unavailable. No automatic fallback was used.");
        if(!type->hasSeparateInputsAndOutputs() && input.isNotEmpty() && input!=output)throw std::runtime_error("This driver requires the same input and output device.");
        std::unique_ptr<juce::AudioIODevice> result(type->createDevice(output,input));
        if(!result)throw std::runtime_error("The selected input/output pair could not be created.");return result;
    }
    bool open(juce::AudioIODevice& device,const DeviceSelection& choice,std::string& error) {
        juce::BigInteger inputs,outputs;
        const auto inputCount=device.getInputChannelNames().size(),outputCount=device.getOutputChannelNames().size();
        if(choice.inputChannel>=0 && (choice.input.empty() || choice.inputChannel>=inputCount)){error="The selected guitar input channel is unavailable.";return false;}
        if(choice.outputFirst>=static_cast<uint32_t>(outputCount) || (choice.outputFirst+1>=static_cast<uint32_t>(outputCount) && outputCount!=1)){error="The selected stereo output pair is unavailable.";return false;}
        if(choice.inputChannel>=0)inputs.setBit(choice.inputChannel);outputs.setBit(static_cast<int>(choice.outputFirst));if(outputCount>1)outputs.setBit(static_cast<int>(choice.outputFirst+1));
        const auto rates=device.getAvailableSampleRates();const auto sizes=device.getAvailableBufferSizes();
        if(choice.rate!=0 && !rates.isEmpty() && !rates.contains(choice.rate)){error="The selected sample rate is not supported.";return false;}
        if(choice.frames!=0 && !sizes.isEmpty() && !sizes.contains(static_cast<int>(choice.frames))){error="The selected buffer size is not supported.";return false;}
        // Exactly one physical input is active; JUCE packs it to callback input[0].
        const auto message=device.open(inputs,outputs,choice.rate,static_cast<int>(choice.frames));
        if(message.isNotEmpty()){error=message.toStdString();return false;}
        if((choice.rate!=0 && std::abs(device.getCurrentSampleRate()-choice.rate)>0.5) || (choice.frames!=0 && device.getCurrentBufferSizeSamples()!=static_cast<int>(choice.frames))){error="The driver changed the requested format. Choose its default values or a supported format.";return false;}
        if(device.getActiveInputChannels()!=inputs || device.getActiveOutputChannels()!=outputs){error="The driver changed the selected physical channel routing.";return false;}
        if(device.getActiveOutputChannels().isZero()){error="The driver opened without an output.";return false;}
        return true;
    }
    static std::string describe(juce::AudioIODevice& device,const DeviceSelection& choice) {
        auto value=object();field(value,"schemaVersion",1);field(value,"backend",choice.backend.c_str());field(value,"input",choice.input.c_str());field(value,"output",choice.output.c_str());
        field(value,"sampleRate",device.getCurrentSampleRate());field(value,"bufferFrames",device.getCurrentBufferSizeSamples());
        field(value,"inputChannels",device.getActiveInputChannels().countNumberOfSetBits());field(value,"outputChannels",device.getActiveOutputChannels().countNumberOfSetBits());
        field(value,"inputLatencyFrames",device.getInputLatencyInSamples());field(value,"outputLatencyFrames",device.getOutputLatencyInSamples());field(value,"controlPanel",device.hasControlPanel());return json(value);
    }
    template<class Operation> auto onControl(Operation operation)->decltype(operation()) {
        using Result=decltype(operation());auto completion=std::make_shared<std::promise<Result>>();auto result=completion->get_future();
        if(!juce::MessageManager::callAsync([completion,operation=std::move(operation)]() mutable {
            try{if constexpr(std::is_void_v<Result>){operation();completion->set_value();}else completion->set_value(operation());}catch(...){completion->set_exception(std::current_exception());}
        }))throw std::runtime_error("Audio control thread is unavailable.");return result.get();
    }
    void stopOnControl() noexcept {
        stopping_.store(true);if(device_){device_->stop();device_->close();device_.reset();}tone_=nullptr;
        sampleRate_.store(0);bufferFrames_.store(0);channels_.store(0);
    }
    void audioDeviceAboutToStart(juce::AudioIODevice* device) override {
        if(sampleRate_.load()!=0 && !stopping_.load()){tone_->enable(false);failed_.store(true);}
        const auto rate=device->getCurrentSampleRate();sampleRate_.store(rate);bufferFrames_.store(static_cast<uint32_t>(device->getCurrentBufferSizeSamples()));channels_.store(static_cast<uint32_t>(device->getActiveOutputChannels().countNumberOfSetBits()));
        if(rate<=0 || !std::isfinite(rate)){failed_.store(true);tone_->enable(false);}tone_->prepare(rate>0 && std::isfinite(rate)?rate:48000);
    }
    void audioDeviceIOCallbackWithContext(const float* const* inputs,int inputChannels,float* const* outputs,int channels,int frames,const juce::AudioIODeviceCallbackContext&) override { tone_->process(inputs,inputChannels,outputs,channels,frames); }
    void audioDeviceStopped() override {if(!stopping_.load()){tone_->enable(false);failed_.store(true);}}
    void audioDeviceError(const juce::String&) override {if(tone_)tone_->enable(false);failed_.store(true);}
    RuntimeLease lease_;std::thread worker_;juce::AudioDeviceManager* manager_=nullptr;std::unique_ptr<juce::AudioIODevice> device_;
    DeviceSelection selection_;bool selected_=false;std::string lastDetails_="{\"schemaVersion\":1,\"available\":false}";
    RenderProcessor* tone_=nullptr;std::atomic<bool> stopping_{true},failed_{false};std::atomic<double> sampleRate_{0};std::atomic<uint32_t> bufferFrames_{0},channels_{0};
};
std::unique_ptr<AudioDriver> makeAudioDriver(){return std::make_unique<JuceDriver>();}
}
