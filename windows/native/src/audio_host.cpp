// SPDX-License-Identifier: AGPL-3.0-only
#include "rocker_audio.h"
#include "audio_driver.h"
#include "control_executor.h"
#include <algorithm>
#include <cstring>
#include <mutex>
#if ROCKER_WITH_MEDIA
#include "media_clip.h"
#endif

static_assert(sizeof(rocker_audio_config) == 24);
static_assert(sizeof(rocker_audio_snapshot) == 40);
static_assert(sizeof(rocker_transport_snapshot) == 56);
static_assert(sizeof(rocker_device_config)==32);
struct rocker_audio_host;
static void refreshHost(rocker_audio_host&);
struct rocker_audio_host {
    explicit rocker_audio_host(const rocker_audio_config& config)
        : tone(config.frequency_hz, config.amplitude), driver(rocker::makeAudioDriver()) {
        tone.authority(intent);errorCache.reserve(65536);publish();
        executor=std::make_unique<rocker::ControlExecutor>([this]{
            if(stopRequested.exchange(false))stopOnControl();
            refreshHost(*this);publish();
        },[this]{state=ROCKER_CLOSING;tone.enable(false);driver->stop();publish();});
    }
    ~rocker_audio_host(){tone.enable(false);intent.fetch_add(1);executor.reset();}
    rocker::RenderProcessor tone;
    std::unique_ptr<rocker::AudioDriver> driver;
    std::string error;
    uint32_t state=ROCKER_STOPPED;
    std::atomic<uint64_t> intent{0};
    std::atomic<bool> stopRequested{false};
    // Only small copies hold telemetry; no driver call or graph mutation holds it.
    std::mutex telemetry;
    rocker_audio_snapshot audioCache{};
    rocker_transport_snapshot transportCache{};
    std::string errorCache;
    std::unique_ptr<rocker::ControlExecutor> executor;
    void stopOnControl(){state=ROCKER_STOPPING;publish();tone.enable(false);driver->stop();state=ROCKER_STOPPED;}
    void publish(){
        const auto device=driver->snapshot();
        const rocker_audio_snapshot audio={sizeof(audio),ROCKER_AUDIO_ABI_VERSION,state,0,device.sampleRate,device.bufferFrames,device.outputChannels,tone.frames()};
        const rocker_transport_snapshot transport={sizeof(transport),ROCKER_AUDIO_ABI_VERSION,tone.hasClip()?1u:0u,state==ROCKER_RUNNING && tone.clipMode()?1u:0u,tone.ended()?1u:0u,0,tone.duration(),tone.position(),tone.speed(),tone.volume()};
        const std::lock_guard lock(telemetry);audioCache=audio;transportCache=transport;errorCache=error;
    }
};
template<typename Operation>
static rocker_audio_result withHost(rocker_audio_handle host,Operation operation) noexcept {
    if(!host)return ROCKER_INVALID_ARGUMENT;
    try{return host->executor->invoke([host,operation=std::move(operation)]() mutable{
        const auto result=operation(*host);refreshHost(*host);host->publish();return result;
    });}catch(...){return ROCKER_INTERNAL_ERROR;}
}

