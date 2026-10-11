// SPDX-License-Identifier: AGPL-3.0-only
#include "callback_stats.h"
#include "render_processor.h"
#include <iostream>
#include <stdexcept>
using namespace rocker;
static int checks=0;static void check(bool value,const char* text){++checks;if(!value)throw std::runtime_error(text);}
int main(){try{
    CallbackStats stats;stats.prepare(48000);const auto origin=CallbackStats::Clock::time_point{};
    stats.completed(origin,origin+std::chrono::microseconds(100),128);stats.completed(origin,origin+std::chrono::milliseconds(10),256);stats.completed(origin,origin,0);
    auto values=stats.observation();check(values.callbacks==2 && values.frames==384 && values.totalNanoseconds==10100000 && values.lastNanoseconds==10000000 && values.maxNanoseconds==10000000,"exact synthetic timing accumulation");check(values.overBudget==1 && values.invalidCallbacks==1 && values.minFrames==128 && values.maxFrames==256,"deadline budget, invalid callback and block bounds are distinct");
    stats.prepare(96000);values=stats.observation();check(values.callbacks==0 && values.overBudget==0 && values.rate==96000,"new joined epoch resets only callback interval counters");
    RealtimeQueue<uint32_t,8> queue;for(uint32_t i=0;i<8;++i)queue.push(i);check(queue.highWatermark()==8 && !queue.push(99) && queue.dropped()==1,"producer watermark remains distinct from explicit dropped blocks");uint32_t item;while(queue.pop(item)){}check(queue.highWatermark()==8 && queue.dropped()==1,"drain retains diagnostic history");queue.reset();check(queue.highWatermark()==0 && queue.dropped()==0,"joined queue reset clears diagnostic history");
    RenderProcessor graph(440,.03);graph.prepare(48000);graph.selectLive();graph.enable(true);AmpParameters amp;amp.enabled=true;amp.values={1,1,1,1,1,1,1,1,1};graph.input().amp().configure(amp,true);graph.prepare(48000);graph.input().monitoring(true,.15);MetronomeParameters metro;metro.enabled=true;metro.bpm=137;metro.subdivision=4;metro.poly=true;metro.polyTicks=7;graph.input().metronome().configure(metro,true);graph.input().reference().command({1,440,.3},true);
    std::array<float,1024> left{},right{},input{};input.fill(.2f);float* outputs[]={left.data(),right.data()};const float* inputs[]={input.data()};uint64_t expected=0;
    for(int i=0;i<10000;++i){const int frames=i%2?127:1024;graph.process(inputs,1,outputs,2,frames);expected+=frames;AnalysisBlock block;while(graph.input().analysis().pop(block)){} }
    values=graph.callbacks().observation();check(values.callbacks==10000 && values.frames==expected && values.totalNanoseconds>0 && values.maxNanoseconds>=values.lastNanoseconds,"long synthetic active graph measures actual callback work across changing buffer sizes");check(values.minFrames==127 && values.maxFrames==1024 && graph.input().analysis().highWatermark()<=5 && graph.input().analysis().dropped()==0,"drained analysis stream stays within its bounded queue under long DSP load");check(graph.input().meters().invalid==0,"loaded graph remains finite throughout the long callback sequence");
    std::cout<<"PASS: "<<checks<<" synthetic diagnostic contracts; graph samples="<<values.frames<<", average_us="<<values.totalNanoseconds/(static_cast<double>(values.callbacks)*1000)<<", max_us="<<values.maxNanoseconds/1000.0<<", over_budget="<<values.overBudget<<" (not physical latency or hardware underruns).\n";
}catch(const std::exception& error){std::cerr<<error.what()<<'\n';return 1;}}
