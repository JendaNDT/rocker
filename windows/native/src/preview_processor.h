// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include "recording_tap.h"
#include <numbers>
namespace rocker {
struct PreviewNote {double start=0,duration=0,frequency=440,volume=1;};
struct PreviewPattern {uint64_t token=0;uint32_t count=0,repeat=0,countInBeats=0;double length=0;bool musical=false;std::array<PreviewNote,128> notes{};};
// Small synthetic teaching phrases share the input graph. Event storage and
// cancellation are bounded; no decoder, file, allocation or UI timing is used.
class PreviewProcessor {
public:
    void authority(const std::atomic<uint64_t>& value) noexcept{authority_=&value;}
    void expect(uint64_t token) noexcept{desired_.store(token);}
    bool installCallback(const PreviewPattern& value) noexcept{if(value.token!=desired_.load() || (authority_ && value.token!=authority_->load()))return false;install(value);return true;}
    void prepare(double rate) noexcept{rate_=rate;frame_=0;gain_=last_=0;}
    bool arm(const PreviewPattern& pattern) noexcept{desired_.store(pattern.token);if(commands_.push(pattern))return true;desired_.store(0);return false;}
    void cancel() noexcept{desired_.store(0);}
    void stopJoined() noexcept{PreviewPattern old;while(commands_.pop(old)){}desired_.store(0);pattern_={};gain_=last_=0;active_.store(false);}
    bool active() const noexcept{return active_.load() || (desired_.load()!=0 && (!authority_ || desired_.load()==authority_->load()));}
    void beginBlock() noexcept{PreviewPattern next;while(commands_.pop(next))installCallback(next);}
    double process(const GridTick& tick) noexcept{
        if(!pattern_.token)return 0;
        if(waiting_ && (desired_.load()!=pattern_.token || (authority_ && pattern_.token!=authority_->load())))waiting_=false;
        if(waiting_){if(startBeat_==UINT64_MAX && tick.quarter)startBeat_=tick.beat+pattern_.countInBeats;if(!tick.quarter || tick.beat<startBeat_)return 0;waiting_=false;}
        if(pattern_.musical && musicalStart_==UINT64_MAX)musicalStart_=tick.beat;
        const auto total=static_cast<uint64_t>(std::ceil(pattern_.length*rate_));
        const bool wanted=desired_.load()==pattern_.token && (!authority_ || pattern_.token==authority_->load()) && total>0 && (pattern_.repeat || frame_<total);
        gain_+=std::clamp((wanted?1.:0.)-gain_,-1/(rate_*.01),1/(rate_*.01));
        double seconds=(pattern_.repeat && total?frame_%total:frame_)/rate_;
        if(pattern_.musical){const double beat=std::max(0.,tick.beat+tick.phase-static_cast<double>(musicalStart_));const auto cycle=static_cast<uint64_t>(std::floor((beat+1e-10)/pattern_.length));if(cycle!=musicalCycle_){musicalCycle_=cycle;onsets_.fill(UINT64_MAX);}seconds=std::max(0.,beat-cycle*pattern_.length);}
        double sample=0;int voices=0;
        for(uint32_t i=0;i<pattern_.count;++i){const auto& note=pattern_.notes[i];const double unitTime=seconds-note.start;if(unitTime<0 || unitTime>=note.duration)continue;
            if(pattern_.musical && onsets_[i]==UINT64_MAX)onsets_[i]=tick.frame;const double time=pattern_.musical?(tick.frame-onsets_[i])/rate_:unitTime;
            const double remaining=pattern_.musical?(note.duration-unitTime)*60/currentBpm_:note.duration-time;
            const double fade=std::min(.01,note.duration*.25),envelope=std::min({1.,time/std::min(.002,note.duration*.25),remaining/fade})*std::exp(-2*time);
            const double phase=2*std::numbers::pi*note.frequency*time;
            sample+=note.volume*envelope*(std::sin(phase)+.25*std::sin(2*phase)+.1*std::sin(3*phase))/1.35;++voices;
        }
        sample*=gain_/std::max(1.,std::sqrt(static_cast<double>(voices)));++frame_;
        if(transitionFrames_){const double blend=transitionFrames_/(rate_*.01);sample=sample*(1-std::min(1.,blend))+transition_*std::min(1.,blend);--transitionFrames_;}
        last_=std::clamp(sample,-1.,1.);
        if(!wanted && gain_<=1e-12){auto expected=pattern_.token;desired_.compare_exchange_strong(expected,0);pattern_.token=0;active_.store(false);last_=0;}
        return last_;
    }
    void tempo(double bpm) noexcept{currentBpm_=bpm;}
private:
    void install(const PreviewPattern& next) noexcept{pattern_=next;frame_=0;waiting_=next.countInBeats>0 || next.musical;startBeat_=UINT64_MAX;musicalStart_=UINT64_MAX;musicalCycle_=0;onsets_.fill(UINT64_MAX);gain_=0;transition_=last_;transitionFrames_=static_cast<uint64_t>(std::ceil(rate_*.01));active_.store(true);}
    const std::atomic<uint64_t>* authority_=nullptr;std::array<uint64_t,128> onsets_{};uint64_t musicalStart_=UINT64_MAX,musicalCycle_=0;double currentBpm_=100;
    RealtimeQueue<PreviewPattern,4> commands_;PreviewPattern pattern_{};
    std::atomic<uint64_t> desired_{0};std::atomic<bool> active_{false};
    double rate_=48000,gain_=0,last_=0,transition_=0;uint64_t frame_=0,startBeat_=UINT64_MAX,transitionFrames_=0;bool waiting_=false;
};
}
