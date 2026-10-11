// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include "realtime_queue.h"
#include <algorithm>
#include <cmath>
namespace rocker {
struct BusMeters {
    double inputPeak=0,inputRms=0,processedPeak=0,processedRms=0,mixPeak=0,mixRms=0;
    uint64_t invalid=0,inputClips=0,outputClips=0,dropped=0,frame=0;
    uint32_t epoch=0;
};
// Called only by the single device callback. All control fields and observations
// are lock-free scalar atomics; analysis storage is allocated with the graph.
class InputGraph {
public:
    void prepare(double rate,uint32_t epoch) noexcept {rate_=rate;epoch_=epoch;gain_=0;inputPresent_.store(false);frame_=0;peak_[0].store(0);peak_[1].store(0);peak_[2].store(0);rms_[0].store(0);rms_[1].store(0);rms_[2].store(0);}
    void monitoring(bool enabled,double gain) noexcept {gainTarget_.store(gain);monitor_.store(enabled);}
    bool monitoring() const noexcept{return monitor_.load();}
    bool hasInput() const noexcept{return inputPresent_.load();}
    BusMeters meters() const noexcept {return {peak_[0].load(),rms_[0].load(),peak_[1].load(),rms_[1].load(),peak_[2].load(),rms_[2].load(),invalid_.load(),inputClips_.load(),outputClips_.load(),analysis_.dropped(),publishedFrame_.load(),publishedEpoch_.load()};}
    AnalysisQueue& analysis() noexcept{return analysis_;}
    void process(const float* input,float* const* outputs,int channels,int frames) noexcept {
        if(!outputs || channels<=0 || frames<=0)return;
        inputPresent_.store(input!=nullptr);
        double peaks[3]{},energy[3]{};uint64_t invalid=0,inputClips=0,outputClips=0;
        const double target=monitor_.load()?gainTarget_.load():0;
        AnalysisBlock block;block.epoch=epoch_;block.sampleRate=rate_;block.firstFrame=frame_;
        for(int i=0;i<frames;++i){
            double raw=input?input[i]:0;
            if(!std::isfinite(raw)){raw=0;++invalid;}else if(std::abs(raw)>=1)++inputClips;
            raw=std::clamp(raw,-1.0,1.0);
            // W17 replaces the explicit unity processed bus with the amplifier.
            const double processed=raw;
            gain_+=std::clamp(target-gain_,-1.0/(rate_*0.01),1.0/(rate_*0.01));
            peaks[0]=std::max(peaks[0],std::abs(raw));energy[0]+=raw*raw;
            peaks[1]=std::max(peaks[1],std::abs(processed));energy[1]+=processed*processed;
            double mixPeak=0,mixEnergy=0;int present=0;
            for(int c=0;c<channels;++c)if(outputs[c]){
                double mix=outputs[c][i]+(c<2?processed*gain_:0);
                if(!std::isfinite(mix)){mix=0;++invalid;}if(std::abs(mix)>1)++outputClips;
                const float safe=static_cast<float>(std::clamp(mix,-1.0,1.0));outputs[c][i]=safe;
                mixPeak=std::max(mixPeak,std::abs(static_cast<double>(safe)));mixEnergy+=safe*safe;++present;
            }
            peaks[2]=std::max(peaks[2],mixPeak);energy[2]+=present?mixEnergy/present:0;
            if(input){block.mono[block.count++]=static_cast<float>(raw);if(block.count==block.mono.size()){analysis_.push(block);block.count=0;block.firstFrame=frame_+static_cast<uint64_t>(i)+1;}}
        }
        if(block.count)analysis_.push(block);
        frame_+=static_cast<uint64_t>(frames);publishedFrame_.store(frame_);publishedEpoch_.store(epoch_);
        invalid_.fetch_add(invalid);inputClips_.fetch_add(inputClips);outputClips_.fetch_add(outputClips);
        for(int b=0;b<3;++b){peak_[b].store(peaks[b]);rms_[b].store(std::sqrt(energy[b]/frames));}
    }
private:
    AnalysisQueue analysis_;
    double rate_=48000,gain_=0;uint32_t epoch_=0;uint64_t frame_=0;
    std::atomic<bool> monitor_{false},inputPresent_{false};std::atomic<double> gainTarget_{0.15};
    std::atomic<double> peak_[3]{},rms_[3]{};
    std::atomic<uint64_t> invalid_{0},inputClips_{0},outputClips_{0},publishedFrame_{0};std::atomic<uint32_t> publishedEpoch_{0};
};
}
