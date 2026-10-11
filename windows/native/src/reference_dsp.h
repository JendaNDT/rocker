// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include "realtime_queue.h"
#include <algorithm>
#include <cmath>
#include <numbers>
namespace rocker {
struct ReferenceCommand{uint32_t kind=0;double frequency=440,volume=.3;};
class ReferenceProcessor {
public:
    void prepare(double rate) noexcept{rate_=rate;phase_=gain_=0;rampLevel_=volume_;}
    bool command(ReferenceCommand value,bool stopped) noexcept{
        if(stopped){ReferenceCommand old;while(queue_.pop(old)){}apply(value);}else if(!queue_.push(value))return false;
        if(value.kind==1)wanted_.store(true);
        if(value.kind==0)wanted_.store(false);
        if(value.kind==2)chimeWanted_.store(true);
        return true;
    }
    void stopJoined() noexcept{ReferenceCommand old;while(queue_.pop(old)){}wanted_.store(false);chimeWanted_.store(false);held_=chiming_=false;gain_=0;playing_.store(false);chimePlaying_.store(false);}
    bool wanted() const noexcept{return queue_.pending() || wanted_.load() || chimeWanted_.load();}
    bool playing() const noexcept{return playing_.load();}
    bool chimePlaying() const noexcept{return chimePlaying_.load();}
    void beginBlock() noexcept{ReferenceCommand value;while(queue_.pop(value))apply(value);}
    double process() noexcept{
        if(held_ && (heldSeconds_+=1/rate_)>=5){held_=false;wanted_.store(false);}
        const double target=held_?volume_:0;gain_+=std::clamp(target-gain_,-rampLevel_/(rate_*.05),rampLevel_/(rate_*.03));
        const double tone=gain_*(std::sin(phase_)+.3*std::sin(2*phase_)+.15*std::sin(3*phase_));phase_+=2*std::numbers::pi*frequency_/rate_;if(phase_>=2*std::numbers::pi)phase_=std::fmod(phase_,2*std::numbers::pi);
        double chime=0;if(chiming_){const double time=chimeSeconds_;chimeSeconds_+=1/rate_;chime=chimeAt(time,1320)+chimeAt(time-.13,1980);if(time>=.26){chiming_=false;chimeWanted_.store(false);}}
        const bool active=held_ || gain_>1e-8;if(playing_.load(std::memory_order_relaxed)!=active)playing_.store(active);if(chimePlaying_.load(std::memory_order_relaxed)!=chiming_)chimePlaying_.store(chiming_);return std::clamp(tone+chime,-1.0,1.0);
    }
private:
    void apply(const ReferenceCommand& value) noexcept{if(value.kind==0){rampLevel_=std::max(gain_,1e-6);held_=false;wanted_.store(false);}if(value.kind==1){held_=true;frequency_=value.frequency;volume_=value.volume;rampLevel_=std::max({gain_,volume_,1e-6});heldSeconds_=0;wanted_.store(true);}if(value.kind==2){chiming_=true;chimeSeconds_=0;chimeVolume_=value.volume;chimeWanted_.store(true);}}
    double chimeAt(double time,double frequency) const noexcept{if(time<0 || time>.124)return 0;const double envelope=time<.004?time/.004:std::exp(-9.210340371976184*(time-.004)/.12);return .45*chimeVolume_*envelope*std::sin(2*std::numbers::pi*frequency*time);}
    RealtimeQueue<ReferenceCommand,32> queue_;double rate_=48000,phase_=0,gain_=0,frequency_=440,volume_=.3,rampLevel_=.3,chimeVolume_=1;double heldSeconds_=0,chimeSeconds_=0;bool held_=false,chiming_=false;
    std::atomic<bool> wanted_{false},chimeWanted_{false},playing_{false},chimePlaying_{false};
};
}
