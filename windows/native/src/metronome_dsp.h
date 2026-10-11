// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include "biquad.h"
#include "sample_clock.h"
#include "realtime_queue.h"
#include <array>
namespace rocker {
struct MetronomeParameters {
    bool enabled=false;uint32_t bpm=100,beats=4,subdivision=1,sound=0,subSound=5,countInBars=0;
    bool ramp=false,gap=false,poly=false;uint32_t rampEvery=4,rampStep=5,rampTarget=160,audibleBars=4,silentBars=1,polyTicks=3,polySound=4;
    double volume=0.8,accentVolume=1,swing=0.5,polyVolume=0.6;
    std::array<uint32_t,16> accents{2,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1};
    std::array<uint32_t,16> sounds{5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5};
};
struct MetronomeObservation{uint32_t flags=0;int32_t beat=-1;int64_t bar=0;uint64_t frame=0,primaryEvents=0,secondaryEvents=0,voiceOverloads=0;uint32_t subTick=0,polyTick=0;};
struct MetronomeEvent{uint64_t frame;int64_t bar;uint32_t beat,tick,sound;bool secondary,accent;};
class MetronomeProcessor {
public:
    void prepare(double rate) noexcept {rate_=rate;random_=0x61a92d37;for(auto& voice:voices_)voice.active=false;waiting_=true;started_=false;countIn_=params_.countInBars;rampBars_=0;nextSub_=nextPoly_=0;observation_={};publish();}
    bool configure(const MetronomeParameters& value,bool stopped) noexcept {if(stopped){MetronomeParameters old;while(commands_.pop(old)){}apply(value);return true;}return commands_.push(value);}
    void disableStopped() noexcept {MetronomeParameters old;while(commands_.pop(old)){}params_.enabled=false;for(auto& v:voices_)v.active=false;waiting_=true;started_=false;observation_.flags=0;observation_.beat=-1;publish();}
    bool enabled() const noexcept{return enabled_.load();}
    void beginBlock() noexcept {MetronomeParameters value;while(commands_.pop(value))apply(value);}
    MetronomeObservation observation() const noexcept {return {flags_.load(),beat_.load(),bar_.load(),frame_.load(),primaryEvents_.load(),secondaryEvents_.load(),overloads_.load(),subTick_.load(),polyTick_.load()};}
    template<class EventCallback> double process(const GridTick& tick,SampleClock& clock,EventCallback event) noexcept {
        if(params_.enabled){
            if(waiting_ && tick.quarter){baseBeat_=tick.beat;waiting_=false;started_=true;nextSub_=nextPoly_=0;rampBars_=0;}
            if(started_){
                const uint64_t relative=tick.beat-baseBeat_;const auto beat=static_cast<uint32_t>(relative%params_.beats);const int64_t bar=static_cast<int64_t>(relative/params_.beats)-countIn_;
                if(tick.quarter){nextSub_=nextPoly_=0;
                    if(beat==0 && relative>0 && bar>=0 && params_.ramp && ++rampBars_>=params_.rampEvery){rampBars_=0;const auto now=clock.currentBpm();const double bpm=std::min(now+params_.rampStep,static_cast<double>(params_.rampTarget));if(now<params_.rampTarget)clock.tempoAfterTick(bpm);}
                }
                const bool silent=params_.gap && bar>=0 && static_cast<uint64_t>(bar)%(params_.audibleBars+params_.silentBars)>=params_.audibleBars;
                observation_.flags=1u|(bar<0?2u:0u)|(silent?4u:0u);observation_.beat=static_cast<int32_t>(beat);observation_.bar=bar;observation_.frame=tick.frame;
                while(nextSub_<params_.subdivision && tick.phase+1e-12>=subPhase(nextSub_)){
                    const auto sub=nextSub_++;observation_.subTick=sub;const bool accent=sub==0 && params_.accents[beat]==2;
                    const uint32_t sound=sub>0 && params_.subSound<5?params_.subSound:params_.sounds[beat]<5?params_.sounds[beat]:params_.sound;
                    if(!silent && params_.accents[beat]!=0){++observation_.primaryEvents;event(MetronomeEvent{tick.frame,bar,beat,sub,sound,false,accent});trigger(sound,(accent?0.6+0.4*params_.accentVolume:sub>0?0.32:0.6)*params_.volume,accent,sub>0,false);}
                }
                while(params_.poly && nextPoly_<params_.polyTicks && tick.phase+1e-12>=static_cast<double>(nextPoly_)/params_.polyTicks){
                    const auto index=nextPoly_++;observation_.polyTick=index;
                    if(!silent && bar>=0){++observation_.secondaryEvents;event(MetronomeEvent{tick.frame,bar,beat,index,params_.polySound,true,false});trigger(params_.polySound,params_.polyVolume*params_.volume*0.5,false,false,true);}
                }
            }
        }
        double sample=0;for(auto& voice:voices_)if(voice.active)sample+=voiceSample(voice);return std::clamp(sample,-1.0,1.0);
    }
    double process(const GridTick& tick,SampleClock& clock) noexcept{return process(tick,clock,[](const MetronomeEvent&){});}
    void publish() noexcept {enabled_.store(params_.enabled);flags_.store(observation_.flags);beat_.store(observation_.beat);bar_.store(observation_.bar);frame_.store(observation_.frame);primaryEvents_.store(observation_.primaryEvents);secondaryEvents_.store(observation_.secondaryEvents);overloads_.store(observation_.voiceOverloads);subTick_.store(observation_.subTick);polyTick_.store(observation_.polyTick);}
private:
    struct Voice{bool active=false,noise=false,triangle=false;uint32_t age=0,duration=0;double level=0,phase=0,increment=0,attack=0,decay=0;Biquad filter;};
    void apply(const MetronomeParameters& value) noexcept {
        if(value.enabled && !params_.enabled){waiting_=true;started_=false;countIn_=value.countInBars;}
        else if(value.enabled && (value.beats!=params_.beats || value.subdivision!=params_.subdivision)){waiting_=true;started_=false;countIn_=0;}
        if(!value.enabled){started_=false;waiting_=true;observation_.flags=0;observation_.beat=-1;}
        params_=value;enabled_.store(value.enabled);
    }
    double subPhase(uint32_t tick) const noexcept {if(params_.subdivision==2)return tick==0?0:params_.swing;if(params_.subdivision==4)return tick/2*0.5+(tick%2?params_.swing*0.5:0);return static_cast<double>(tick)/params_.subdivision;}
    void trigger(uint32_t sound,double level,bool accent,bool subdivision,bool secondary) noexcept {
        if(level<0.0005)return;
        Voice* selected=nullptr;for(auto& voice:voices_)if(!voice.active){selected=&voice;break;}
        if(!selected){++observation_.voiceOverloads;return;}auto& voice=*selected;voice={};voice.active=true;voice.level=level;voice.attack=sound==2?0.004:0.001;
        double frequency=1000,decay=0.05;
        if(sound==0){frequency=secondary?1100:accent?1600:subdivision?760:1000;decay=secondary?0.04:0.05;}
        if(sound==1){frequency=secondary?950:accent?1150:subdivision?720:920;decay=secondary?0.03:0.035;voice.triangle=true;}
        if(sound==2){frequency=secondary?900:accent?1320:subdivision?660:880;decay=secondary?0.08:0.09;}
        if(sound>=3){voice.noise=true;voice.attack=0;frequency=sound==3?(secondary?2000:accent?2600:subdivision?1500:2000):(secondary?7500:accent?9000:subdivision?7000:8000);decay=sound==3?(accent?0.02:secondary?0.012:0.014):(secondary?0.035:accent?0.06:subdivision?0.03:0.045);voice.filter.configure(Biquad::Highpass,rate_,frequency,0.707,0);}
        voice.increment=2*std::numbers::pi*frequency/rate_;voice.decay=decay;voice.duration=static_cast<uint32_t>(std::ceil(rate_*(voice.attack+decay+0.02)));
    }
    double voiceSample(Voice& voice) noexcept {
        const double age=voice.age/rate_,attack=voice.attack;const double envelope=age<attack?voice.level*age/attack:voice.level*std::exp(-9.210340371976184*(age-attack)/voice.decay);
        double source=0;if(voice.noise){random_^=random_<<13;random_^=random_>>17;random_^=random_<<5;source=voice.filter.process(static_cast<double>(random_)/UINT32_MAX*2-1);}else{source=voice.triangle?2/std::numbers::pi*std::asin(std::sin(voice.phase)):std::sin(voice.phase);voice.phase+=voice.increment;if(voice.phase>=2*std::numbers::pi)voice.phase-=2*std::numbers::pi;}
        if(++voice.age>=voice.duration)voice.active=false;
        return source*envelope;
    }
    RealtimeQueue<MetronomeParameters,32> commands_;MetronomeParameters params_;MetronomeObservation observation_;std::array<Voice,16> voices_;
    double rate_=48000;bool waiting_=true,started_=false;uint64_t baseBeat_=0;uint32_t countIn_=0,rampBars_=0,nextSub_=0,nextPoly_=0,random_=0x61a92d37;
    std::atomic<bool> enabled_{false};std::atomic<uint32_t> flags_{0},subTick_{0},polyTick_{0};std::atomic<int32_t> beat_{-1};std::atomic<int64_t> bar_{0};std::atomic<uint64_t> frame_{0},primaryEvents_{0},secondaryEvents_{0},overloads_{0};
};
}
