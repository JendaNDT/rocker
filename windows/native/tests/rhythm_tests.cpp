// SPDX-License-Identifier: AGPL-3.0-only
#include "rhythm_processor.h"
#include <vector>
#include <iostream>
#include <cstdlib>
#include <new>
static bool audit=false;static size_t allocations=0;
void* operator new(size_t n){if(audit)++allocations;if(auto* p=std::malloc(n?n:1))return p;throw std::bad_alloc();}
void operator delete(void* p) noexcept{std::free(p);}void operator delete(void* p,size_t) noexcept{std::free(p);}
static void check(bool value,const char* label){if(!value)throw std::runtime_error(label);}
struct Engine{
    std::atomic<uint64_t> trainer{1},phrase{1};rocker::SampleClock clock;rocker::PreviewProcessor preview;rocker::RhythmProcessor rhythm;
    Engine(){clock.prepare(8000,9);preview.prepare(8000);preview.authority(phrase);rhythm.authority(trainer,phrase);rhythm.prepare(9);}
    void arm(double round,uint32_t count=4,double pattern=6){rocker::RhythmCommand command;command.token=1;command.roundBeats=round;command.bpm=120;command.speedUp=true;command.pattern.token=1;command.pattern.musical=true;command.pattern.repeat=1;command.pattern.countInBeats=count;command.pattern.length=pattern;command.pattern.count=1;command.pattern.notes[0]={0,pattern,100,.2};preview.expect(1);check(rhythm.arm(command),"fixed command accepts a rhythm round");}
    void begin(){rhythm.beginBlock(clock,preview);clock.beginBlock();preview.beginBlock();}
    double sample(){auto tick=clock.tick();rhythm.process(tick,clock);preview.tempo(clock.currentBpm());return preview.process(tick);}
    void end(){clock.publish();rhythm.endBlock();}
};
int main(){try{
    auto render=[](uint32_t block){Engine e;e.arm(6);std::vector<double> values(66000);audit=true;for(uint32_t first=0;first<values.size();first+=block){e.begin();for(uint32_t i=first;i<std::min<uint32_t>(first+block,values.size());++i)values[i]=e.sample();e.end();}audit=false;check(allocations==0,"callback performs no heap allocation");auto r=e.rhythm.observation();check(r.epoch==9 && r.startFrame==16000 && r.rounds==2 && r.lastBpm==126 && r.bpm==132,"count-in and six-beat rounds advance by the completed tempo, including five percent rounding");return values;};
    auto single=render(1),irregular=render(131);check(single==irregular,"tempo changes and accompaniment are independent of block size");check(std::all_of(single.begin(),single.begin()+16000,[](double x){return x==0;}),"accompaniment stays silent for all four count-in beats");double energy=0;for(double x:single){check(std::isfinite(x)&&std::abs(x)<=1,"finite bounded audio");energy+=x*x;}check(energy>1,"actual synthetic accompaniment is produced");
    Engine pitch;pitch.arm(1,0,8);pitch.begin();double actual=0;for(int i=0;i<=4017;++i)actual=pitch.sample();pitch.end();double t=4017./8000,phase=2*std::numbers::pi*100*t,expected=.2*std::exp(-2*t)*(std::sin(phase)+.25*std::sin(2*phase)+.1*std::sin(3*phase))/1.35;check(std::abs(actual-expected)<1e-9,"tempo change preserves oscillator frequency and phase in a held musical note");
    ++pitch.trainer;pitch.begin();for(int i=0;i<128;++i)pitch.sample();pitch.end();check(pitch.rhythm.observation().state==3,"reserved cancellation pauses the trainer at the next sample");
    Engine cancelled;cancelled.arm(6);++cancelled.trainer;++cancelled.phrase;cancelled.begin();check(cancelled.sample()==0 && !cancelled.preview.active(),"a stale queued round cannot resurrect its accompaniment");
    std::cout<<"PASS: sample-exact count-in, non-bar rounds, tempo records, block invariance, pitch, cancellation and allocation\n";return 0;
}catch(const std::exception& e){std::cerr<<e.what()<<'\n';return 1;}}
