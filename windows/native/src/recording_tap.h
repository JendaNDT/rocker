// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include "realtime_queue.h"
#include "sample_clock.h"
#include <algorithm>
#include <array>
#include <atomic>
#include <cmath>
#include <memory>
namespace rocker {
struct RecordingBlock {
    uint64_t token=0,firstFrame=0;uint32_t epoch=0,frames=0,channels=0;double sampleRate=0;
    std::array<float,256*2> samples{};
};
using RecordingQueue=RealtimeQueue<RecordingBlock,256>;
struct RecordingArm {uint64_t token=0;uint32_t bus=0,countInBeats=0;double rate=0;uint32_t epoch=0;};
struct RecordingTapObservation {uint64_t token=0,captured=0,firstFrame=0;uint32_t state=0,failure=0,epoch=0;double rate=0;uint32_t dspAtStart=0;bool dspChanged=false;};
// Callback-owned packing and musical start gate. The disk worker is the sole
// consumer. Full queues fail the take and never overwrite unread audio.
class RecordingTap {
public:
    bool arm(RecordingArm value) noexcept {
        desiredToken_.store(value.token,std::memory_order_release);
        if(commands_.push(value))return true;
        desiredToken_.store(0,std::memory_order_release);return false;
    }
    void requestStop(uint64_t token) noexcept{stopToken_.store(token,std::memory_order_release);}
    void beginBlock(double rate,uint32_t epoch) noexcept {
        consumeCommands();
        if(state_==1 || state_==2){
            if(rate!=rate_ || epoch!=epoch_)finish(2);
            else if(stopToken_.load()==token_ || desiredToken_.load()!=token_)finish(0);
        }
        publish();
    }
    void sample(double raw,double processed,float left,float right,const GridTick& tick,bool input,uint32_t dspFrames=0) noexcept {
        if(state_!=1 && state_!=2)return;
        if(bus_<2 && !input){finish(3);return;}
        if(state_==1){
            if(waitingQuarter_){if(!tick.quarter)return;startBeat_=tick.beat+countInBeats_;waitingQuarter_=false;}
            if(countInBeats_ && (!tick.quarter || tick.beat<startBeat_))return;
            state_=2;firstFrame_=tick.frame;dspAtStart_=bus_==0?0:dspFrames;
        }
        if(bus_!=0 && dspFrames!=dspAtStart_)dspChanged_=true;
        if(chunk_.frames==0){chunk_.token=token_;chunk_.firstFrame=tick.frame;chunk_.epoch=epoch_;chunk_.sampleRate=rate_;chunk_.channels=bus_==2?2u:1u;}
        if(!std::isfinite(raw) || !std::isfinite(processed) || !std::isfinite(left) || !std::isfinite(right)){finish(4);return;}
        const auto at=chunk_.frames*chunk_.channels;
        chunk_.samples[at]=static_cast<float>(std::clamp(bus_==0?raw:bus_==1?processed:static_cast<double>(left),-1.,1.));
        if(chunk_.channels==2)chunk_.samples[at+1]=std::clamp(right,-1.f,1.f);
        ++chunk_.frames;++captured_;
        if(chunk_.frames==256)flush();
    }
    void invalidSample() noexcept{if(state_==1 || state_==2)finish(4);}
    void endBlock() noexcept{publish();}
    // Only after the device callback has joined. This transfers producer duties
    // to the coordinator while the consumer may drain the final partial block.
    void finishJoined(uint32_t failure=0) noexcept{consumeCommands();if(state_==1 || state_==2)finish(failure);publish();}
    bool active() const noexcept{const auto state=statePublished_.load();return state==1 || state==2;}
    RecordingTapObservation observation() const noexcept{const auto state=statePublished_.load(std::memory_order_acquire);return {tokenPublished_.load(),capturedPublished_.load(),firstPublished_.load(),state,failurePublished_.load(),epochPublished_.load(),ratePublished_.load(),dspPublished_.load(),dspChangedPublished_.load()};}
    RecordingQueue& queue() noexcept{return *queue_;}
private:
    void consumeCommands() noexcept {
        RecordingArm value;while(commands_.pop(value)){
            if(value.token!=desiredToken_.load())continue;
            token_=value.token;bus_=value.bus;countInBeats_=value.countInBeats;rate_=value.rate;epoch_=value.epoch;
            captured_=firstFrame_=0;dspAtStart_=0;dspChanged_=false;chunk_={};state_=1;failure_=0;waitingQuarter_=countInBeats_>0;startBeat_=0;
        }
    }
    void flush() noexcept {
        if(!chunk_.frames)return;
        if(!queue_->push(chunk_)){failure_=1;state_=4;desiredToken_.store(0,std::memory_order_release);}
        chunk_.frames=0;
    }
    void finish(uint32_t failure) noexcept{flush();if(failure_==0)failure_=failure;state_=failure_?4u:3u;publish();}
    void publish() noexcept{tokenPublished_.store(token_);capturedPublished_.store(captured_);firstPublished_.store(firstFrame_);epochPublished_.store(epoch_);ratePublished_.store(rate_);failurePublished_.store(failure_);dspPublished_.store(dspAtStart_);dspChangedPublished_.store(dspChanged_);statePublished_.store(state_,std::memory_order_release);}
    std::unique_ptr<RecordingQueue> queue_=std::make_unique<RecordingQueue>();RealtimeQueue<RecordingArm,64> commands_;RecordingBlock chunk_{};
    uint64_t token_=0,captured_=0,firstFrame_=0,startBeat_=0;uint32_t bus_=0,countInBeats_=0,epoch_=0,state_=0,failure_=0;double rate_=0;uint32_t dspAtStart_=0;bool dspChanged_=false;bool waitingQuarter_=false;
    std::atomic<uint64_t> desiredToken_{0},stopToken_{0},tokenPublished_{0},capturedPublished_{0},firstPublished_{0};
    std::atomic<uint32_t> dspPublished_{0};std::atomic<bool> dspChangedPublished_{false};
    std::atomic<uint32_t> statePublished_{0},failurePublished_{0},epochPublished_{0};std::atomic<double> ratePublished_{0};
};
}
