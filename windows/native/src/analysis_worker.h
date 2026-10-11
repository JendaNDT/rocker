// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include "analysis_processor.h"
#include <thread>
#include <mutex>
#include <chrono>
namespace rocker {
struct AnalysisReport{AnalysisObservation observed;double ageSeconds=0,calibrationProgress=0,recommendedGate=0;uint32_t calibrationFlags=0;uint64_t calibrationToken=0;};
class AnalysisWorker {
public:
    explicit AnalysisWorker(AnalysisQueue& queue):queue_(queue),worker_([this](std::stop_token token){run(token);}){}
    void gate(double value) noexcept{gate_.store(value);}
    uint64_t calibrate() noexcept{return calibrationRequested_.fetch_add(1)+1;}
    AnalysisReport report(){const std::lock_guard lock(mutex_);auto copy=report_;const auto requested=calibrationRequested_.load();if(copy.calibrationToken!=requested){copy.calibrationToken=requested;copy.calibrationFlags=8u;copy.calibrationProgress=0;}copy.ageSeconds=published_==Clock::time_point{}?std::numeric_limits<double>::infinity():std::chrono::duration<double>(Clock::now()-published_).count();return copy;}
private:
    using Clock=std::chrono::steady_clock;
    void run(std::stop_token token){
        bool calibrating=false,hasFrame=false;double capturedSeconds=0,noisePeak=0;uint32_t epoch=0;uint64_t endFrame=0,lastCalibration=0;
        while(!token.stop_requested()){
            AnalysisBlock block;if(!queue_.pop(block)){std::this_thread::sleep_for(std::chrono::milliseconds(2));continue;}
            const bool gap=hasFrame && (block.epoch!=epoch || block.firstFrame!=endFrame);processor_.gate(gate_.load());if(!processor_.consume(block))continue;
            auto observation=processor_.observation();AnalysisReport next;{const std::lock_guard lock(mutex_);next=report_;}
            if(gap && calibrating){calibrating=false;next.calibrationFlags=32u;next.calibrationProgress=0;}
            const auto requested=calibrationRequested_.load();if(requested!=lastCalibration){lastCalibration=requested;next.calibrationToken=requested;calibrating=true;capturedSeconds=noisePeak=0;next.calibrationFlags=8u;next.calibrationProgress=0;}
            if(calibrating){capturedSeconds+=block.count/block.sampleRate;noisePeak=std::max(noisePeak,observation.rms);next.calibrationProgress=std::min(1.0,capturedSeconds/2.0);
                if(capturedSeconds>=2){next.recommendedGate=std::floor(std::min(.5,noisePeak*3+.03)*100+.5)/100;next.calibrationFlags=16u;calibrating=false;}}
            next.observed=observation;{const std::lock_guard lock(mutex_);report_=next;published_=Clock::now();}
            hasFrame=true;epoch=block.epoch;endFrame=block.firstFrame+block.count;
        }
    }
    AnalysisQueue& queue_;AnalysisProcessor processor_;
    std::atomic<double> gate_{.06};std::atomic<uint64_t> calibrationRequested_{0};std::mutex mutex_;AnalysisReport report_;Clock::time_point published_{};
    // Declared last: destruction requests stop and joins before processor/mutex.
    std::jthread worker_;
};
}
