// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include <atomic>
#include <algorithm>
#include <cmath>
#include <cstdint>
namespace rocker {
struct ClockObservation {uint64_t frame=0,beat=0;uint32_t epoch=0;double rate=0,phase=0,bpm=100;};
struct GridTick {uint64_t frame=0,beat=0;bool quarter=false;double phase=0;};
// The device callback owns phase and frame. Fractional phase is carried between
// blocks, never rounding each beat's duration to a whole buffer or sample.
class SampleClock {
public:
    void prepare(double rate,uint32_t epoch) noexcept {rate_=rate;epoch_=epoch;frame_=beat_=0;phase_=0;first_=true;publish();}
    void tempo(double bpm) noexcept {targetBpm_.store(bpm);}
    double currentBpm() const noexcept{return bpm_;}
    void tempoAfterTick(double bpm) noexcept {const double next=bpm/(60*rate_);phase_+=next-increment_;bpm_=bpm;increment_=next;targetBpm_.store(bpm);}
    void beginBlock() noexcept {bpm_=targetBpm_.load();increment_=bpm_/(60*rate_);}
    GridTick tick() noexcept {
        bool quarter=first_;first_=false;
        if(phase_>=1-1e-12){phase_=std::max(0.0,phase_-1);++beat_;quarter=true;}
        const GridTick result{frame_,beat_,quarter,phase_};++frame_;phase_+=increment_;return result;
    }
    void publish() noexcept {publishedFrame_.store(frame_);publishedBeat_.store(beat_);publishedEpoch_.store(epoch_);publishedRate_.store(rate_);publishedPhase_.store(std::min(phase_,1.0));publishedBpm_.store(bpm_);}
    ClockObservation observation() const noexcept {return {publishedFrame_.load(),publishedBeat_.load(),publishedEpoch_.load(),publishedRate_.load(),publishedPhase_.load(),publishedBpm_.load()};}
private:
    double rate_=48000,phase_=0,bpm_=100,increment_=100.0/(60*48000);uint64_t frame_=0,beat_=0;uint32_t epoch_=0;bool first_=true;
    std::atomic<double> targetBpm_{100},publishedRate_{0},publishedPhase_{0},publishedBpm_{100};
    std::atomic<uint64_t> publishedFrame_{0},publishedBeat_{0};std::atomic<uint32_t> publishedEpoch_{0};
};
}
