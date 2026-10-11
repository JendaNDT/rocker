// SPDX-License-Identifier: AGPL-3.0-only
#include "analysis_processor.h"
#include <iostream>
#include <stdexcept>
using namespace rocker;
static int checks=0;static void check(bool valid,const char* text){++checks;if(!valid)throw std::runtime_error(text);}
template<class Signal> static AnalysisObservation feed(AnalysisProcessor& analyzer,double rate,double seconds,Signal signal,uint32_t epoch=1,uint64_t first=0,uint32_t size=256){
    const auto frames=static_cast<uint64_t>(rate*seconds);for(uint64_t offset=0;offset<frames;offset+=size){AnalysisBlock block;block.firstFrame=first+offset;block.epoch=epoch;block.sampleRate=rate;block.count=static_cast<uint32_t>(std::min<uint64_t>(size,frames-offset));for(uint32_t i=0;i<block.count;++i)block.mono[i]=static_cast<float>(signal((first+offset+i)/rate));check(analyzer.consume(block),"valid analysis block");}return analyzer.observation();
}
int main(){try{
    for(double rate:{8000.0,44100.0,48000.0,96000.0,192000.0})for(int midi:{24,28,33,40,45,50,55,59,64,69,76,84,88}){
        AnalysisProcessor analyzer;double frequency=440*std::pow(2.0,(midi-69)/12.0);auto reading=feed(analyzer,rate,.45,[&](double t){const double phase=2*std::numbers::pi*frequency*t;return 0.18*std::sin(phase)+0.07*std::sin(phase*2)+0.03*std::sin(phase*3);});
        const double cents=reading.frequency>0?1200*std::log2(reading.frequency/frequency):10000;
        if(std::abs(cents)>4)std::cerr<<"rate="<<rate<<" midi="<<midi<<" frequency="<<reading.frequency<<" cents="<<cents<<" clarity="<<reading.clarity<<'\n';
        check((reading.flags&1) && reading.clarity>=.9 && std::abs(cents)<4,"guitar range and harmonics within four cents");check(reading.frame==static_cast<uint64_t>(rate*.45) && reading.epoch==1,"analysis metadata tied to actual input frames");
    }
    AnalysisProcessor analyzer;const double sharp=82.406889*std::pow(2.0,35.0/1200);auto reading=feed(analyzer,48000,.5,[&](double t){return .2*std::sin(2*std::numbers::pi*sharp*t);},1,0,127);check(std::abs(1200*std::log2(reading.frequency/82.406889)-35)<2,"detuned guitar note preserves cents");
    reading=feed(analyzer,48000,.02,[](double){return 0.0;},1,24000);check(reading.frequency==0 && !(reading.flags&1),"silence clears a previous note immediately without presenting held tone as current");
    reading=feed(analyzer,48000,.25,[](double t){return .001*std::sin(2*std::numbers::pi*440*t);},1,24960);check(reading.frequency==0,"shared input scale noise gate suppresses quiet tone");
    uint32_t random=12;reading=feed(analyzer,48000,.3,[&](double){random=random*1664525u+1013904223u;return .1*(static_cast<double>(random)/UINT32_MAX*2-1);},2);check(reading.frequency==0 && !(reading.flags&1),"noise has no confident pitch");
    reading=feed(analyzer,48000,.4,[](double t){return .2*std::sin(2*std::numbers::pi*1000*t);},3);check(reading.centroid>980 && reading.centroid<1020,"native Hann FFT centroid follows a clean sinusoid");
    AnalysisProcessor strikes;auto hits=feed(strikes,48000,1.2,[](double t){const double local=std::fmod(t,.4);return local<.25?.25*std::sin(2*std::numbers::pi*330*t):0;});check(hits.hitCount==3 && hits.hitCentroid>300 && hits.hitCentroid<370,"three separate attacks capture the note body over 40–170 ms with 320 ms refractory");
    reading=feed(analyzer,48000,.01,[](double){return 0;},4);check(reading.frequency==0 && reading.hitCount==0 && reading.sequence==0,"new epoch starts warm-up rather than reusing old tone/hits");
    AnalysisBlock invalid;invalid.count=257;invalid.sampleRate=48000;check(!analyzer.consume(invalid),"invalid input block is rejected");
    std::cout<<"PASS: "<<checks<<" pitch range/harmonics, cents, rate conversion, noise, silence, FFT and onset checks.\n";
}catch(const std::exception& error){std::cerr<<error.what()<<'\n';return 1;}}
