// SPDX-License-Identifier: AGPL-3.0-only
#include "analysis_worker.h"
#include "reference_dsp.h"
#include <iostream>
#include <stdexcept>
using namespace rocker;
static int checks=0;static void check(bool value,const char* text){++checks;if(!value)throw std::runtime_error(text);}
int main(){try{
    AnalysisQueue queue;AnalysisWorker worker(queue);auto token=worker.calibrate();check(worker.report().calibrationToken==token && worker.report().calibrationFlags==8,"calibration request is immediately distinguishable from old results");
    for(uint64_t frame=0;frame<105600;frame+=256){while(queue.pending())std::this_thread::yield();AnalysisBlock block;block.firstFrame=frame;block.epoch=1;block.sampleRate=48000;block.count=static_cast<uint32_t>(std::min(uint64_t{256},105600-frame));for(uint32_t i=0;i<block.count;++i)block.mono[i]=.003f;check(queue.push(block),"offline feeder never overwrites analysis data");}
    auto deadline=std::chrono::steady_clock::now()+std::chrono::seconds(5);while(worker.report().observed.frame<105600 && std::chrono::steady_clock::now()<deadline)std::this_thread::yield();
    auto report=worker.report();check(report.observed.frame==105600 && report.calibrationToken==token && report.calibrationFlags==16 && report.recommendedGate==.04,"actual quiet samples produce scaled peak-plus-margin noise gate");
    const auto next=worker.calibrate();report=worker.report();check(next>token && report.calibrationToken==next && report.calibrationFlags==8,"new calibration cannot reuse an earlier completed recommendation");
    AnalysisBlock block;block.firstFrame=105600;block.epoch=1;block.sampleRate=48000;block.count=256;while(queue.pending())std::this_thread::yield();queue.push(block);while(worker.report().observed.frame<105856)std::this_thread::yield();
    block.firstFrame=0;block.epoch=2;queue.push(block);deadline=std::chrono::steady_clock::now()+std::chrono::seconds(5);while(worker.report().observed.epoch!=2 && std::chrono::steady_clock::now()<deadline)std::this_thread::yield();check(worker.report().calibrationFlags==32,"device epoch interrupts an incomplete calibration");
    ReferenceProcessor reference;reference.command({1,440,.3},true);reference.prepare(48000);double energy=0,peak=0;for(int i=0;i<48000;++i){const double out=reference.process();energy+=out*out;peak=std::max(peak,std::abs(out));}
    check(energy>100 && peak<=.435 && reference.playing(),"guitar-coloured reference generates bounded audio");
    reference.prepare(96000);for(int i=0;i<static_cast<int>(96000*4.1);++i)reference.process();check(!reference.playing() && !reference.wanted(),"five-second audible safety cap survives a sample-rate/graph restart");
    reference.command({1,329.627,.3},true);reference.prepare(48000);for(int i=0;i<2400;++i)reference.process();reference.command({0,440,0},false);reference.beginBlock();for(int i=0;i<2401;++i)reference.process();check(!reference.playing() && reference.process()==0,"reference release ends at silence within fifty milliseconds");
    reference.command({2,1320,.6},true);double first=0,second=0;for(int i=0;i<14400;++i){const double value=reference.process();if(i<6000)first+=value*value;if(i>6240)second+=value*value;}check(first>0 && second>0 && !reference.chimePlaying() && !reference.wanted(),"confirmation emits the two original frequency chimes then stops");
    reference.command({1,440,.3},false);reference.stopJoined();reference.beginBlock();check(!reference.wanted() && reference.process()==0,"global joined stop drains queued reference commands");
    std::cout<<"PASS: "<<checks<<" analysis worker, noise calibration generations, epochs, reference harmonics/envelopes/cap and confirmation contracts.\n";
}catch(const std::exception& error){std::cerr<<error.what()<<'\n';return 1;}}
