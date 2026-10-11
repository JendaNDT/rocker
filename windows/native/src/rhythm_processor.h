// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include "preview_processor.h"
namespace rocker {
struct RhythmCommand {uint64_t token=0;double roundBeats=0;uint32_t bpm=100;bool speedUp=false;PreviewPattern pattern;};
struct RhythmObservation {uint32_t state=0,epoch=0,bpm=100,flags=0,lastBpm=0;uint64_t token=0,startFrame=0,rounds=0;double elapsed=0,roundBeats=0;};
// Musical rounds and their 5% tempo changes are owned by the device callback.
// A single command installs both the round clock and its teaching accompaniment.
class RhythmProcessor {
public:
    void authority(const std::atomic<uint64_t>& trainer,const std::atomic<uint64_t>& preview) noexcept{trainerAuthority_=&trainer;previewAuthority_=&preview;}
    void prepare(uint32_t epoch) noexcept{epoch_=epoch;state_=0;publish();}
    bool arm(const RhythmCommand& value) noexcept{previewToken_.store(value.pattern.token);return commands_.push(value);}
    uint64_t previewToken() const noexcept{return previewToken_.load();}
    void beginBlock(SampleClock& clock,PreviewProcessor& preview) noexcept{
        RhythmCommand value;while(commands_.pop(value)){
            if((trainerAuthority_ && value.token!=trainerAuthority_->load()) || (previewAuthority_ && value.pattern.token!=previewAuthority_->load()))continue;
            if(!preview.installCallback(value.pattern))continue;
            current_=value;state_=1;startBeat_=UINT64_MAX;startFrame_=rounds_=0;elapsed_=0;lastBpm_=0;bpm_=value.bpm;clock.tempo(bpm_);
        }
    }
    void process(const GridTick& tick,SampleClock& clock) noexcept{
        if(state_!=1 && state_!=2)return;
        if((trainerAuthority_ && current_.token!=trainerAuthority_->load()) || (previewAuthority_ && current_.pattern.token!=previewAuthority_->load())){state_=3;return;}
        if(state_==1){if(startBeat_==UINT64_MAX && tick.quarter)startBeat_=tick.beat+current_.pattern.countInBeats;if(!tick.quarter || tick.beat<startBeat_)return;state_=2;startFrame_=tick.frame;}
        elapsed_=std::max(0.,tick.beat+tick.phase-static_cast<double>(startBeat_));
        const auto rounds=static_cast<uint64_t>(std::floor((elapsed_+1e-10)/current_.roundBeats));
        if(rounds>rounds_){lastBpm_=bpm_;rounds_=rounds;if(current_.speedUp){bpm_=static_cast<uint32_t>(std::clamp(std::floor(bpm_*1.05+.5),40.,240.));clock.tempoAfterTick(bpm_);}}
    }
    void endBlock() noexcept{publish();}
    void stopJoined() noexcept{RhythmCommand old;while(commands_.pop(old)){}if(state_==1 || state_==2)state_=3;publish();}
    RhythmObservation observation() const noexcept{return {statePublished_.load(),epochPublished_.load(),bpmPublished_.load(),flagsPublished_.load(),lastBpmPublished_.load(),tokenPublished_.load(),startPublished_.load(),roundsPublished_.load(),elapsedPublished_.load(),roundBeatsPublished_.load()};}
private:
    void publish() noexcept{epochPublished_.store(epoch_);bpmPublished_.store(bpm_);flagsPublished_.store(current_.speedUp?1u:0u);lastBpmPublished_.store(lastBpm_);tokenPublished_.store(current_.token);startPublished_.store(startFrame_);roundsPublished_.store(rounds_);elapsedPublished_.store(elapsed_);roundBeatsPublished_.store(current_.roundBeats);statePublished_.store(state_);}
    RealtimeQueue<RhythmCommand,4> commands_;RhythmCommand current_{};
    const std::atomic<uint64_t>* trainerAuthority_=nullptr;const std::atomic<uint64_t>* previewAuthority_=nullptr;
    std::atomic<uint64_t> previewToken_{0},tokenPublished_{0},startPublished_{0},roundsPublished_{0};
    std::atomic<uint32_t> statePublished_{0},epochPublished_{0},bpmPublished_{100},flagsPublished_{0},lastBpmPublished_{0};std::atomic<double> elapsedPublished_{0},roundBeatsPublished_{0};
    uint32_t state_=0,epoch_=0,bpm_=100,lastBpm_=0;uint64_t startBeat_=UINT64_MAX,startFrame_=0,rounds_=0;double elapsed_=0;
};
}
