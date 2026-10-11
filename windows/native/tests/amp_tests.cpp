// SPDX-License-Identifier: AGPL-3.0-only
#include "amp_dsp.h"
#include <iostream>
#include <stdexcept>
#include <limits>
namespace {int checks=0;void check(bool value,const char* message){++checks;if(!value)throw std::runtime_error(message);}}
int main(){try{
    rocker::AmpProcessor amp;amp.prepare(48000);bool bypass=true;
    for(int i=0;i<4800;++i){double raw=0.5*std::sin(i*0.123);bypass&=amp.process(raw)==raw;}
    check(bypass && amp.latency()==0,"disabled amp is exact zero-delay unity bypass");
    rocker::DistortionOversampler sampler;sampler.prepare();double largest=0;int largestAt=0;
    for(int i=0;i<64;++i){const double value=std::abs(sampler.process(i==0?1e-5:0,{1,0,0}));if(value>largest){largest=value;largestAt=i;}}
    check(largestAt==15,"two linear-phase halfband filters have 15-sample group delay");
    uint32_t random=42;
    for(double rate:{44100.0,48000.0,96000.0,192000.0})for(uint32_t type=0;type<3;++type)for(uint32_t cab=0;cab<3;++cab){
        rocker::AmpParameters params;params.enabled=true;params.distortion=type;params.cabinet=cab;params.values={1,1,1,1,1,1,1,1,1};
        amp.configure(params,true);amp.prepare(rate);bool finite=true;double peak=0,energy=0;
        for(int i=0;i<static_cast<int>(rate*0.25);++i){random=random*1664525u+1013904223u;const double raw=(static_cast<double>(random)/UINT32_MAX-0.5)*2;const double out=amp.process(raw);finite&=std::isfinite(out)&&std::abs(out)<=1.000001;peak=std::max(peak,std::abs(out));energy+=out*out;}
        check(finite && amp.faults()==0 && peak>1e-4 && energy>0,"all distortion/cabinet/sample-rate combinations stay finite and bounded at extreme gains");
        check(amp.latency()==15,"enabled nonlinear path publishes its constant FIR group delay");
    }
    rocker::AmpParameters params;params.enabled=true;params.distortion=0;params.values={0,0,0,0,0,0.7,0.7,0.7,0.6};amp.configure(params,true);amp.prepare(48000);
    double early=0,late=0;for(int i=0;i<48000*5;++i){const double out=amp.process(i==0?0.5:0);if(i>48000 && i<96000)early+=out*out;if(i>48000*4)late+=out*out;}
    check(early>1e-12 && late<early*0.01,"delay/reverb tail decays instead of entering unstable feedback");
    // A live toggle crossfades, then lazily clears delay generations without a large callback fill.
    params.enabled=false;check(amp.configure(params,false),"bounded live bypass command accepted");amp.beginBlock();
    for(int i=0;i<2000;++i)amp.process(0.25);
    check(amp.process(0.25)==0.25 && amp.latency()==0,"bypass eventually returns exact raw samples and zero algorithmic delay");
    for(int i=0;i<100;++i)amp.process(0);params.enabled=true;amp.configure(params,false);amp.beginBlock();bool cleared=true;
    for(int i=0;i<48000;++i)cleared&=amp.process(0)==0;check(cleared,"reenabling on silence does not resurrect old effect tails");
    // Compare active zero wet mixes with different histories: no hidden wet gain.
    rocker::AmpProcessor dryA,dryB;params.values={0,0,0,0,0,0,0,0,0.6};dryA.configure(params,true);dryB.configure(params,true);dryA.prepare(48000);dryB.prepare(48000);bool equal=true;
    for(int i=0;i<4800;++i){double raw=0.1*std::sin(i*0.3);equal&=dryA.process(raw)==dryB.process(raw);}
    check(equal && dryA.latency()==dryB.latency(),"zero wet mixes do not add a delayed return");
    for(int i=0;i<32;++i)check(amp.configure(params,false),"bounded parameter slot");check(!amp.configure(params,false),"full parameter queue reports overload rather than blocking the callback");amp.beginBlock();check(amp.configure(params,false),"parameter queue recovers after callback consumption");
    std::cout<<"PASS: "<<checks<<" amplifier stability, zero-delay bypass, FIR latency, cabinet/drive, tails and bounded control contracts.\n";
}catch(const std::exception& e){std::cerr<<e.what()<<'\n';return 1;}}
