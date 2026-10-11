// SPDX-License-Identifier: AGPL-3.0-only
#include "media_clip.h"
#include <vector>
#include <iostream>
#include <cmath>
#include <algorithm>
using Bytes=std::vector<uint8_t>;
static void check(bool value,const char* label){if(!value)throw std::runtime_error(label);}
static void delta(Bytes& out,uint32_t value){uint8_t bytes[4];int n=0;bytes[n++]=value&127;while((value>>=7)!=0)bytes[n++]=(value&127)|128;while(n)out.push_back(bytes[--n]);}
static Bytes midi(const std::vector<std::pair<uint32_t,Bytes>>& events){Bytes track;for(const auto& event:events){delta(track,event.first);track.insert(track.end(),event.second.begin(),event.second.end());}delta(track,0);track.insert(track.end(),{255,47,0});Bytes out={'M','T','h','d',0,0,0,6,0,0,0,1,1,224,'M','T','r','k',0,0,0,0};const uint32_t size=static_cast<uint32_t>(track.size());for(int i=0;i<4;++i)out[18+i]=(size>>(24-i*8))&255;out.insert(out.end(),track.begin(),track.end());return out;}
static double pitch(const rocker::media::Clip& clip,double begin,double end){uint64_t first=static_cast<uint64_t>(begin*clip.sampleRate),last=static_cast<uint64_t>(end*clip.sampleRate);int crossings=0;for(uint64_t i=first+1;i<last;++i)if(clip.channels[0][i-1]<0 && clip.channels[0][i]>=0)++crossings;return crossings/(end-begin);}
int main(){try{
    auto score=midi({{0,{255,81,3,7,161,32}},{0,{176,101,0}},{0,{176,100,0}},{0,{176,6,12}},{0,{144,69,110}},{480,{224,127,127}},{240,{224,0,64}},{240,{128,69,0}}});auto bent=rocker::media::synthesizeMidi(score.data(),static_cast<uint32_t>(score.size()));check(std::abs(pitch(bent,.1,.4)-440)<4 && std::abs(pitch(bent,.54,.72)-880)<7 && std::abs(pitch(bent,.79,.97)-440)<7,"pitch wheel follows RPN range and returns to the original note");check(std::abs(bent.frames()/bent.sampleRate-1.08)<1e-6,"bend preserves note-off timing and release");
    auto drums=midi({{0,{153,36,127}},{240,{137,36,0}},{0,{153,38,127}},{240,{137,38,0}}});auto a=rocker::media::synthesizeMidi(drums.data(),static_cast<uint32_t>(drums.size())),b=rocker::media::synthesizeMidi(drums.data(),static_cast<uint32_t>(drums.size()));double energy=0;for(float sample:a.channels[0]){check(std::isfinite(sample) && std::abs(sample)<=.8f,"synthetic percussion is finite and bounded");energy+=sample*sample;}check(energy>1 && a.channels==b.channels,"percussion produces deterministic actual samples without a soundfont");
    auto silent=midi({{0,{176,7,0}},{0,{144,69,127}},{480,{128,69,0}}});auto muted=rocker::media::synthesizeMidi(silent.data(),static_cast<uint32_t>(silent.size()));check(std::all_of(muted.channels[0].begin(),muted.channels[0].end(),[](float value){return value==0;}),"MIDI channel volume is respected");
    std::cout<<"PASS: RPN pitch bends, phase-preserving return, release timing, deterministic percussion and channel volume\n";return 0;
}catch(const std::exception& error){std::cerr<<error.what()<<'\n';return 1;}}
