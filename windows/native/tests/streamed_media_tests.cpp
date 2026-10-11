// SPDX-License-Identifier: AGPL-3.0-only
#include "media_clip.h"
#include "render_processor.h"
#include <chrono>
#include <fstream>
#include <iostream>
#include <thread>
namespace {
int checks=0;void check(bool value,const char* message){++checks;if(!value)throw std::runtime_error(message);}
void u16(std::ofstream& out,uint16_t value){for(int i=0;i<2;++i)out.put(static_cast<char>(value>>(i*8)));}
void u32(std::ofstream& out,uint32_t value){for(int i=0;i<4;++i)out.put(static_cast<char>(value>>(i*8)));}
bool awaitFrame(rocker::media::PcmSource& source,uint64_t frame,std::array<float,2>& samples){source.request(frame);for(int i=0;i<1000;++i){if(source.read(frame,samples))return true;std::this_thread::sleep_for(std::chrono::milliseconds(2));}return false;}
}
int main(int argc,char** argv){try{
    check(argc==2,"fixture path required");std::atomic<uint64_t> generation{1};
    for(double speed:{.25,.5,.75,1.,1.5,2.}){
        auto clip=rocker::media::prepareFile(argv[1],speed,generation,1);
        check(clip.stream && clip.channels.empty(),"stream preparation retains no complete PCM vector");
        check(std::abs(clip.frames()/clip.sampleRate-1/speed)<.001,"chunk stretch exact target length");
        check(clip.stream->waveform().size()==1024,"bounded waveform cache");
        int crossings=0;std::array<float,2> current{},previous{};bool finite=true;
        const auto first=static_cast<uint64_t>(clip.sampleRate*.1/speed),last=static_cast<uint64_t>(clip.sampleRate*.8/speed);
        check(awaitFrame(*clip.stream,first,previous),"read-ahead reaches requested start");
        for(uint64_t i=first+1;i<last;++i){if(!clip.stream->read(i,current))check(awaitFrame(*clip.stream,i,current),"bounded read-ahead catches up");finite&=std::isfinite(current[0]) && current[0]==current[1];if(previous[0]<0 && current[0]>=0)++crossings;previous=current;}
        check(finite,"finite mono duplication");check(std::abs(crossings/((last-first)/clip.sampleRate)-440)<5,"chunk stretch retains A4 pitch");
    }
    const auto path=std::filesystem::temp_directory_path()/"rocker-stream-long-fixture.wav";
    struct Cleanup{std::filesystem::path path;~Cleanup(){std::error_code ignored;std::filesystem::remove(path,ignored);}} cleanup{path};
    constexpr uint32_t frames=8000*60*40,bytes=frames*2;
    {std::ofstream out(path,std::ios::binary|std::ios::trunc);out.write("RIFF",4);u32(out,bytes+36);out.write("WAVEfmt ",8);u32(out,16);u16(out,1);u16(out,1);u32(out,8000);u32(out,16000);u16(out,2);u16(out,16);out.write("data",4);u32(out,bytes);out.seekp(44+bytes-2);u16(out,8192);}
    auto longClip=rocker::media::prepareFile(path,1,generation,1);
    check(longClip.frames()==frames && longClip.stream && longClip.channels.empty(),"40-minute source exceeds previous 64 MiB PCM budget with bounded read-ahead");
    std::array<float,2> last{};check(awaitFrame(*longClip.stream,frames-1,last) && std::abs(last[0]-.25)<1e-6,"far seek loads the last source sample");
    rocker::RenderProcessor render(440,.02);render.install(std::move(longClip),1);render.prepare(48000);render.enable(true);render.input().monitoring(true,.5);
    rocker::MetronomeParameters metro;metro.enabled=true;render.input().metronome().configure(metro,true);
    std::array<float,512> in{},left{},right{};in.fill(.2f);const float* input[]{in.data()};float* output[]{left.data(),right.data()};
    render.process(input,1,output,2,512);const auto clock=render.input().clock().observation();
    check(render.requestSeek(1800,false),"running seek queues without joining device");render.process(input,1,output,2,512);
    check(render.input().clock().observation().frame==clock.frame+512 && render.input().monitoring(),"seek preserves sample clock and monitoring");
    check(render.requestPlayback(false,false),"pause command");render.process(input,1,output,2,512);const auto paused=render.position();render.process(input,1,output,2,512);
    check(render.position()==paused && !render.playing() && left[511]>.05,"paused backing retains live input");
    // A prepared next speed switches at the exact source loop wrap; no ownership
    // is destroyed there and no input/metronome clock epoch is reset.
    rocker::media::Clip initial{48000,{std::vector<float>(200,.2f)}},next{48000,{std::vector<float>(100,.2f)}};
    rocker::RenderProcessor ramp(440,.02);ramp.stage(&initial,1,1,true);ramp.prepare(48000);ramp.enable(true);ramp.selectClip();ramp.loop(0,200./48000,true);
    check(ramp.stage(&next,2,2,false,1),"future speed queues for loop boundary");ramp.process(input,1,output,2,150);
    check(ramp.speed()==1 && ramp.rampPending() && ramp.clipLease().current==1 && ramp.clipLease().pending==2,"pending cache is retained before boundary");
    ramp.process(input,1,output,2,100);
    check(ramp.speed()==2 && !ramp.rampPending() && ramp.loopCycles()==1 && std::abs(ramp.position()*48000-50)<1e-8,"next speed begins exactly at source loop wrap");
    check(ramp.input().clock().observation().frame==250 && ramp.clipLease().current==2 && ramp.clipLease().consumed==2,"ramp preserves frame clock and acknowledges ownership after callback");
    ramp.requestPlayback(false,true);ramp.seek(0);ramp.loop(0,0,false);ramp.prepare(48000);ramp.selectClip();
    check(ramp.requestCountIn(1,4,120),"sample-counted count-in queues");
    for(int i=0;i<187;++i)ramp.process(input,1,output,2,512);
    check(ramp.position()==0 && ramp.waiting(),"backing remains stationary during count-in");ramp.process(input,1,output,2,256);
    check(ramp.position()==0 && !ramp.waiting(),"count-in ends exactly after 96000 samples");ramp.process(input,1,output,2,16);
    check(std::abs(ramp.position()*48000-16)<1e-8,"backing starts on the following sample, independent of block size");
    // Retire thousands of actual PCM owners while a callback consumer runs.
    // ASan detects a premature reclamation; the queue/lease contract must also
    // make progress without keeping every past source alive.
    rocker::RenderProcessor concurrent(440,.02);concurrent.prepare(48000);concurrent.enable(true);
    std::atomic<bool> finish{false};std::thread callback([&]{std::array<float,127> l{},r{};float* outs[]{l.data(),r.data()};while(!finish.load())concurrent.process(nullptr,0,outs,2,127);});
    struct Owner{uint64_t id;std::unique_ptr<rocker::media::Clip> clip;};std::vector<Owner> owners;
    for(uint64_t id=1;id<=2000;++id){auto candidate=std::make_unique<rocker::media::Clip>();candidate->sampleRate=48000;candidate->channels={std::vector<float>(4800,.1f)};
        while(!concurrent.stage(candidate.get(),1,id,false))std::this_thread::yield();owners.push_back({id,std::move(candidate)});
        const auto lease=concurrent.clipLease();if(lease.valid)std::erase_if(owners,[&](const auto& owner){return owner.id<=lease.consumed && owner.id!=lease.current && owner.id!=lease.pending;});
    }
    while(concurrent.acknowledgedClip()!=2000)std::this_thread::yield();finish.store(true);callback.join();
    const auto finalLease=concurrent.clipLease();std::erase_if(owners,[&](const auto& owner){return owner.id<=finalLease.consumed && owner.id!=finalLease.current && owner.id!=finalLease.pending;});
    check(owners.size()==1 && owners[0].id==2000,"concurrent source replacement retires old owners after consistent callback acknowledgements");
    generation.store(2);try{rocker::media::prepareFile(path,1,generation,1);check(false,"superseded task accepted");}catch(const rocker::media::Failure&){check(true,"superseded task cancels before decode");}
    std::cout<<"PASS: "<<checks<<" chunked PCM, pitch, long-file, seek and independent-monitor assertions\n";
    return 0;
}catch(const std::exception& error){std::cerr<<error.what()<<'\n';return 1;}}
