// SPDX-License-Identifier: AGPL-3.0-only
#include "render_processor.h"
#include <array>
#include <iostream>
#include <limits>
#include <stdexcept>
namespace {int checks=0;void check(bool value,const char* message){++checks;if(!value)throw std::runtime_error(message);}}
int main(){try{
    rocker::RenderProcessor graph(440,0.02);graph.prepare(48000);graph.selectLive();graph.enable(true);
    std::array<float,1024> guitar{},other{},left{},right{},extra{};guitar.fill(0.25f);other.fill(-0.8f);
    const float* inputs[]={guitar.data(),other.data()};float* outputs[]={left.data(),right.data(),extra.data()};
    graph.process(inputs,2,outputs,3,1024);
    check(std::all_of(left.begin(),left.end(),[](float x){return x==0;}),"input monitoring defaults to silence");
    auto meter=graph.input().meters();check(meter.inputPeak==0.25 && std::abs(meter.inputRms-0.25)<1e-9,"dry bus measures the single packed physical input");
    graph.input().monitoring(true,0.5);graph.process(inputs,2,outputs,3,1024);
    check(left==right && std::abs(left.back()-0.125f)<1e-6,"selected mono input reaches both slaved outputs");
    check(std::all_of(extra.begin(),extra.end(),[](float x){return x==0;}),"unselected extra output stays silent");
    check(left.front()<left.back(),"monitor gain ramps instead of jumping");
    other.fill(0.99f);graph.process(inputs,2,outputs,3,1024);check(std::abs(left.back()-0.125f)<1e-6,"unselected second channel never mixes into guitar");
    graph.input().monitoring(false,0.5);graph.process(inputs,2,outputs,3,1024);check(left.back()==0 && right.back()==0,"monitor mute reaches silence");
    rocker::AnalysisBlock block;check(graph.input().analysis().pop(block) && block.firstFrame==0 && block.count==256 && block.epoch==1 && block.sampleRate==48000 && block.mono[0]==0.25f,"analysis receives pre-monitor dry PCM and sample-clock metadata");
    graph.input().monitoring(true,1);guitar[0]=std::numeric_limits<float>::quiet_NaN();guitar[1]=std::numeric_limits<float>::infinity();guitar[2]=2;
    graph.process(inputs,2,outputs,3,1024);meter=graph.input().meters();
    check(meter.invalid==2 && meter.inputClips==1,"invalid and clipped input is diagnosed");
    check(std::all_of(left.begin(),left.end(),[](float x){return std::isfinite(x) && std::abs(x)<=1;}),"invalid samples never reach speakers");
    guitar.fill(0);graph.process(inputs,2,outputs,3,1024);meter=graph.input().meters();check(meter.inputPeak==0 && meter.inputRms==0 && meter.mixPeak==0,"silence clears meters rather than showing an old signal");
    graph.process(nullptr,0,outputs,3,1024);check(!graph.input().hasInput(),"missing input differs from a real quiet signal");
    graph.input().monitoring(true,1);graph.process(inputs,1,outputs,3,1024);
    const auto epoch=meter.epoch;graph.prepare(44100);graph.process(inputs,1,outputs,3,64);meter=graph.input().meters();check(meter.epoch==epoch+1 && meter.frame==64,"sample-rate restart changes epoch and restarts sample clock");
    for(int i=0;i<100;++i)graph.process(inputs,1,outputs,3,1024);check(graph.input().meters().dropped>0,"analysis saturation reports loss and never blocks the callback");
    std::cout<<"PASS: "<<checks<<" mono routing, stereo monitoring, bus meters, invalid samples and bounded analysis contracts.\n";
}catch(const std::exception& e){std::cerr<<e.what()<<'\n';return 1;}}
