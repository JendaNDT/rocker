// SPDX-License-Identifier: AGPL-3.0-only
#include "realtime_queue.h"
#include "control_executor.h"
#include "render_processor.h"
#include <cstdlib>
#include <iostream>
#include <new>
#include <thread>
#include <vector>
namespace {
thread_local bool audit=false;
std::atomic<uint64_t> allocations{0},releases{0};
int checks=0;
void check(bool valid,const char* message){++checks;if(!valid){std::cerr<<message<<'\n';std::exit(1);}}
}
void* operator new(std::size_t size){if(audit)++allocations;if(void* p=std::malloc(size?size:1))return p;throw std::bad_alloc();}
void* operator new[](std::size_t size){return ::operator new(size);}
void operator delete(void* p) noexcept {if(audit && p)++releases;std::free(p);}
void operator delete[](void* p) noexcept {::operator delete(p);}
void operator delete(void* p,std::size_t) noexcept {::operator delete(p);}
void operator delete[](void* p,std::size_t) noexcept {::operator delete(p);}
int main(){
    std::promise<void> failedMaintenance;auto failed=failedMaintenance.get_future();
    rocker::ControlExecutor executor([&]{failedMaintenance.set_value();throw std::runtime_error("Injected coordinator failure");},[]{});
    failed.get();bool rejected=false;try{executor.invoke([]{return 1;});}catch(const std::exception&){rejected=true;}
    check(rejected,"failed coordinator rejects or releases pending work instead of hanging the caller");
    audit=true;void* canary=::operator new(64);::operator delete(canary);audit=false;
    check(allocations.load()==1 && releases.load()==1,"allocation audit detects a deliberate allocation and release");allocations.store(0);releases.store(0);
    rocker::RealtimeQueue<uint64_t,8> queue;uint64_t item=0;
    check(!queue.pop(item),"empty queue");
    for(uint64_t i=0;i<8;++i)check(queue.push(i),"bounded enqueue");
    check(!queue.push(99) && queue.dropped()==1,"full queue reports loss without overwriting unread data");
    for(uint64_t i=0;i<8;++i)check(queue.pop(item) && item==i,"queue order");
    check(!queue.pop(item),"queue drained");queue.reset();check(queue.dropped()==0,"reset only while stopped");
    constexpr uint64_t count=200000;
    std::atomic<bool> ordered{true};
    std::thread consumer([&]{for(uint64_t expected=0;expected<count;){uint64_t next;if(queue.pop(next)){if(next!=expected)ordered.store(false);++expected;}else std::this_thread::yield();}});
    for(uint64_t next=0;next<count;){if(queue.push(next))++next;else std::this_thread::yield();}
    consumer.join();check(ordered.load(),"concurrent SPSC order across wraps");
    rocker::RenderProcessor processor(440,0.03);processor.prepare(48000);processor.enable(true);
    std::array<float,512> left{},right{};float* outputs[]={left.data(),right.data()};
    rocker::AnalysisQueue analysis;rocker::AnalysisBlock block;block.count=256;block.sampleRate=48000;
    audit=true;
    for(int i=0;i<20;++i){processor.render(outputs,2,512);analysis.push(block);analysis.pop(block);}
    audit=false;check(allocations.load()==0 && releases.load()==0,"tone callback and bounded queue allocate/release no ownership");
    rocker::media::Clip clip;clip.sampleRate=48000;clip.channels={{}};clip.channels[0].assign(48000,0.1f);
    processor.install(std::move(clip),1);processor.prepare(48000);
    audit=true;for(int i=0;i<20;++i)processor.render(outputs,2,512);audit=false;
    check(allocations.load()==0 && releases.load()==0,"PCM callback allocates/releases no ownership");
    std::array<float,512> input{};input.fill(0.2f);const float* inputs[]={input.data()};
    processor.selectLive();processor.prepare(48000);processor.input().monitoring(true,0.5);
    audit=true;for(int i=0;i<20;++i)processor.process(inputs,1,outputs,2,512);audit=false;
    check(allocations.load()==0 && releases.load()==0,"input buses and analysis queue allocate/release no callback ownership");
    rocker::AmpParameters amplifier;amplifier.enabled=true;amplifier.values={1,1,1,1,1,1,1,1,1};processor.input().amp().configure(amplifier,true);processor.prepare(48000);
    audit=true;for(int i=0;i<20;++i)processor.process(inputs,1,outputs,2,512);audit=false;
    check(allocations.load()==0 && releases.load()==0,"active amplifier/FIR/EQ/effect rings allocate or release no callback ownership");
    std::atomic<uint64_t> authority{1};processor.authority(authority);processor.authorize(1);authority.store(2);processor.enable(true);
    processor.render(outputs,2,512);
    check(std::all_of(left.begin(),left.end(),[](float v){return v==0;}) && std::all_of(right.begin(),right.end(),[](float v){return v==0;}),"late enable cannot resurrect a cancelled authority");
    std::cout<<"PASS: "<<checks<<" realtime queue, concurrent ordering and callback allocation assertions.\n";
}
