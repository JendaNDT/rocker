// SPDX-License-Identifier: AGPL-3.0-only
#include "media_clip.h"
#include "media_decoders.h"
#include <algorithm>
#include <chrono>
#include <cmath>
#include <iostream>
#include <thread>
namespace {int checks=0;void check(bool value,const char* message){++checks;if(!value)throw std::runtime_error(message);}bool read(rocker::media::PcmSource& source,uint64_t frame,std::array<float,2>& value){source.request(frame);for(int i=0;i<1000;++i){if(source.read(frame,value))return true;std::this_thread::sleep_for(std::chrono::milliseconds(2));}return false;}}
int main(int argc,char** argv){try{
    check(argc==3,"short and long container fixtures required");std::atomic<uint64_t> generation{1};auto source=rocker::media::decode(argv[1]);
    for(double speed:{.5,1.,1.5}){auto prepared=rocker::media::prepareFile(argv[1],speed,generation,1);check(prepared.stream && prepared.channels.empty(),"container preparation retains only bounded disk-backed PCM");check(prepared.channelCount()==source.channelCount() && prepared.sampleRate==source.sampleRate && std::abs(prepared.frames()-source.frames()/speed)<=1,"container shape and stretch length preserved");std::array<float,2> value{};check(read(*prepared.stream,prepared.frames()/2,value) && std::isfinite(value[0]),"container midpoint seek yields finite PCM");if(speed==1){for(uint64_t at:{uint64_t{0},prepared.frames()/2,prepared.frames()-1}){check(read(*prepared.stream,at,value),"container seek reaches exact source frame");check(value[0]==source.channels[0][at] && value[1]==source.channels[source.channelCount()-1][at],"1x spool is sample-exact after delay/padding");}}}
    auto large=rocker::media::prepareFile(argv[2],1,generation,1);check(large.stream && large.channels.empty() && large.frames()*large.channelCount()*sizeof(float)>64ull*1024*1024,"long container exceeds old full-PCM budget without a complete vector");check(std::abs(large.frames()/large.sampleRate-400)<.1,"long container duration retained");std::array<float,2> value{};check(read(*large.stream,large.frames()-1,value) && std::isfinite(value[0]) && std::abs(value[0])<.001,"far end of long silent container is readable");
    generation.store(2);bool cancelled=false;try{rocker::media::prepareFile(argv[2],1,generation,1);}catch(const rocker::media::Failure&){cancelled=true;}check(cancelled,"cancelled long container is not published");
    int packets=0;bool midCancelled=false;auto sink=[](const float*,size_t,uint32_t,double){};auto cancel=[&]{if(++packets==5)throw rocker::media::Failure(rocker::media::Error::decodeFailed,"cancelled test worker");};try{if(std::filesystem::path(argv[2]).extension()==".webm")rocker::media::detail::decodeWebmChunks(argv[2],{1024ull*1024*1024,8ull*1024*1024*1024,8ull*1024*1024*1024},sink,cancel);else rocker::media::detail::decodeMp4Chunks(argv[2],{1024ull*1024*1024,8ull*1024*1024*1024,8ull*1024*1024*1024},sink,cancel);}catch(const rocker::media::Failure&){midCancelled=packets==5;}check(midCancelled,"container decoder checks cancellation between packets/samples");
    std::cout<<"PASS: "<<checks<<" container spool, long-file, exact PCM, seek and cancellation assertions\n";return 0;
}catch(const std::exception& error){std::cerr<<error.what()<<'\n';return 1;}}
