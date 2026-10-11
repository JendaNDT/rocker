// SPDX-License-Identifier: AGPL-3.0-only
#include "rocker_audio.h"
#include "audio_driver.h"
#include "control_executor.h"
#include "analysis_worker.h"
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
static_assert(sizeof(rocker_meter_snapshot)==112);
static_assert(sizeof(rocker_clock_snapshot)==72);
static_assert(sizeof(rocker_amp_config)==96);
static_assert(sizeof(rocker_metronome_config)==240);
static_assert(sizeof(rocker_metronome_snapshot)==80);
static_assert(sizeof(rocker_analysis_snapshot)==144);
static_assert(sizeof(rocker_diagnostic_snapshot)==160);
static rocker_metronome_config defaultMetronome(){
    rocker_metronome_config value{sizeof(value),ROCKER_AUDIO_ABI_VERSION,0,100,4,1,0,5,0,0,4,5,160,0,4,1,0,3,4,0,0.8,1,0.5,0.6,{}, {}};
    for(auto& accent:value.accents)accent=1;
    value.accents[0]=2;for(auto& sound:value.sounds)sound=5;return value;
}
struct rocker_audio_host;
static void refreshHost(rocker_audio_host&);
struct rocker_audio_host {
    explicit rocker_audio_host(const rocker_audio_config& config)
        : tone(config.frequency_hz, config.amplitude), driver(rocker::makeAudioDriver()) {
        tone.authority(intent);errorCache.reserve(65536);analyzer=std::make_unique<rocker::AnalysisWorker>(tone.input().analysis());publish();
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
    std::atomic<uint64_t> intent{0},referenceIntent{0};
    std::atomic<uint64_t> stopRequests{0};
    uint64_t deviceStarts=0,deviceFaults=0,mediaErrors=0,configurationFailures=0;
    int64_t driverXruns=-1;uint32_t lastFailureReason=0;
    rocker_diagnostic_snapshot diagnosticCache{};
    std::atomic<bool> stopRequested{false};
    // Only small copies hold telemetry; no driver call or graph mutation holds it.
    std::mutex telemetry;
    rocker_audio_snapshot audioCache{};
    rocker_transport_snapshot transportCache{};
    rocker_meter_snapshot meterCache{};
    rocker_clock_snapshot clockCache{};
    rocker_amp_config ampCache{sizeof(ampCache),ROCKER_AUDIO_ABI_VERSION,0,1,0,0,0,0.1,0.1,0,0.15,0.1,0,0,0.6};
    rocker_metronome_config metroCache=defaultMetronome();
    rocker_metronome_snapshot metroSnapshotCache{};
    rocker_analysis_snapshot analysisCache{};
    std::unique_ptr<rocker::AnalysisWorker> analyzer;
    std::string errorCache;
    std::unique_ptr<rocker::ControlExecutor> executor;
    void disarmSourcesStopped(){driverXruns=-1;tone.input().monitoring(false,0.15);tone.input().metronome().disableStopped();tone.input().reference().stopJoined();const std::lock_guard lock(telemetry);metroCache.enabled=0;}
    void stopOnControl(){state=ROCKER_STOPPING;publish();tone.enable(false);driver->stop();driverXruns=-1;disarmSourcesStopped();state=ROCKER_STOPPED;}
    void publish(){
        const auto device=driver->snapshot();
        const rocker_audio_snapshot audio={sizeof(audio),ROCKER_AUDIO_ABI_VERSION,state,tone.mode(),device.sampleRate,device.bufferFrames,device.outputChannels,tone.frames()};
        const rocker_transport_snapshot transport={sizeof(transport),ROCKER_AUDIO_ABI_VERSION,tone.hasClip()?1u:0u,state==ROCKER_RUNNING && tone.clipMode() && !tone.ended()?1u:0u,tone.ended()?1u:0u,tone.loopEnabled()?1u:0u,tone.duration(),tone.position(),tone.speed(),tone.volume()};
        const auto levels=tone.input().meters();const bool active=state==ROCKER_RUNNING;
        const rocker_meter_snapshot meter={sizeof(meter),ROCKER_AUDIO_ABI_VERSION,(tone.input().monitoring()?1u:0u)|(active && tone.input().hasInput()?2u:0u),levels.epoch,levels.frame,device.sampleRate,
            active?levels.inputPeak:0,active?levels.inputRms:0,active?levels.processedPeak:0,active?levels.processedRms:0,active?levels.mixPeak:0,active?levels.mixRms:0,levels.invalid,levels.inputClips,levels.outputClips,levels.dropped};
        const auto clock=tone.input().clock().observation();
        const rocker_clock_snapshot observedClock={sizeof(observedClock),ROCKER_AUDIO_ABI_VERSION,(active?1u:0u)|(tone.loopEnabled()?2u:0u),clock.epoch,clock.frame,clock.beat,clock.rate,clock.phase,clock.bpm,tone.input().amp().latency(),0,tone.loopCycles()};
        const auto metro=tone.input().metronome().observation();
        const rocker_metronome_snapshot observedMetro={sizeof(observedMetro),ROCKER_AUDIO_ABI_VERSION,metro.flags|(active?8u:0u),metro.beat,metro.frame,metro.bar,clock.bpm,clock.phase,metro.subTick,metro.polyTick,metro.voiceOverloads,metro.primaryEvents,metro.secondaryEvents};
        const auto report=analyzer->report();const auto reading=report.observed;
        const double frameAge=reading.rate>0 && reading.epoch==clock.epoch && clock.frame>=reading.frame?(clock.frame-reading.frame)/reading.rate:1;
        const double age=std::max(report.ageSeconds,frameAge);const bool inputActive=active && tone.input().hasInput() && reading.epoch==clock.epoch;const bool fresh=inputActive && age<=0.25;
        const rocker_analysis_snapshot analysis={sizeof(analysis),ROCKER_AUDIO_ABI_VERSION,(fresh?reading.flags:0u)|(inputActive?4u:0u)|(inputActive?report.calibrationFlags:report.calibrationFlags&8u?32u:report.calibrationFlags)|(age>0.25?64u:0u)|(tone.input().reference().playing()?128u:0u)|(tone.input().reference().chimePlaying()?256u:0u),reading.epoch,reading.frame,reading.sequence,reading.hitCount,reading.onsetFrame,report.calibrationToken,reading.rate,fresh?reading.rms:0,fresh?reading.peak:0,fresh?reading.frequency:0,fresh?reading.clarity:0,fresh?reading.centroid:0,reading.hitCentroid,reading.windowSeconds,std::isfinite(age)?age:1,report.calibrationProgress,report.recommendedGate};
        const auto perf=tone.callbacks().observation();
        const rocker_diagnostic_snapshot diagnostic={sizeof(diagnostic),ROCKER_AUDIO_ABI_VERSION,clock.epoch,(active?1u:0u)|(driverXruns>=0?2u:0u),perf.callbacks,perf.frames,perf.totalNanoseconds,perf.lastNanoseconds,perf.maxNanoseconds,perf.overBudget,levels.dropped,tone.input().analysis().highWatermark(),deviceFaults,deviceStarts,stopRequests.load(),mediaErrors,perf.invalidCallbacks,driverXruns,perf.rate,perf.minFrames,perf.maxFrames,lastFailureReason,tone.mode(),configurationFailures};
        const std::lock_guard lock(telemetry);diagnosticCache=diagnostic;analysisCache=analysis;metroSnapshotCache=observedMetro;clockCache=observedClock;audioCache=audio;transportCache=transport;meterCache=meter;errorCache=error;
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
static rocker_audio_result startHost(rocker_audio_host& h, uint32_t mode,uint64_t intent) {
    if(intent!=h.intent.load())return ROCKER_OK;
    h.stopRequested.exchange(false);
    if(mode==4 && !h.tone.input().reference().wanted())return ROCKER_NOT_READY;
    if(mode==3 && !h.metroCache.enabled)return ROCKER_NOT_READY;
    if((mode==3 || mode==4) && h.state==ROCKER_RUNNING && !h.driver->snapshot().failed){h.tone.authorize(intent);return ROCKER_OK;}
    if (mode==1 && !h.tone.hasClip()) { h.error = "No prepared media is available."; return ROCKER_NOT_READY; }
    if (h.state == ROCKER_RUNNING && !h.driver->snapshot().failed && h.tone.mode() == mode && (mode!=1 || !h.tone.ended())) return ROCKER_OK;
    h.state=ROCKER_STARTING;h.driverXruns=-1;h.publish();h.driver->stop();
    h.error.clear();
    if (mode==1) { h.tone.selectClip(); if (h.tone.ended()) h.tone.seek(0); }
    else if(mode==2)h.tone.selectLive();
    else if(mode==3)h.tone.selectMetronome();
    else if(mode==4)h.tone.selectReference();
    else h.tone.selectTone();
    h.tone.authorize(intent);h.tone.enable(true);
    try {
        if (h.driver->start(h.tone, h.error)) {
            ++h.deviceStarts;
            if(intent!=h.intent.load()){h.stopOnControl();return ROCKER_OK;}
            h.state = ROCKER_RUNNING; return ROCKER_OK;
        }
    } catch (...) {
        h.error = "The audio driver could not start.";
        ++h.deviceFaults;h.lastFailureReason=h.driver->snapshot().failureReason?h.driver->snapshot().failureReason:5;h.tone.enable(false); h.driver->stop(); h.disarmSourcesStopped();h.state = ROCKER_FAULTED;
        return ROCKER_INTERNAL_ERROR;
    }
    ++h.deviceFaults;h.lastFailureReason=h.driver->snapshot().failureReason?h.driver->snapshot().failureReason:5;h.tone.enable(false); h.driver->stop(); h.disarmSourcesStopped();h.state = ROCKER_FAULTED;
    return ROCKER_DEVICE_ERROR;
}
rocker_audio_result ROCKER_CALL rocker_audio_reserve_start(rocker_audio_handle host,uint64_t* ticket){
    if(!host || !ticket)return ROCKER_INVALID_ARGUMENT;
    *ticket=host->intent.fetch_add(1)+1;return ROCKER_OK;
}
rocker_audio_result ROCKER_CALL rocker_audio_reserve_metronome_start(rocker_audio_handle host,uint64_t* ticket){
    if(!host || !ticket)return ROCKER_INVALID_ARGUMENT;
    const std::lock_guard lock(host->telemetry);
    *ticket=host->audioCache.state==ROCKER_RUNNING && !host->stopRequested.load()?host->intent.load():host->intent.fetch_add(1)+1;return ROCKER_OK;
}
rocker_audio_result ROCKER_CALL rocker_audio_start_reserved(rocker_audio_handle host,uint64_t ticket,uint32_t mode){
    if(!ticket || mode>4)return ROCKER_INVALID_ARGUMENT;
    return withHost(host,[ticket,mode](auto& h){return startHost(h,mode,ticket);});
}
rocker_audio_result ROCKER_CALL rocker_audio_start(rocker_audio_handle host){
    uint64_t ticket=0;const auto result=rocker_audio_reserve_start(host,&ticket);
    return result==ROCKER_OK?rocker_audio_start_reserved(host,ticket,0):result;
}
rocker_audio_result ROCKER_CALL rocker_audio_start_live(rocker_audio_handle host){
    uint64_t ticket=0;const auto result=rocker_audio_reserve_start(host,&ticket);
    return result==ROCKER_OK?rocker_audio_start_reserved(host,ticket,2):result;
}
rocker_audio_result ROCKER_CALL rocker_audio_set_monitoring(rocker_audio_handle host,uint32_t enabled,double gain){
    if(!host || enabled>1 || !std::isfinite(gain) || gain<0 || gain>1)return ROCKER_INVALID_ARGUMENT;
    host->tone.input().monitoring(enabled!=0,gain);const std::lock_guard lock(host->telemetry);host->meterCache.flags=(host->meterCache.flags&~1u)|enabled;return ROCKER_OK;
}
rocker_audio_result ROCKER_CALL rocker_audio_get_meters(rocker_audio_handle host,rocker_meter_snapshot* value){
    if(!host || !value)return ROCKER_INVALID_ARGUMENT;
    if(value->struct_size!=sizeof(*value) || value->abi_version!=ROCKER_AUDIO_ABI_VERSION)return ROCKER_ABI_MISMATCH;
    const std::lock_guard lock(host->telemetry);*value=host->meterCache;return ROCKER_OK;
}
rocker_audio_result ROCKER_CALL rocker_audio_get_clock(rocker_audio_handle host,rocker_clock_snapshot* value){
    if(!host || !value)return ROCKER_INVALID_ARGUMENT;
    if(value->struct_size!=sizeof(*value)||value->abi_version!=ROCKER_AUDIO_ABI_VERSION)return ROCKER_ABI_MISMATCH;
    const std::lock_guard lock(host->telemetry);*value=host->clockCache;return ROCKER_OK;
}
rocker_audio_result ROCKER_CALL rocker_audio_set_tempo(rocker_audio_handle host,double bpm){
    if(!host || !std::isfinite(bpm) || bpm<40 || bpm>240)return ROCKER_INVALID_ARGUMENT;
    host->tone.input().clock().tempo(bpm);return ROCKER_OK;
}
rocker_audio_result ROCKER_CALL rocker_audio_set_loop(rocker_audio_handle host,double start,double end,uint32_t enabled){
    if(!host || enabled>1 || !std::isfinite(start) || !std::isfinite(end))return ROCKER_INVALID_ARGUMENT;
    rocker_audio_request_stop(host);
    return withHost(host,[=](auto& h)->rocker_audio_result{h.stopRequested.exchange(false);h.stopOnControl();
        return h.tone.loop(start,end,enabled!=0)?ROCKER_OK:ROCKER_INVALID_ARGUMENT;});
}
rocker_audio_result ROCKER_CALL rocker_audio_set_amp(rocker_audio_handle host,const rocker_amp_config* value){
    if(!host || !value)return ROCKER_INVALID_ARGUMENT;
    if(value->struct_size!=sizeof(*value) || value->abi_version!=ROCKER_AUDIO_ABI_VERSION)return ROCKER_ABI_MISMATCH;
    if(value->enabled>1 || value->distortion>2 || value->cabinet>2 || value->reserved!=0)return ROCKER_INVALID_ARGUMENT;
    const std::array<double,9> values{value->compression,value->drive,value->bass,value->mid,value->treble,value->delay,value->reverb,value->chorus,value->level};
    for(size_t i=0;i<values.size();++i)if(!std::isfinite(values[i]) || values[i]>(1.0) || values[i]<(i>=2 && i<=4?-1.0:0.0))return ROCKER_INVALID_ARGUMENT;
    const auto owned=*value;
    return withHost(host,[owned,values](auto& h)->rocker_audio_result{
        rocker::AmpParameters params{owned.enabled!=0,owned.distortion,owned.cabinet,values};
        if(!h.tone.input().amp().configure(params,h.state!=ROCKER_RUNNING)){h.error="Amplifier parameter queue is full. Retry the latest setting.";return ROCKER_NOT_READY;}
        const std::lock_guard lock(h.telemetry);h.ampCache=owned;return ROCKER_OK;
    });
}
rocker_audio_result ROCKER_CALL rocker_audio_get_amp(rocker_audio_handle host,rocker_amp_config* value){
    if(!host || !value)return ROCKER_INVALID_ARGUMENT;
    if(value->struct_size!=sizeof(*value) || value->abi_version!=ROCKER_AUDIO_ABI_VERSION)return ROCKER_ABI_MISMATCH;
    const std::lock_guard lock(host->telemetry);*value=host->ampCache;return ROCKER_OK;
}
rocker_audio_result ROCKER_CALL rocker_audio_set_metronome(rocker_audio_handle host,const rocker_metronome_config* value){
    if(!host || !value)return ROCKER_INVALID_ARGUMENT;
    if(value->struct_size!=sizeof(*value) || value->abi_version!=ROCKER_AUDIO_ABI_VERSION)return ROCKER_ABI_MISMATCH;
    if(value->enabled>1 || value->bpm<40 || value->bpm>240 || value->beats<1 || value->beats>16 || value->subdivision<1 || value->subdivision>4 || value->sound>4 || value->sub_sound>5 || value->count_in>2 || value->ramp_enabled>1 || value->ramp_every<1 || value->ramp_every>16 || value->ramp_step<1 || value->ramp_step>20 || value->ramp_target<40 || value->ramp_target>240 || value->gap_enabled>1 || value->audible_bars<1 || value->audible_bars>16 || value->silent_bars<1 || value->silent_bars>16 || value->poly_enabled>1 || value->poly_ticks<2 || value->poly_ticks>7 || value->poly_sound>4 || value->reserved)return ROCKER_INVALID_ARGUMENT;
    for(double number:{value->volume,value->accent_volume,value->poly_volume})if(!std::isfinite(number)||number<0||number>1)return ROCKER_INVALID_ARGUMENT;
    if(!std::isfinite(value->swing)||value->swing<0.5||value->swing>0.75)return ROCKER_INVALID_ARGUMENT;
    for(unsigned i=0;i<16;++i)if(value->accents[i]>2 || value->sounds[i]>5)return ROCKER_INVALID_ARGUMENT;
    const auto owned=*value;
    return withHost(host,[owned](auto& h)->rocker_audio_result{
        rocker::MetronomeParameters params;params.enabled=owned.enabled!=0;params.bpm=owned.bpm;params.beats=owned.beats;params.subdivision=owned.subdivision;params.sound=owned.sound;params.subSound=owned.sub_sound;params.countInBars=owned.count_in;
        params.ramp=owned.ramp_enabled!=0;params.rampEvery=owned.ramp_every;params.rampStep=owned.ramp_step;params.rampTarget=owned.ramp_target;params.gap=owned.gap_enabled!=0;params.audibleBars=owned.audible_bars;params.silentBars=owned.silent_bars;params.poly=owned.poly_enabled!=0;params.polyTicks=owned.poly_ticks;params.polySound=owned.poly_sound;
        params.volume=owned.volume;params.accentVolume=owned.accent_volume;params.swing=owned.swing;params.polyVolume=owned.poly_volume;std::copy_n(owned.accents,16,params.accents.begin());std::copy_n(owned.sounds,16,params.sounds.begin());
        if(!h.tone.input().metronome().configure(params,h.state!=ROCKER_RUNNING)){h.error="Metronome parameter queue is full. Retry the latest setting.";return ROCKER_NOT_READY;}
        h.tone.input().clock().tempo(owned.bpm);
        {const std::lock_guard lock(h.telemetry);h.metroCache=owned;}
        if(!params.enabled && h.state==ROCKER_RUNNING && h.tone.mode()==3 && !h.tone.input().monitoring() && !h.tone.input().reference().wanted() && !h.tone.input().reference().playing() && !h.tone.input().reference().chimePlaying())h.stopOnControl();
        return ROCKER_OK;
    });
}
rocker_audio_result ROCKER_CALL rocker_audio_get_metronome(rocker_audio_handle host,rocker_metronome_config* value){
    if(!host || !value)return ROCKER_INVALID_ARGUMENT;
    if(value->struct_size!=sizeof(*value)||value->abi_version!=ROCKER_AUDIO_ABI_VERSION)return ROCKER_ABI_MISMATCH;
    const std::lock_guard lock(host->telemetry);*value=host->metroCache;return ROCKER_OK;
}
rocker_audio_result ROCKER_CALL rocker_audio_get_metronome_snapshot(rocker_audio_handle host,rocker_metronome_snapshot* value){
    if(!host || !value)return ROCKER_INVALID_ARGUMENT;
    if(value->struct_size!=sizeof(*value)||value->abi_version!=ROCKER_AUDIO_ABI_VERSION)return ROCKER_ABI_MISMATCH;
    const std::lock_guard lock(host->telemetry);*value=host->metroSnapshotCache;return ROCKER_OK;
}
rocker_audio_result ROCKER_CALL rocker_audio_get_analysis(rocker_audio_handle host,rocker_analysis_snapshot* value){
    if(!host || !value)return ROCKER_INVALID_ARGUMENT;
    if(value->struct_size!=sizeof(*value)||value->abi_version!=ROCKER_AUDIO_ABI_VERSION)return ROCKER_ABI_MISMATCH;
    const std::lock_guard lock(host->telemetry);*value=host->analysisCache;return ROCKER_OK;
}
rocker_audio_result ROCKER_CALL rocker_audio_set_noise_gate(rocker_audio_handle host,double value){
    if(!host || !std::isfinite(value) || value<0 || value>0.5)return ROCKER_INVALID_ARGUMENT;
    host->analyzer->gate(value);return ROCKER_OK;
}
rocker_audio_result ROCKER_CALL rocker_audio_calibrate_noise(rocker_audio_handle host,uint64_t* token){
    if(!host || !token)return ROCKER_INVALID_ARGUMENT;
    const std::lock_guard lock(host->telemetry);if(host->stopRequested.load() || host->audioCache.state!=ROCKER_RUNNING || !(host->meterCache.flags&2))return ROCKER_NOT_READY;
    *token=host->analyzer->calibrate();return ROCKER_OK;
}
rocker_audio_result ROCKER_CALL rocker_audio_reserve_reference(rocker_audio_handle host,uint32_t kind,uint64_t* graphTicket,uint64_t* referenceTicket){
    if(!host || kind>2 || !graphTicket || !referenceTicket)return ROCKER_INVALID_ARGUMENT;
    if(kind==1)*graphTicket=0;else {const auto result=rocker_audio_reserve_metronome_start(host,graphTicket);if(result!=ROCKER_OK)return result;}
    *referenceTicket=kind==2?0:host->referenceIntent.fetch_add(1)+1;return ROCKER_OK;
}
rocker_audio_result ROCKER_CALL rocker_audio_reference(rocker_audio_handle host,uint64_t ticket,uint64_t referenceTicket,uint32_t enabled,double frequency,double volume,uint32_t confirmation){
    if(!host || enabled>1 || confirmation>1 || !std::isfinite(frequency) || frequency<20 || frequency>5000 || !std::isfinite(volume) || volume<0 || volume>1 || ((enabled || confirmation) && !ticket) || (!confirmation && !referenceTicket))return ROCKER_INVALID_ARGUMENT;
    return withHost(host,[=](auto& h)->rocker_audio_result{
        if((ticket && ticket!=h.intent.load()) || (referenceTicket && referenceTicket!=h.referenceIntent.load()))return ROCKER_OK;
        if(!h.tone.input().reference().command({confirmation?2u:enabled?1u:0u,frequency,volume},h.state!=ROCKER_RUNNING))return ROCKER_NOT_READY;
        return enabled || confirmation?startHost(h,4,ticket):static_cast<rocker_audio_result>(ROCKER_OK);
    });
}
rocker_audio_result ROCKER_CALL rocker_audio_get_diagnostics(rocker_audio_handle host,rocker_diagnostic_snapshot* value){
    if(!host || !value)return ROCKER_INVALID_ARGUMENT;
    if(value->struct_size!=sizeof(*value)||value->abi_version!=ROCKER_AUDIO_ABI_VERSION)return ROCKER_ABI_MISMATCH;
    const std::lock_guard lock(host->telemetry);*value=host->diagnosticCache;return ROCKER_OK;
}
rocker_audio_result ROCKER_CALL rocker_audio_refresh_diagnostics(rocker_audio_handle host){
    return withHost(host,[](auto& h)->rocker_audio_result{h.driverXruns=h.state==ROCKER_RUNNING?h.driver->xrunCount():-1;return ROCKER_OK;});
}
rocker_audio_result ROCKER_CALL rocker_audio_play(rocker_audio_handle host){
    uint64_t ticket=0;const auto result=rocker_audio_reserve_start(host,&ticket);
    return result==ROCKER_OK?rocker_audio_start_reserved(host,ticket,1):result;
}
static void refreshHost(rocker_audio_host& h) {
    if (h.state != ROCKER_RUNNING) return;
    if (h.driver->snapshot().failed) {
        ++h.deviceFaults;h.lastFailureReason=h.driver->snapshot().failureReason;h.driverXruns=-1;
        h.tone.enable(false); h.driver->stop();h.disarmSourcesStopped(); h.state = ROCKER_FAULTED;
        h.error = "Audio device stopped unexpectedly. Stop and start playback again.";
    } else if (h.tone.clipMode() && h.tone.ended() && !h.metroCache.enabled && !h.tone.input().monitoring() && !h.tone.input().reference().wanted() && !h.tone.input().reference().playing() && !h.tone.input().reference().chimePlaying()) {
        h.tone.enable(false); h.driver->stop();h.driverXruns=-1; h.state = ROCKER_STOPPED;
    } else if((h.tone.mode()==3 || h.tone.mode()==4) && !h.metroCache.enabled && !h.tone.input().monitoring() && !h.tone.input().reference().wanted() && !h.tone.input().reference().playing() && !h.tone.input().reference().chimePlaying())h.stopOnControl();
}
rocker_audio_result ROCKER_CALL rocker_audio_reserve_stop(rocker_audio_handle host,uint64_t* ticket){
    if(!host || !ticket)return ROCKER_INVALID_ARGUMENT;
    // The callback authority is the immediate gate. An old control request never
    // writes enable(false) after a newer start has already enabled its ticket.
    *ticket=host->intent.fetch_add(1)+1;host->stopRequests.fetch_add(1);host->referenceIntent.fetch_add(1);host->tone.input().monitoring(false,0.15);host->stopRequested.store(true);return ROCKER_OK;
}
rocker_audio_result ROCKER_CALL rocker_audio_request_stop(rocker_audio_handle host){uint64_t ticket=0;return rocker_audio_reserve_stop(host,&ticket);}
rocker_audio_result ROCKER_CALL rocker_audio_stop_reserved(rocker_audio_handle host,uint64_t ticket){
    if(!ticket)return ROCKER_INVALID_ARGUMENT;
    return withHost(host,[ticket](auto& h)->rocker_audio_result{
        if(ticket!=h.intent.load())return ROCKER_OK;
        h.stopRequested.exchange(false);h.stopOnControl();return ROCKER_OK;
    });
}
rocker_audio_result ROCKER_CALL rocker_audio_stop(rocker_audio_handle host){
    uint64_t ticket=0;const auto result=rocker_audio_reserve_stop(host,&ticket);
    return result==ROCKER_OK?rocker_audio_stop_reserved(host,ticket):result;
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
        h.tone.enable(false); h.driver->stop();h.disarmSourcesStopped(); h.state = ROCKER_STOPPED;
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
            h.tone.enable(false); h.driver->stop();h.disarmSourcesStopped(); h.state = ROCKER_STOPPED;
            h.tone.install(std::move(clip), speed); h.error.clear(); return ROCKER_OK;
        });
    } catch (const std::exception& error) {
        return withHost(host, [&](auto& h) -> rocker_audio_result { ++h.mediaErrors;h.error = error.what(); return ROCKER_MEDIA_ERROR; });
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
        const bool ok=h.driver->configure(choice,h.error);if(!ok)++h.configurationFailures;h.state=ROCKER_STOPPED;if(ok)h.error.clear();return ok?ROCKER_OK:ROCKER_DEVICE_ERROR;});
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
