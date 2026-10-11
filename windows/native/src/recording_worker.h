// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include "recording_tap.h"
#include "wave_writer.h"
#include <chrono>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
namespace rocker {
struct RecordingObservation {
    uint64_t token=0,writtenFrames=0,capturedFrames=0,firstFrame=0,droppedBlocks=0,highWater=0;
    uint32_t state=0,failure=0,rate=0,channels=0,bus=0,epoch=0;bool headerFinalized=false;
};
// Only the disk thread touches WaveWriter after initial preparation. No writer
// operation, file handle, mutex or thread notification enters the callback.
class RecordingWorker {
public:
    explicit RecordingWorker(RecordingTap& tap):tap_(tap){}
    ~RecordingWorker(){forceCancel_.store(true);if(worker_.joinable()){tap_.requestStop(token_.load());worker_.join();}}
    uint64_t token() const noexcept{return token_.load();}
    bool active() const noexcept{const auto state=state_.load();return state==1 || state==2 || state==3;}
    void start(const std::filesystem::path& path,uint64_t token,uint32_t rate,uint32_t channels,uint32_t bus,uint64_t limit=WaveWriter::maximumBytes){
        if(!token || bus>2 || channels!=(bus==2?2u:1u))throw std::invalid_argument("Invalid recording bus or token");
        if(active() || tap_.active())throw std::runtime_error("A recording is already running");
        if(worker_.joinable())worker_.join();
        auto output=std::make_unique<WaveWriter>(path,rate,channels,limit);
        token_.store(token);rate_=rate;channels_=channels;bus_=bus;state_.store(1);failure_.store(0);written_.store(0);firstFrame_.store(0);epoch_.store(0);headerFinalized_.store(false);forceCancel_.store(false);droppedBaseline_=tap_.queue().dropped();
        {const std::lock_guard lock(errorMutex_);error_.clear();}
        try{worker_=std::jthread([this,output=std::move(output)]() mutable {
            uint64_t expected=0;bool began=false;
            try{
                while(!forceCancel_.load()){
                    RecordingBlock block;bool consumed=false;
                    while(tap_.queue().pop(block)){
                        if(block.token!=token_.load())continue;
                        consumed=true;
                        if(block.channels!=channels_ || block.sampleRate!=rate_ || block.frames==0 || block.frames>256 || (began && (block.firstFrame!=expected || block.epoch!=epoch_.load())))throw std::runtime_error("Recording blocks are discontinuous or changed format");
                        if(!began){began=true;expected=block.firstFrame;firstFrame_.store(expected);epoch_.store(block.epoch);}
                        output->append(block.samples.data(),block.frames);expected+=block.frames;written_.store(output->frames());state_.store(2);
                    }
                    const auto capture=tap_.observation();
                    if(capture.token==token_.load() && (capture.state==3 || capture.state==4)){
                        // The callback publishes its terminal state only after
                        // pushing the final partial block. Recheck the queue on
                        // the next pass before finalizing the durable header.
                        if(tap_.queue().pending())continue;
                        failure_.store(capture.failure);break;
                    }
                    if(!consumed)std::this_thread::sleep_for(std::chrono::milliseconds(2));
                }
                if(forceCancel_.load() && !failure_.load())failure_.store(6);
                state_.store(3);output->checkpoint();headerFinalized_.store(true);state_.store(failure_.load()?5u:4u);
            }catch(const std::exception& error){
                failure_.store(5);tap_.requestStop(token_.load());
                {const std::lock_guard lock(errorMutex_);error_=error.what();}
                try{if(!forceCancel_.load()){output->checkpoint();headerFinalized_.store(true);}}catch(...){headerFinalized_.store(false);}
                state_.store(5);
            }catch(...){failure_.store(5);tap_.requestStop(token_.load());state_.store(5);}
            output.reset();
        });}catch(...){failure_.store(5);state_.store(5);throw;}
    }
    void requestStop() noexcept{tap_.requestStop(token_.load());}
    bool wait(std::chrono::milliseconds timeout){
        const auto until=std::chrono::steady_clock::now()+timeout;
        while(active() && std::chrono::steady_clock::now()<until)std::this_thread::sleep_for(std::chrono::milliseconds(2));
        if(active())return false;
        if(worker_.joinable())worker_.join();
        return true;
    }
    void cancel() noexcept{forceCancel_.store(true);tap_.requestStop(token_.load());
#ifdef _WIN32
        if(worker_.joinable())CancelSynchronousIo(worker_.native_handle());
#endif
    }
    RecordingObservation observation() const noexcept {
        const auto state=state_.load(std::memory_order_acquire);const auto capture=tap_.observation();
        return {token_.load(),written_.load(),capture.token==token_.load()?capture.captured:0,firstFrame_.load(),tap_.queue().dropped()-droppedBaseline_,tap_.queue().highWatermark(),state,failure_.load(),rate_,channels_,bus_,epoch_.load(),headerFinalized_.load()};
    }
    std::string error(){const std::lock_guard lock(errorMutex_);return error_;}
private:
    RecordingTap& tap_;std::jthread worker_;std::atomic<uint64_t> token_{0},written_{0},firstFrame_{0};
    std::atomic<uint32_t> state_{0},failure_{0},epoch_{0};std::atomic<bool> headerFinalized_{false},forceCancel_{false};
    uint32_t rate_=0,channels_=0,bus_=0;uint64_t droppedBaseline_=0;
    std::mutex errorMutex_;std::string error_;
};
}
