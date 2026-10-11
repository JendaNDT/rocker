// SPDX-License-Identifier: AGPL-3.0-only
#include "recording_worker.h"
#include "media_clip.h"
#include <iostream>
namespace {int checks=0;void check(bool value,const char* message){++checks;if(!value)throw std::runtime_error(message);}}
int main(){try{
    const auto directory=std::filesystem::temp_directory_path()/("rocker-record-check-"+std::to_string(std::chrono::steady_clock::now().time_since_epoch().count()));std::filesystem::create_directory(directory);
    struct Cleanup{std::filesystem::path directory;~Cleanup(){std::error_code ignored;std::filesystem::remove_all(directory,ignored);}} cleanup{directory};
    {const auto path=directory/"odd-mono.wav";{rocker::WaveWriter writer(path,48000,1);const float sample=.125f;writer.append(&sample,1);writer.checkpoint();}check(std::filesystem::file_size(path)==48 && rocker::media::decode(path).frames()==1,"odd PCM24 mono payload has the required RIFF padding byte");}
    {rocker::RecordingTap tap;rocker::SampleClock clock;clock.prepare(48000,1);tap.arm({1,1,0,48000,1});tap.beginBlock(48000,1);tap.sample(.1,.2,.3f,.3f,clock.tick(),true,15);tap.sample(.1,.2,.3f,.3f,clock.tick(),true,0);tap.endBlock();check(tap.observation().dspAtStart==15 && tap.observation().dspChanged,"capture metadata retains initial DSP delay and records its change");tap.invalidSample();check(tap.observation().failure==4,"sanitized device samples cannot masquerade as a valid recording");}
    for(uint32_t bus=0;bus<3;++bus){
        rocker::RecordingTap tap;rocker::RecordingWorker worker(tap);rocker::SampleClock clock;clock.prepare(48000,7);clock.tempo(120);
        const auto path=directory/("bus-"+std::to_string(bus)+".wav");worker.start(path,bus+1,48000,bus==2?2:1,bus);
        check(tap.arm({bus+1,bus,0,48000,7}),"capture arms only after file preparation");
        for(int block=0;block<20;++block){tap.beginBlock(48000,7);clock.beginBlock();for(int i=0;i<127;++i)tap.sample(.25,.5,.125f,-.125f,clock.tick(),true);tap.endBlock();clock.publish();}
        worker.requestStop();tap.beginBlock(48000,7);tap.endBlock();check(worker.wait(std::chrono::seconds(2)),"disk consumer drains final partial packet");
        const auto result=worker.observation();check(result.state==4 && result.failure==0 && result.writtenFrames==2540 && result.capturedFrames==2540 && result.headerFinalized,"exact sample duration and successful durable finalization");
        const auto clip=rocker::media::decode(path);check(clip.frames()==2540 && clip.channels.size()==(bus==2?2u:1u),"primary PCM24 WAV plays through independent decoder");
        const auto expected=bus==0?.25f:bus==1?.5f:.125f;check(std::abs(clip.channels[0][100]-expected)<1e-6 && (bus!=2 || std::abs(clip.channels[1][100]+.125f)<1e-6),"dry/processed/mix isolation and stereo polarity");
        try{rocker::WaveWriter duplicate(path,48000,1);check(false,"existing take overwritten");}catch(const std::exception&){check(true,"exclusive creation preserves existing take");}
    }
    {
        rocker::RecordingTap tap;rocker::SampleClock clock;clock.prepare(48000,1);clock.tempo(120);tap.arm({1,0,4,48000,1});
        for(int block=0;block<187;++block){tap.beginBlock(48000,1);clock.beginBlock();for(int i=0;i<512;++i)tap.sample(.2,.3,.4f,.4f,clock.tick(),true);tap.endBlock();clock.publish();}
        tap.beginBlock(48000,1);clock.beginBlock();for(int i=0;i<256;++i)tap.sample(.2,.3,.4f,.4f,clock.tick(),true);tap.endBlock();clock.publish();
        check(tap.observation().captured==0,"count-in consumes exactly four beats without recording them");
        tap.beginBlock(48000,1);clock.beginBlock();for(int i=0;i<127;++i)tap.sample(.2,.3,.4f,.4f,clock.tick(),true);tap.endBlock();
        check(tap.observation().firstFrame==96000 && tap.observation().captured==127,"counted capture starts on the exact shared beat sample");
        tap.finishJoined(2);check(tap.observation().state==4 && tap.observation().failure==2,"epoch/device loss keeps explicit partial failure");
    }
    {
        rocker::RecordingTap tap;tap.arm({1,0,0,48000,1});tap.beginBlock(48000,1);
        for(uint64_t i=0;i<70000;++i)tap.sample(.2,.2,.2f,.2f,{i,0,false,0},true);tap.endBlock();
        check(tap.observation().state==4 && tap.observation().failure==1 && tap.queue().dropped()>0,"queue overrun fails instead of claiming a complete take");
    }
    {
        rocker::RecordingTap tap;rocker::RecordingWorker worker(tap);const auto path=directory/"bounded-disk.wav";worker.start(path,1,48000,1,0,44+256*3);tap.arm({1,0,0,48000,1});tap.beginBlock(48000,1);
        for(uint64_t i=0;i<512;++i)tap.sample(.25,.25,.25f,.25f,{i,0,false,0},true);tap.endBlock();tap.finishJoined();
        check(worker.wait(std::chrono::seconds(2)),"write-limit failure settles");auto result=worker.observation();
        check(result.state==5 && result.failure==5 && result.writtenFrames==256 && result.headerFinalized,"disk failure retains an explicitly incomplete playable prefix");
        check(rocker::media::decode(path).frames()==256,"failed take prefix is independently readable");
    }
    std::cout<<"PASS: "<<checks<<" recording bus, musical gate, durable WAV, overrun and disk failure assertions\n";return 0;
}catch(const std::exception& error){std::cerr<<error.what()<<'\n';return 1;}}
