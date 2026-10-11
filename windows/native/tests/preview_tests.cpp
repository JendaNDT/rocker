// SPDX-License-Identifier: AGPL-3.0-only
#include "preview_processor.h"
#include <iostream>
#include <vector>
#include <cstdlib>
#include <new>
static bool audit=false;static size_t allocations=0;
void* operator new(size_t size){if(audit)++allocations;if(auto* value=std::malloc(size?size:1))return value;throw std::bad_alloc();}
void operator delete(void* value) noexcept{std::free(value);}void operator delete(void* value,size_t) noexcept{std::free(value);}
namespace{int checks=0;void check(bool ok,const char* label){++checks;if(!ok)throw std::runtime_error(label);}}
int main(){try{
    rocker::PreviewPattern phrase;phrase.token=1;phrase.count=3;phrase.length=.5;phrase.notes[0]={0,.5,110,.3};phrase.notes[1]={.018,.48,165,.3};phrase.notes[2]={.036,.46,220,.3};
    auto render=[&](uint32_t block){rocker::PreviewProcessor preview;rocker::SampleClock clock;preview.prepare(8000);clock.prepare(8000,1);preview.arm(phrase);std::vector<double> output(4200);for(uint32_t first=0;first<output.size();first+=block){preview.beginBlock();clock.beginBlock();for(uint32_t i=first;i<std::min<uint32_t>(first+block,output.size());++i)output[i]=preview.process(clock.tick());}return output;};
    auto one=render(1),irregular=render(131);check(one==irregular,"phrase sample timing is independent of callback block size");double energy=0;for(double sample:one){check(std::isfinite(sample) && std::abs(sample)<=1,"polyphonic synthetic preview stays finite and bounded");energy+=sample*sample;}check(energy>1,"chord preview produces actual audio samples");
    rocker::PreviewProcessor preview;rocker::SampleClock clock;preview.prepare(8000);clock.prepare(8000,1);clock.tempo(240);clock.beginBlock();phrase.countInBeats=4;preview.arm(phrase);preview.beginBlock();bool silent=true;for(int i=0;i<8000;++i)silent&=preview.process(clock.tick())==0;check(silent,"four-beat count-in produces no prematurely scheduled note");double after=0;audit=true;for(int i=0;i<800;++i)after+=std::abs(preview.process(clock.tick()));preview.cancel();preview.beginBlock();for(int i=0;i<100;++i)preview.process(clock.tick());audit=false;check(after>1 && !preview.active() && allocations==0,"preview renders and fades out through bounded callback operations without allocation");
    preview.arm(phrase);preview.cancel();preview.beginBlock();check(!preview.active() && preview.process(clock.tick())==0,"cancelled queued preview cannot resurrect sound");
    preview.arm(phrase);preview.beginBlock();preview.cancel();for(int i=0;i<100;++i)preview.process(clock.tick());check(!preview.active(),"cancellation while waiting for a beat does not leave an active preview forever");
    std::cout<<"PASS: "<<checks<<" finite sample, polyphony, count-in, cancellation and allocation assertions\n";return 0;
}catch(const std::exception& error){std::cerr<<error.what()<<'\n';return 1;}}
