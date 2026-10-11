// SPDX-License-Identifier: AGPL-3.0-only
#include "rocker_audio.h"
#include "media_clip.h"
#include <chrono>
#include <filesystem>
#include <iostream>
#include <thread>
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
    check(rocker_audio_reserve_metronome_start(host,&graph)==ROCKER_OK,"reserve looper join");check(rocker_audio_loop_command(host,graph,1,0,0,4,0,0,0,.7)==ROCKER_OK,"start looper on the same selected input");rocker_test_process(672,.25f);check(rocker_audio_loop_command(host,0,2,0,0,4,0,0,0,.7)==ROCKER_OK,"end free loop");rocker_test_process(1,0);
    rocker_loop_snapshot loop{sizeof(loop),ROCKER_AUDIO_ABI_VERSION};for(int retry=0;retry<100;++retry){rocker_audio_get_loop(host,&loop);if(loop.layers==1)break;std::this_thread::sleep_for(std::chrono::milliseconds(5));}check(loop.layers==1 && loop.frames==800 && loop.state==3,"actual 128 plus 672 input callback samples define loop length");
    check(rocker_audio_reserve_metronome_start(host,&graph)==ROCKER_OK && rocker_audio_loop_command(host,graph,3,0,0,4,0,32,0,.7)==ROCKER_OK,"arm second layer without reopening device");rocker_test_process(799,0);rocker_test_process(800,.125f);for(int retry=0;retry<100;++retry){rocker_audio_get_loop(host,&loop);if(loop.layers==2)break;std::this_thread::sleep_for(std::chrono::milliseconds(5));}check(loop.layers==2 && loop.frames==800,"overdub layer has exactly the first loop sample length");check(rocker_audio_stop(host)==ROCKER_OK,"global stop pauses looper without deleting layers");rocker_audio_get_loop(host,&loop);check(loop.state==6 && loop.layers==2,"paused layers survive stopped device");
    const auto layerFile=directory/"loop-layer.wav";check(rocker_audio_export_loop(host,layerFile.string().c_str(),1)==ROCKER_OK,"export paused layer without opening a device");auto layerPcm=rocker::media::decode(layerFile);check(layerPcm.frames()==800 && std::abs(layerPcm.channels[0][100]-.125f)<1e-6,"independent layer WAV preserves exact length and source bus");
    check(rocker_audio_load_loop_layer(host,layerFile.string().c_str(),0,.4)==ROCKER_OK,"restore first layer into paused session");rocker_audio_get_loop(host,&loop);check(loop.layers==1 && loop.state==6 && loop.frames==800 && std::abs(loop.gains[0]-.4f)<1e-6,"restored session preserves exact rate, length and gain without playing");
    std::cout<<"PASS: "<<checks<<" recording C ABI, actual callback bus, cancellation and global-stop assertions\n";return 0;
}catch(const std::exception& error){std::cerr<<error.what()<<'\n';return 1;}}