uint32_t ROCKER_CALL rocker_audio_get_abi_version() { return ROCKER_AUDIO_ABI_VERSION; }
rocker_audio_result ROCKER_CALL rocker_audio_create(const rocker_audio_config* config, rocker_audio_handle* output) {
    if (!output) return ROCKER_INVALID_ARGUMENT;
    *output = nullptr;
    if (!config) return ROCKER_INVALID_ARGUMENT;
    if (config->struct_size != sizeof(*config) || config->abi_version != ROCKER_AUDIO_ABI_VERSION)
        return ROCKER_ABI_MISMATCH;
    if (!std::isfinite(config->frequency_hz) || !std::isfinite(config->amplitude)
        || config->frequency_hz < 20 || config->frequency_hz > 2000
        || config->amplitude <= 0 || config->amplitude > 0.05) return ROCKER_INVALID_ARGUMENT;
    try { *output = new rocker_audio_host(*config); return ROCKER_OK; }
    catch (...) { return ROCKER_INTERNAL_ERROR; }
}
static rocker_audio_result startHost(rocker_audio_host& h, bool media,uint64_t intent) {
    if(intent!=h.intent.load())return ROCKER_OK;
    h.stopRequested.exchange(false);
    if (media && !h.tone.hasClip()) { h.error = "No prepared media is available."; return ROCKER_NOT_READY; }
    if (h.state == ROCKER_RUNNING && !h.driver->snapshot().failed && h.tone.clipMode() == media && (!media || !h.tone.ended())) return ROCKER_OK;
    h.state=ROCKER_STARTING;h.publish();h.driver->stop();
    h.error.clear();
    if (media) { h.tone.selectClip(); if (h.tone.ended()) h.tone.seek(0); }
    else h.tone.selectTone();
    h.tone.authorize(intent);h.tone.enable(true);
    try {
        if (h.driver->start(h.tone, h.error)) {
            if(intent!=h.intent.load()){h.stopOnControl();return ROCKER_OK;}
            h.state = ROCKER_RUNNING; return ROCKER_OK;
        }
    } catch (...) {
        h.error = "The audio driver could not start.";
        h.tone.enable(false); h.driver->stop(); h.state = ROCKER_FAULTED;
        return ROCKER_INTERNAL_ERROR;
    }
    h.tone.enable(false); h.driver->stop(); h.state = ROCKER_FAULTED;
    return ROCKER_DEVICE_ERROR;
}
rocker_audio_result ROCKER_CALL rocker_audio_reserve_start(rocker_audio_handle host,uint64_t* ticket){
    if(!host || !ticket)return ROCKER_INVALID_ARGUMENT;
    *ticket=host->intent.fetch_add(1)+1;return ROCKER_OK;
}
rocker_audio_result ROCKER_CALL rocker_audio_start_reserved(rocker_audio_handle host,uint64_t ticket,uint32_t media){
    if(!ticket || media>1)return ROCKER_INVALID_ARGUMENT;
    return withHost(host,[ticket,media](auto& h){return startHost(h,media!=0,ticket);});
}
rocker_audio_result ROCKER_CALL rocker_audio_start(rocker_audio_handle host){
    uint64_t ticket=0;const auto result=rocker_audio_reserve_start(host,&ticket);
    return result==ROCKER_OK?rocker_audio_start_reserved(host,ticket,0):result;
}
rocker_audio_result ROCKER_CALL rocker_audio_play(rocker_audio_handle host){
    uint64_t ticket=0;const auto result=rocker_audio_reserve_start(host,&ticket);
    return result==ROCKER_OK?rocker_audio_start_reserved(host,ticket,1):result;
}
static void refreshHost(rocker_audio_host& h) {
    if (h.state != ROCKER_RUNNING) return;
    if (h.driver->snapshot().failed) {
        h.tone.enable(false); h.driver->stop(); h.state = ROCKER_FAULTED;
        h.error = "Audio device stopped unexpectedly. Stop and start playback again.";
    } else if (h.tone.clipMode() && h.tone.ended()) {
        h.tone.enable(false); h.driver->stop(); h.state = ROCKER_STOPPED;
    }
}
rocker_audio_result ROCKER_CALL rocker_audio_request_stop(rocker_audio_handle host){
    if(!host)return ROCKER_INVALID_ARGUMENT;
    host->intent.fetch_add(1);host->tone.enable(false);host->stopRequested.store(true);return ROCKER_OK;
}
rocker_audio_result ROCKER_CALL rocker_audio_stop(rocker_audio_handle host){
    if(!host)return ROCKER_INVALID_ARGUMENT;
    rocker_audio_request_stop(host);
    return withHost(host,[](auto& h)->rocker_audio_result{h.stopRequested.exchange(false);h.stopOnControl();return ROCKER_OK;});
}
rocker_audio_result ROCKER_CALL rocker_audio_get_snapshot(rocker_audio_handle host,rocker_audio_snapshot* out){
    if(!host || !out)return ROCKER_INVALID_ARGUMENT;
    if(out->struct_size!=sizeof(*out) || out->abi_version!=ROCKER_AUDIO_ABI_VERSION)return ROCKER_ABI_MISMATCH;
    const std::lock_guard lock(host->telemetry);*out=host->audioCache;return ROCKER_OK;
}
rocker_audio_result ROCKER_CALL rocker_audio_seek(rocker_audio_handle host, double seconds) {
    if (!std::isfinite(seconds) || seconds < 0) return ROCKER_INVALID_ARGUMENT;
    return withHost(host, [seconds](auto& h) -> rocker_audio_result {
        if (!h.tone.hasClip()) return ROCKER_NOT_READY;
        if (seconds > h.tone.duration()) return ROCKER_INVALID_ARGUMENT;
        h.tone.enable(false); h.driver->stop(); h.state = ROCKER_STOPPED;
        h.tone.selectClip(); h.tone.seek(seconds); return ROCKER_OK;
    });
}
rocker_audio_result ROCKER_CALL rocker_audio_set_volume(rocker_audio_handle host, double volume) {
    if (!std::isfinite(volume) || volume < 0 || volume > 1) return ROCKER_INVALID_ARGUMENT;
    if(!host)return ROCKER_INVALID_ARGUMENT;
    host->tone.volume(volume);
    const std::lock_guard lock(host->telemetry);host->transportCache.volume=volume;return ROCKER_OK;
}
rocker_audio_result ROCKER_CALL rocker_audio_get_transport(rocker_audio_handle host, rocker_transport_snapshot* out) {
    if (!out) return ROCKER_INVALID_ARGUMENT;
    if (out->struct_size != sizeof(*out) || out->abi_version != ROCKER_AUDIO_ABI_VERSION) return ROCKER_ABI_MISMATCH;
    if(!host)return ROCKER_INVALID_ARGUMENT;
    const std::lock_guard lock(host->telemetry);*out=host->transportCache;return ROCKER_OK;
}
#if ROCKER_WITH_MEDIA
template <typename Decode> static rocker_audio_result prepareMedia(rocker_audio_handle host, double speed, Decode decode) noexcept {
    if (!host || !std::isfinite(speed) || speed < 0.25 || speed > 2) return ROCKER_INVALID_ARGUMENT;
    try {
        auto original = decode();
        auto clip = speed == 1 ? std::move(original) : rocker::media::stretch(original, speed);
        return withHost(host, [&](auto& h) -> rocker_audio_result {
            h.state=ROCKER_RECONFIGURING;h.publish();
            h.tone.enable(false); h.driver->stop(); h.state = ROCKER_STOPPED;
            h.tone.install(std::move(clip), speed); h.error.clear(); return ROCKER_OK;
        });
    } catch (const std::exception& error) {
        return withHost(host, [&](auto& h) -> rocker_audio_result { h.error = error.what(); return ROCKER_MEDIA_ERROR; });
    } catch (...) { return ROCKER_INTERNAL_ERROR; }
}
#endif
rocker_audio_result ROCKER_CALL rocker_audio_prepare_file(rocker_audio_handle host, const char* path, double speed) {
    if (!host || !path || !*path) return ROCKER_INVALID_ARGUMENT;
#if ROCKER_WITH_MEDIA
    // Bound the borrowed path before constructing any path/string allocation.
    size_t length = 0;
    while (length < 32768 && path[length]) ++length;
    if (length == 32768) return ROCKER_INVALID_ARGUMENT;
    return prepareMedia(host, speed, [&] { return rocker::media::decode(std::filesystem::path(std::u8string(reinterpret_cast<const char8_t*>(path), length))); });
#else
    (void)speed;
    return ROCKER_NOT_READY;
#endif
}
rocker_audio_result ROCKER_CALL rocker_audio_prepare_midi(rocker_audio_handle host, const uint8_t* data, uint32_t count, double speed) {
    if (!host || !data || !count) return ROCKER_INVALID_ARGUMENT;
#if ROCKER_WITH_MEDIA
    return prepareMedia(host, speed, [&] { return rocker::media::synthesizeMidi(data, count); });
#else
    (void)speed;
    return ROCKER_NOT_READY;
#endif
}
static bool deviceText(const char* value,std::string& copied) {
    if(!value)return false;
    size_t length=0;while(length<1024 && value[length])++length;
    if(length==1024)return false;
    copied.assign(value,length);return true;
}
static rocker_audio_result copyJson(const std::string& value,char* buffer,uint32_t capacity,uint32_t* required) {
    if(value.size()>1024*1024)return ROCKER_DEVICE_ERROR;
    *required=static_cast<uint32_t>(value.size()+1);if(capacity==0)return ROCKER_OK;
    const auto count=std::min(value.size(),static_cast<size_t>(capacity-1));std::memcpy(buffer,value.data(),count);buffer[count]='\0';
    return capacity<*required?ROCKER_BUFFER_TOO_SMALL:ROCKER_OK;
}
template<class Query> static rocker_audio_result queryDevice(rocker_audio_handle host,char* buffer,uint32_t capacity,uint32_t* required,Query query) {
    if(!required || (capacity && !buffer))return ROCKER_INVALID_ARGUMENT;
    return withHost(host,[&](auto& h)->rocker_audio_result{
        if(h.state==ROCKER_RUNNING)return ROCKER_NOT_READY;
        try{return copyJson(query(*h.driver),buffer,capacity,required);}catch(const std::exception& e){h.error=e.what();return ROCKER_DEVICE_ERROR;}
    });
}
rocker_audio_result ROCKER_CALL rocker_audio_copy_devices(rocker_audio_handle host,char* buffer,uint32_t capacity,uint32_t* required){
    return queryDevice(host,buffer,capacity,required,[](auto& driver){return driver.catalog();});
}
rocker_audio_result ROCKER_CALL rocker_audio_copy_capabilities(rocker_audio_handle host,const char* backend,const char* input,const char* output,char* buffer,uint32_t capacity,uint32_t* required){
    rocker::DeviceSelection choice;if(!deviceText(backend,choice.backend)||!deviceText(input,choice.input)||!deviceText(output,choice.output))return ROCKER_INVALID_ARGUMENT;
    return queryDevice(host,buffer,capacity,required,[&](auto& driver){return driver.capabilities(choice);});
}
rocker_audio_result ROCKER_CALL rocker_audio_copy_device_details(rocker_audio_handle host,char* buffer,uint32_t capacity,uint32_t* required){
    if(!required || (capacity && !buffer))return ROCKER_INVALID_ARGUMENT;
    return withHost(host,[&](auto& h)->rocker_audio_result{try{return copyJson(h.driver->details(),buffer,capacity,required);}catch(const std::exception& e){h.error=e.what();return ROCKER_DEVICE_ERROR;}});
}
rocker_audio_result ROCKER_CALL rocker_audio_configure_device(rocker_audio_handle host,const rocker_device_config* config,const char* backend,const char* input,const char* output){
    if(!config)return ROCKER_INVALID_ARGUMENT;
    if(config->struct_size!=sizeof(*config)||config->abi_version!=ROCKER_AUDIO_ABI_VERSION)return ROCKER_ABI_MISMATCH;
    if(!std::isfinite(config->sample_rate) || (config->sample_rate!=0 && (config->sample_rate<8000 || config->sample_rate>192000)) || (config->buffer_frames!=0 && (config->buffer_frames<16 || config->buffer_frames>8192)) || config->input_channel < -1 || config->input_channel>63 || config->output_first>62 || config->reserved!=0)return ROCKER_INVALID_ARGUMENT;
    rocker::DeviceSelection choice;if(!deviceText(backend,choice.backend)||choice.backend.empty()||!deviceText(input,choice.input)||!deviceText(output,choice.output)||choice.output.empty())return ROCKER_INVALID_ARGUMENT;
    choice.rate=config->sample_rate;choice.frames=config->buffer_frames;choice.inputChannel=config->input_channel;choice.outputFirst=config->output_first;
    rocker_audio_request_stop(host);
    return withHost(host,[&](auto& h)->rocker_audio_result{h.stopRequested.exchange(false);h.stopOnControl();h.state=ROCKER_RECONFIGURING;h.publish();
        const bool ok=h.driver->configure(choice,h.error);h.state=ROCKER_STOPPED;if(ok)h.error.clear();return ok?ROCKER_OK:ROCKER_DEVICE_ERROR;});
}
rocker_audio_result ROCKER_CALL rocker_audio_show_control_panel(rocker_audio_handle host){
    return withHost(host,[](auto& h)->rocker_audio_result{if(h.state==ROCKER_RUNNING)return ROCKER_NOT_READY;return h.driver->controlPanel(h.error)?ROCKER_OK:ROCKER_DEVICE_ERROR;});
}
rocker_audio_result ROCKER_CALL rocker_audio_copy_last_error(rocker_audio_handle host, char* buffer, uint32_t capacity, uint32_t* required) {
    if (!required || (capacity > 0 && !buffer)) return ROCKER_INVALID_ARGUMENT;
    if(!host)return ROCKER_INVALID_ARGUMENT;
    const std::lock_guard lock(host->telemetry);
    *required=static_cast<uint32_t>(host->errorCache.size()+1);
    if(capacity==0)return ROCKER_OK;
    const auto count=std::min(static_cast<size_t>(capacity-1),host->errorCache.size());
    std::memcpy(buffer,host->errorCache.data(),count);buffer[count]='\0';
    return capacity<*required?ROCKER_BUFFER_TOO_SMALL:ROCKER_OK;
}
void ROCKER_CALL rocker_audio_destroy(rocker_audio_handle host) {
    try { delete host; } catch (...) { /* Never unwind through the C ABI. */ }
}
