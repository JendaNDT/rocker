// SPDX-License-Identifier: AGPL-3.0-only
#include "project_mixer.h"
#include "render_processor.h"
#include <iostream>
#include <cstdlib>
#include <new>
static bool auditAllocations=false;static size_t allocationCount=0;
void* operator new(size_t size){if(auditAllocations)++allocationCount;if(auto* value=std::malloc(size?size:1))return value;throw std::bad_alloc();}
void operator delete(void* value) noexcept{std::free(value);}
void operator delete(void* value,size_t) noexcept{std::free(value);}
namespace{int checks=0;void check(bool value,const char* message){++checks;if(!value)throw std::runtime_error(message);}}
int main(){try{
    rocker::media::Clip stereo;stereo.sampleRate=8000;stereo.channels={std::vector<float>(8000,.2f),std::vector<float>(8000,-.2f)};rocker::media::Clip mono;mono.sampleRate=16000;mono.channels={std::vector<float>(8000,.1f)};
    rocker::media::ProjectTrackSpec first;first.gain=.5;rocker::media::ProjectTrackSpec second;second.pan=-1;second.offset=1000;second.trim=1000;
    std::vector<rocker::media::ProjectTrackAudio> tracks;tracks.push_back({first,std::move(stereo)});tracks.push_back({second,std::move(mono)});rocker::media::ProjectSource source(std::move(tracks),8000);std::array<float,2> values{};
    source.request(2000);check(source.read(2000,values)&&std::abs(values[0]-.2f)<1e-6&&std::abs(values[1]+.1f)<1e-6,"mixed gain, hard pan, source trim and unequal-rate resampling");check(source.frames()==8000 && source.channels()==2,"exact project extent includes source offsets and trims");
    source.update({1,1,-1,false,true});source.beginBlock();source.request(3000);source.read(3000,values);check(std::abs(values[0]-.1f)<1e-6&&values[1]==0,"solo preserves selected track and channel routing");source.update({1,1,-1,true,true});source.beginBlock();source.request(4000);source.read(4000,values);check(values[0]==0&&values[1]==0,"mute takes precedence over solo");check(source.ownsEnvelope(),"project source applies the same track envelope offline and live");
    source.update({0,1,0,false,true});source.update({1,1,-1,true,false});source.beginBlock();source.request(5000);source.read(5000,values);check(std::abs(values[0]-.2f)<1e-6&&std::abs(values[1]+.2f)<1e-6,"live parameter changes affect the same retained PCM without decoding");
    auditAllocations=true;for(uint64_t frame=5100;frame<5500;++frame){source.request(frame);source.read(frame,values);}source.update({0,.8,.2,false,true});source.beginBlock();auditAllocations=false;check(allocationCount==0,"project callback reads, prefetch requests and mixer command consumption do not allocate");
    rocker::media::Clip backing;backing.sampleRate=8000;backing.channels={std::vector<float>(16000,.1f)};
    rocker::RenderProcessor render(440,.01);render.prepare(8000);render.stage(&backing,1,1,true,0,77);render.enable(true);
    render.input().recording().arm({19,0,0,8000,1,77,8000});render.requestCountIn(1,4,240);
    std::array<float,131> input{},left{},right{};input.fill(.25f);const float* ins[]{input.data()};float* outs[]{left.data(),right.data()};
    for(int i=0;i<63;++i)render.process(ins,1,outs,2,131);
    auto alignment=render.input().recording().alignment();auto capture=render.input().recording().observation();
    check(alignment.ticket==77 && alignment.frame==0 && alignment.flags==7 && capture.firstFrame==8000 && capture.captured==253,"count-in excludes exact 8000 frames and aligns the first guitar sample to project frame zero across irregular blocks");
    render.requestSeek(.2,false);render.process(ins,1,outs,2,131);alignment=render.input().recording().alignment();check(alignment.failure==1 && render.input().recording().observation().failure==6,"changing the timeline explicitly ends an aligned take instead of silently shifting it");
    std::atomic<uint64_t> mediaIntent{3};render.mediaAuthority(mediaIntent);render.stage(&backing,1,2,false,0,2);render.process(ins,1,outs,2,131);check(render.acknowledgedClip()==1 && render.clipLease().consumed==2,"cancelled queued source is rejected at the callback boundary while ownership is acknowledged for safe cleanup");
    std::cout<<"PASS: "<<checks<<" project mixer routing, rate, trim, extent and live control assertions\n";return 0;
}catch(const std::exception& error){std::cerr<<error.what()<<'\n';return 1;}}
