// SPDX-License-Identifier: AGPL-3.0-only
#include "sample_clock.h"
#include <vector>
#include <stdexcept>
#include <iostream>
namespace {int checks=0;void check(bool value,const char* message){++checks;if(!value)throw std::runtime_error(message);}
std::vector<uint64_t> run(const std::vector<int>& blocks,double bpm,int duration){rocker::SampleClock clock;clock.prepare(48000,7);clock.tempo(bpm);std::vector<uint64_t> ticks;int done=0;size_t block=0;while(done<duration){int frames=std::min(blocks[block++%blocks.size()],duration-done);clock.beginBlock();for(int i=0;i<frames;++i){auto tick=clock.tick();if(tick.quarter)ticks.push_back(tick.frame);}clock.publish();done+=frames;}check(clock.observation().frame==static_cast<uint64_t>(duration),"clock sums exact processed samples");return ticks;}}
int main(){try{
    const auto constant=run({512},137,48000*60*5),variable=run({1,127,256,511,1024,63,7},137,48000*60*5);
    check(constant==variable,"beat positions do not depend on device block sizes");
    check(constant.size()==685,"five minute non-integer beat period neither loses nor duplicates events");
    for(size_t i=0;i<constant.size();++i){const auto expected=static_cast<uint64_t>(std::ceil(i*(60.0*48000/137)-1e-7));check(std::abs(static_cast<double>(constant[i])-expected)<=1,"no cumulative rounding drift at fractional periods");}
    rocker::SampleClock clock;clock.prepare(48000,2);clock.tempo(120);clock.beginBlock();
    for(int i=0;i<12000;++i)clock.tick();clock.publish();check(std::abs(clock.observation().phase-0.5)<1e-10,"half-beat phase before tempo change");
    clock.tempo(240);clock.beginBlock();std::vector<uint64_t> next;for(int i=0;i<18001;++i){auto tick=clock.tick();if(tick.quarter)next.push_back(tick.frame);}clock.publish();
    check(next==std::vector<uint64_t>({18000,30000}),"tempo change preserves phase and does not repeat the current beat");
    clock.prepare(44100,3);clock.beginBlock();auto tick=clock.tick();clock.publish();check(tick.frame==0 && tick.beat==0 && tick.quarter && clock.observation().epoch==3,"device restart starts a new epoch instead of mixing time bases");
    std::cout<<"PASS: "<<checks<<" sample clock, variable blocks, fractional timing, tempo changes and epoch contracts.\n";
}catch(const std::exception& e){std::cerr<<e.what()<<'\n';return 1;}}
