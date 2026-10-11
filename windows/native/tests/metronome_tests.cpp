// SPDX-License-Identifier: AGPL-3.0-only
#include "metronome_dsp.h"
#include <vector>
#include <iostream>
#include <stdexcept>
using namespace rocker;
static int checks=0;
static void check(bool value,const char* text){++checks;if(!value)throw std::runtime_error(text);}
struct Result{std::vector<MetronomeEvent> events;double energy=0;MetronomeObservation observation;double bpm=0;};
static Result run(MetronomeParameters params,double seconds,int block=512,double rate=48000){
    MetronomeProcessor metro;SampleClock clock;Result result;metro.configure(params,true);metro.prepare(rate);clock.tempo(params.bpm);clock.prepare(rate,1);
    const int samples=static_cast<int>(seconds*rate);bool finite=true;
    for(int first=0;first<samples;first+=block){metro.beginBlock();clock.beginBlock();for(int i=first;i<std::min(samples,first+block);++i){const double out=metro.process(clock.tick(),clock,[&](const auto& event){result.events.push_back(event);});finite&=std::isfinite(out)&&std::abs(out)<=1;result.energy+=out*out;}metro.publish();clock.publish();}
    check(finite,"click output finite and bounded");result.observation=metro.observation();result.bpm=clock.observation().bpm;return result;
}
int main(){try{
    MetronomeParameters p;p.enabled=true;p.bpm=120;
    auto base=run(p,2);check(base.events.size()==4 && base.events[0].accent && !base.events[1].accent,"bar accents and number of quarter notes");
    for(size_t i=0;i<base.events.size();++i)check(base.events[i].frame==i*24000,"quarter onset is an exact sample frame");check(base.energy>0,"native click generates audio");
    p.subdivision=4;p.swing=.7;p.poly=true;auto a=run(p,3,7),b=run(p,3,511);check(a.events.size()==b.events.size(),"changing buffer partition does not add events");
    for(size_t i=0;i<a.events.size();++i)check(a.events[i].frame==b.events[i].frame && a.events[i].secondary==b.events[i].secondary,"all primary/poly onsets independent of block size");
    std::vector<uint64_t> frames;for(const auto& event:a.events)if(!event.secondary && event.beat==0 && event.bar==0)frames.push_back(event.frame);
    check(frames==std::vector<uint64_t>{0,8400,12000,20400},"swing delays odd sixteenths and preserves beat length");
    p.subdivision=3;auto triplet=run(p,.5);frames.clear();for(const auto& e:triplet.events)if(!e.secondary)frames.push_back(e.frame);check(frames==std::vector<uint64_t>{0,8000,16000},"triplets remain straight when swing is enabled");
    p.subdivision=2;p.countInBars=1;p.gap=true;p.audibleBars=1;p.silentBars=1;auto gap=run(p,8);unsigned countIn=0,real=0,poly=0;bool noSilent=true;
    for(const auto& e:gap.events){if(e.bar<0){++countIn;noSilent&=!e.secondary;}else{if(e.secondary)++poly;else ++real;noSilent&=e.bar%2==0;}}
    check(countIn==8 && real==16 && poly==24 && noSilent,"count-in always audible, poly absent during count-in, gap silences both layers");
    p.countInBars=0;p.gap=false;p.poly=false;p.beats=1;p.subdivision=1;p.ramp=true;p.rampEvery=1;p.rampStep=20;p.rampTarget=160;auto ramp=run(p,2);
    check(ramp.bpm==160 && ramp.events[1].frame==24000 && std::abs(static_cast<int64_t>(ramp.events[2].frame)-44572)<=1,"tempo ramps on the exact bar sample and carries fractional phase");
    p.ramp=false;p.beats=4;p.subdivision=2;p.sounds[0]=1;p.subSound=2;p.accents[1]=0;auto drums=run(p,1);check(drums.events.size()==2 && drums.events[0].sound==1 && drums.events[1].sound==2,"per-beat sound and subdivision override, muted beats omit every subclick");
    p=MetronomeParameters{};p.enabled=true;p.bpm=240;p.subdivision=4;p.poly=true;p.polyTicks=7;
    for(double rate:{8000.0,44100.0,48000.0,96000.0,192000.0})for(uint32_t sound=0;sound<5;++sound){p.sound=p.polySound=sound;auto soundTest=run(p,.25,127,rate);check(soundTest.energy>0 && soundTest.observation.voiceOverloads==0,"all five native sounds at every supported test rate fit fixed voice pool");}
    p=MetronomeParameters{};p.enabled=true;p.bpm=137;p.subdivision=4;p.poly=true;p.polyTicks=7;
    auto longRun=run(p,300,127);bool onTime=true;for(const auto& event:longRun.events){const double fraction=event.secondary?event.tick/7.0:event.tick/4.0;const double expected=(event.bar*p.beats+event.beat+fraction)*48000*60/137;onTime&=std::abs(static_cast<double>(event.frame)-expected)<=1.000001;}
    check(onTime && longRun.events.size()>7000,"five-minute fractional primary and polyrhythm clock has no accumulated sample drift");
    MetronomeProcessor metro;SampleClock clock;metro.configure(p,true);metro.prepare(48000);clock.prepare(48000,1);clock.beginBlock();metro.beginBlock();metro.process(clock.tick(),clock);metro.disableStopped();metro.publish();check(!metro.enabled() && metro.observation().beat==-1,"global stop disarms and clears visible beat");
    for(int i=0;i<32;++i)check(metro.configure(p,false),"bounded metronome queue slot");check(!metro.configure(p,false),"full queue reports loss");metro.beginBlock();check(metro.configure(p,false),"queue drains on audio block");
    std::cout<<"PASS: "<<checks<<" metronome sample timing, swing, count-in, gaps, ramps, sounds and bounded queue checks.\n";
}catch(const std::exception& error){std::cerr<<error.what()<<'\n';return 1;}}
