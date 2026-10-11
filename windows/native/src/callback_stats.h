// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include <atomic>
#include <chrono>
#include <algorithm>
#include <cstdint>
namespace rocker {
struct CallbackObservation{uint64_t callbacks=0,frames=0,totalNanoseconds=0,lastNanoseconds=0,maxNanoseconds=0,overBudget=0,invalidCallbacks=0;uint32_t minFrames=0,maxFrames=0;double rate=0;};
// One callback producer; resets only after the driver joins. Query is cached
// atomics. No wall-clock/logging/driver query is performed by the UI.
class CallbackStats {
public:
    using Clock=std::chrono::steady_clock;
    void prepare(double rate) noexcept{local_={};local_.rate=rate;publish();}
    void completed(Clock::time_point before,Clock::time_point after,int frames) noexcept{
        if(frames<=0){++local_.invalidCallbacks;publish();return;}
        const auto ns=std::chrono::duration_cast<std::chrono::nanoseconds>(after-before).count();const auto duration=static_cast<uint64_t>(std::max(int64_t{0},static_cast<int64_t>(ns)));
        ++local_.callbacks;local_.frames+=static_cast<uint64_t>(frames);local_.totalNanoseconds+=duration;local_.lastNanoseconds=duration;local_.maxNanoseconds=std::max(local_.maxNanoseconds,duration);
        if(local_.rate>0 && duration>frames/local_.rate*1e9)++local_.overBudget;
        const auto size=static_cast<uint32_t>(frames);local_.minFrames=local_.minFrames?std::min(local_.minFrames,size):size;local_.maxFrames=std::max(local_.maxFrames,size);publish();
    }
    CallbackObservation observation() const noexcept{return {callbacks_.load(),frames_.load(),total_.load(),last_.load(),maximum_.load(),overBudget_.load(),invalid_.load(),minFrames_.load(),maxFrames_.load(),rate_.load()};}
private:
    void publish() noexcept{callbacks_.store(local_.callbacks);frames_.store(local_.frames);total_.store(local_.totalNanoseconds);last_.store(local_.lastNanoseconds);maximum_.store(local_.maxNanoseconds);overBudget_.store(local_.overBudget);invalid_.store(local_.invalidCallbacks);minFrames_.store(local_.minFrames);maxFrames_.store(local_.maxFrames);rate_.store(local_.rate);}
    CallbackObservation local_;std::atomic<uint64_t> callbacks_{0},frames_{0},total_{0},last_{0},maximum_{0},overBudget_{0},invalid_{0};std::atomic<uint32_t> minFrames_{0},maxFrames_{0};std::atomic<double> rate_{0};
};
}
