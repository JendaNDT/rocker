// SPDX-License-Identifier: AGPL-3.0-only
#include "rocker_audio.h"
#include "media_clip.h"
#include <chrono>
#include <filesystem>
#include <iostream>
extern "C" void ROCKER_CALL rocker_test_process(uint32_t,float);
namespace {int checks=0;void check(bool value,const char* message){++checks;if(!value)throw std::runtime_error(message);}}
int main(){try{
    const auto directory=std::filesystem::temp_directory_path()/("rocker-record-contract-"+std::to_string(std::chrono::steady_clock::now().time_since_epoch().count()));std::filesystem::create_directory(directory);
    struct Cleanup{std::filesystem::path path;~Cleanup(){std::error_code ignored;std::filesystem::remove_all(path,ignored);}} cleanup{directory};
    rocker_audio_config config{sizeof(config),ROCKER_AUDIO_ABI_VERSION,440,.02};rocker_audio_handle host{};check(rocker_audio_create(&config,&host)==ROCKER_OK,"create recording host");
    struct Host{rocker_audio_handle handle;~Host(){rocker_audio_destroy(handle);}} own{host};
    rocker_device_config device{sizeof(device),ROCKER_AUDIO_ABI_VERSION,48000,128,0,0,0};check(rocker_audio_configure_device(host,&device,"Test ASIO","Test interface","Test interface")==ROCKER_OK,"select one physical guitar input");
    uint64_t graph=0,take=0;check(rocker_audio_reserve_recording(host,&graph,&take)==ROCKER_OK,"reserve on calling thread");const auto path=directory/"selected-input.wav";
    check(rocker_audio_start_recording_reserved(host,graph,take,path.string().c_str(),0,0)==ROCKER_OK,"start dry take without enabling monitoring");
    for(int i=0;i<20;++i)rocker_test_process(127,.25f);
    rocker_audio_snapshot audioBefore{sizeof(audioBefore),ROCKER_AUDIO_ABI_VERSION};rocker_audio_get_snapshot(host,&audioBefore);
    check(rocker_audio_request_recording_stop(host)==ROCKER_OK,"immediate take stop request");rocker_test_process(64,.25f);
    check(rocker_audio_finish_recording(host)==ROCKER_OK,"drain and finalize disk worker");
    rocker_recording_snapshot record{sizeof(record),ROCKER_AUDIO_ABI_VERSION};check(rocker_audio_get_recording(host,&record)==ROCKER_OK && record.state==4 && record.failure==0 && record.written_frames==2540 && record.captured_frames==2540 && record.channels==1 && record.sample_rate==48000 && record.token==take && (record.flags&1),"C ABI exact sample count, format, token and completion");
    const auto exported=directory/"exported-range.wav";check(rocker_audio_export_wav(path.string().c_str(),exported.string().c_str(),.01,.03,nullptr,nullptr,nullptr)==ROCKER_OK,"offline WAV range export");auto range=rocker::media::decode(exported);check(range.frames()==960 && std::abs(range.channels[0][100]-.25)<1e-6,"exported range has exact frames and source samples");
    const auto cancelPath=directory/"cancelled-export.wav";check(rocker_audio_export_wav(path.string().c_str(),cancelPath.string().c_str(),0,0,[](void*)->uint32_t{return 1;},nullptr,nullptr)==ROCKER_NOT_READY && !std::filesystem::exists(cancelPath),"cancelled export never overwrites destination");
    auto pcm=rocker::media::decode(path);check(pcm.frames()==2540 && std::abs(pcm.channels[0][100]-.25)<1e-6,"actual dry callback bus reaches independent WAV reader");
    rocker_audio_snapshot audioAfter{sizeof(audioAfter),ROCKER_AUDIO_ABI_VERSION};rocker_audio_get_snapshot(host,&audioAfter);check(audioAfter.state==ROCKER_STOPPED || audioAfter.sample_rate==audioBefore.sample_rate,"recording completion does not create another playback device");
    const auto cancelled=directory/"cancelled.wav";check(rocker_audio_reserve_recording(host,&graph,&take)==ROCKER_OK && rocker_audio_request_recording_stop(host)==ROCKER_OK,"cancel reserved take before worker executes");check(rocker_audio_start_recording_reserved(host,graph,take,cancelled.string().c_str(),0,0)==ROCKER_OK && !std::filesystem::exists(cancelled),"cancelled queued start never creates a file or begins capture");
    check(rocker_audio_reserve_recording(host,&graph,&take)==ROCKER_OK,"reserve second take");const auto stopped=directory/"global-stop.wav";check(rocker_audio_start_recording_reserved(host,graph,take,stopped.string().c_str(),1,0)==ROCKER_OK,"start processed take");rocker_test_process(127,.125f);check(rocker_audio_stop(host)==ROCKER_OK,"global stop joins callbacks then flushes final partial block");
    rocker_audio_get_recording(host,&record);check(record.state==4 && record.written_frames==127 && record.bus==1 && record.channels==1,"global stop finishes a 127-frame take without changing its bus");
    std::cout<<"PASS: "<<checks<<" recording C ABI, actual callback bus, cancellation and global-stop assertions\n";return 0;
}catch(const std::exception& error){std::cerr<<error.what()<<'\n';return 1;}}
