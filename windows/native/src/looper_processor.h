// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include "sample_clock.h"
#include "realtime_queue.h"
#include <array>
#include <vector>
#include <cstdint>
#include <atomic>
namespace rocker {
struct LoopBuffer {
    static constexpr uint32_t maximumLayers=8;
    static constexpr uint64_t maximumBytes=64ull*1024*1024;
    explicit LoopBuffer(uint64_t capacity=maximumBytes/(maximumLayers*sizeof(float))):capacity(capacity),samples(capacity*maximumLayers){}
    float& sample(uint32_t layer,uint64_t frame) noexcept{return samples[layer*capacity+frame];}
    const float& sample(uint32_t layer,uint64_t frame) const noexcept{return samples[layer*capacity+frame];}
    const uint64_t capacity;std::vector<float> samples;
};
struct LoopCommand {uint32_t kind=0,bus=0,beats=4,countIn=0,delay=0,layer=0;bool aligned=false;double gain=.7;uint64_t sequence=0,frames=0;uint32_t rate=0,flags=0;LoopBuffer* buffer=nullptr;};
struct LoopObservation {uint32_t state=0,failure=0,layers=0,bus=0,rate=0,flags=0,delay=0;uint64_t frames=0,position=0,cycles=0,start=0,ack=0,capacity=0;std::array<float,8> gains{};};
// One callback producer owns all layer writes. Buffer allocation and file I/O
// remain on coordinators; a paused session permits read-only offline export.
class LooperProcessor {
public:
    bool command(LoopCommand value) noexcept{value.sequence=submitted_+1;if(!commands_.push(value))return false;++submitted_;return true;}
    bool pending() const noexcept{return submitted_!=ackPublished_.load();}
    void prepare(double rate,uint32_t epoch) noexcept{joinedStop(5);deviceRate_=static_cast<uint32_t>(rate);epoch_=epoch;if(layers_ && loopRate_!=deviceRate_)failure_=5;publish();}
    void beginBlock() noexcept{LoopCommand value;while(commands_.pop(value)){apply(value);ack_=value.sequence;}publish();}
    double process(double raw,double processed,bool hasInput,const GridTick& tick) noexcept{
        if(state_==0 || state_==6 || state_==7){lastOutput_*=std::max(0.,1-1/(std::max(1u,deviceRate_)*.01));return lastOutput_;}
        if(!buffer_ || (layers_ && loopRate_!=deviceRate_)){joinedStop(5);return 0;}
        if((state_==1 || state_==2 || state_==4 || state_==5) && (!hasInput || !std::isfinite(raw) || !std::isfinite(processed))){joinedStop(hasInput?4:3);return 0;}
        if(state_==1){
            if(waitingQuarter_){if(!tick.quarter)return 0;startBeat_=tick.beat+countIn_;if(aligned_)startBeat_+=(beats_-startBeat_%beats_)%beats_;waitingQuarter_=false;}
            if((countIn_||aligned_) && (!tick.quarter || tick.beat<startBeat_))return 0;
            state_=2;loopRate_=deviceRate_;start_=tick.frame;position_=0;
        }
        if(state_==2){
            if(endRequested_ && (!aligned_ || (tick.quarter && tick.beat%beats_==0 && position_>0))){finishFirst(0);}
            else{
                if(position_>=buffer_->capacity){finishFirst(1);return 0;}
                buffer_->sample(0,position_++)=static_cast<float>(bus_==0?raw:processed);return 0;
            }
        }
        if(state_==4 && position_==0){state_=5;overdubFrames_=0;}
        double mixed=0;const auto fade=std::max(1u,loopRate_/200);
        const auto edge=std::min(1.,std::min(position_/(double)fade,(frames_-1-position_)/(double)fade));
        for(uint32_t layer=0;layer<layers_;++layer)mixed+=buffer_->sample(layer,position_)*gains_[layer]*edge;
        if(state_==5){const auto destination=(position_+frames_-delay_%frames_)%frames_;buffer_->sample(layers_,destination)=static_cast<float>(bus_==0?raw:processed);++overdubFrames_;}
        ++position_;if(position_==frames_){position_=0;++cycles_;if(state_==5 && overdubFrames_==frames_){++layers_;state_=3;}}
        playbackGain_=std::min(1.,playbackGain_+1/(std::max(1u,loopRate_)*.01));lastOutput_=mixed*playbackGain_;return lastOutput_;
    }
    void invalidSample() noexcept{if(state_==1 || state_==2 || state_==4 || state_==5)joinedStop(4);}
    void stopJoined(uint32_t reason=0) noexcept{beginBlock();joinedStop(reason);}
    void endBlock() noexcept{publish();}
    void joinedStop(uint32_t reason=0) noexcept{
        if(state_==2){finishFirst(reason?reason:2);}
        else if(state_==1){state_=0;failure_=reason;}
        else if(state_==4 || state_==5){state_=6;failure_=reason?reason:2;}
        else if(state_==3){state_=6;if(reason)failure_=reason;}
        lastOutput_=0;publish();
    }
    bool active() const noexcept{const auto state=statePublished_.load();return state>=1 && state<=5;}
    LoopObservation observation() const noexcept{
        LoopObservation result{statePublished_.load(),failurePublished_.load(),layersPublished_.load(),busPublished_.load(),ratePublished_.load(),flagsPublished_.load(),delayPublished_.load(),framesPublished_.load(),positionPublished_.load(),cyclesPublished_.load(),startPublished_.load(),ackPublished_.load(),capacityPublished_.load()};for(size_t i=0;i<8;++i)result.gains[i]=gainsPublished_[i].load();return result;
    }
    // Only call on the coordinator after a paused/empty/failed observation and
    // the pause command acknowledgement. All callback writes have then stopped.
    double offlineSample(uint64_t frame,int layer=-1) const noexcept{
        if(!buffer_ || frame>=frames_)return 0;
        if(layer>=0)return layer<static_cast<int>(layers_)?buffer_->sample(static_cast<uint32_t>(layer),frame):0;
        double result=0;const auto fade=std::max(1u,loopRate_/200);const auto edge=std::min(1.,std::min(frame/(double)fade,(frames_-1-frame)/(double)fade));for(uint32_t i=0;i<layers_;++i)result+=buffer_->sample(i,frame)*gains_[i]*edge;return std::clamp(result,-1.,1.);
    }
private:
    void apply(const LoopCommand& value) noexcept{
        if(value.kind==0){if(state_==0){buffer_=value.buffer;capacity_=buffer_?buffer_->capacity:0;}return;}
        if(value.kind==1){if(state_!=0 || !buffer_)return;bus_=value.bus;beats_=std::max(1u,value.beats);countIn_=value.countIn*beats_;aligned_=value.aligned;waitingQuarter_=aligned_||countIn_>0;endRequested_=false;failure_=flags_=0;gains_[0]=.7f;state_=1;return;}
        if(value.kind==2){if(state_==2)endRequested_=true;else if(state_==1){state_=0;failure_=2;}return;}
        if(value.kind==3){if(state_!=3 || layers_>=8)return;bus_=value.bus;delay_=value.delay;flags_&=~4u;gains_[layers_]=.7f;state_=4;return;}
        if(value.kind==4){const auto previous=lastOutput_;joinedStop();lastOutput_=previous;playbackGain_=0;return;}
        if(value.kind==5){if(state_==6 && frames_ && loopRate_==deviceRate_){state_=3;failure_=0;playbackGain_=0;}else if(frames_ && loopRate_!=deviceRate_)failure_=5;return;}
        if(value.kind==6){if(state_==4||state_==5){state_=3;return;}if((state_==3||state_==6) && layers_){--layers_;if(!layers_){frames_=position_=cycles_=0;state_=0;}return;}return;}
        if(value.kind==7){state_=0;frames_=position_=cycles_=start_=0;layers_=failure_=flags_=0;lastOutput_=0;return;}
        if(value.kind==10){bus_=value.bus;delay_=value.delay;flags_=(flags_&2u)|(value.flags&5u);return;}
        if(value.kind==9 && value.layer<8 && value.frames && buffer_ && value.frames<=buffer_->capacity){if(value.layer==0){layers_=0;frames_=value.frames;loopRate_=value.rate;cycles_=position_=start_=0;}if(value.layer==layers_ && frames_==value.frames && loopRate_==value.rate){gains_[layers_++]=static_cast<float>(value.gain);state_=6;failure_=0;flags_=2;playbackGain_=0;}return;}
        if(value.kind==8 && value.layer<layers_)gains_[value.layer]=static_cast<float>(std::clamp(value.gain,0.,1.));
    }
    void finishFirst(uint32_t failure) noexcept{frames_=position_;position_=0;cycles_=0;failure_=failure;layers_=frames_?1u:0u;state_=frames_?(failure?6u:3u):0u;if(failure)flags_|=1;endRequested_=false;playbackGain_=0;}
    void publish() noexcept{failurePublished_.store(failure_);layersPublished_.store(layers_);busPublished_.store(bus_);ratePublished_.store(loopRate_);flagsPublished_.store(flags_);delayPublished_.store(delay_);framesPublished_.store(frames_);positionPublished_.store(position_);cyclesPublished_.store(cycles_);startPublished_.store(start_);ackPublished_.store(ack_);capacityPublished_.store(capacity_);for(size_t i=0;i<8;++i)gainsPublished_[i].store(gains_[i]);statePublished_.store(state_,std::memory_order_release);}
    LoopBuffer* buffer_=nullptr;RealtimeQueue<LoopCommand,64> commands_;uint64_t submitted_=0,ack_=0,capacity_=0,frames_=0,position_=0,cycles_=0,start_=0,startBeat_=0,overdubFrames_=0;
    uint32_t deviceRate_=48000,loopRate_=0,epoch_=0,state_=0,failure_=0,layers_=0,bus_=0,flags_=0,beats_=4,countIn_=0,delay_=0;bool waitingQuarter_=false,aligned_=false,endRequested_=false;double lastOutput_=0,playbackGain_=0;std::array<float,8> gains_{};
    std::atomic<uint32_t> statePublished_{0},failurePublished_{0},layersPublished_{0},busPublished_{0},ratePublished_{0},flagsPublished_{0},delayPublished_{0};
    std::atomic<uint64_t> framesPublished_{0},positionPublished_{0},cyclesPublished_{0},startPublished_{0},ackPublished_{0},capacityPublished_{0};std::array<std::atomic<float>,8> gainsPublished_{};
};
}
