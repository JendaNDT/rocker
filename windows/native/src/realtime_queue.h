// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include <array>
#include <atomic>
#include <cstddef>
#include <cstdint>
#include <type_traits>
namespace rocker {
// Exactly one producer and one consumer. No allocation, waiting or ownership release.
// Full analysis queues drop the incoming item explicitly; recording callers must treat
// a false result as a recording failure, never claim a complete take.
template<typename T, size_t Capacity> class RealtimeQueue {
    static_assert(std::is_trivially_copyable_v<T>);
    static_assert(Capacity > 0 && (Capacity & (Capacity - 1)) == 0);
    static_assert(std::atomic<size_t>::is_always_lock_free);
    static_assert(std::atomic<uint64_t>::is_always_lock_free);
public:
    bool push(const T& value) noexcept {
        const auto write=write_.load(std::memory_order_relaxed);
        if(write-read_.load(std::memory_order_acquire)>=Capacity) { dropped_.fetch_add(1,std::memory_order_relaxed);return false; }
        data_[write & (Capacity-1)]=value;write_.store(write+1,std::memory_order_release);return true;
    }
    bool pop(T& value) noexcept {
        const auto read=read_.load(std::memory_order_relaxed);
        if(read==write_.load(std::memory_order_acquire))return false;
        value=data_[read & (Capacity-1)];read_.store(read+1,std::memory_order_release);return true;
    }
    uint64_t dropped() const noexcept {return dropped_.load(std::memory_order_relaxed);}
    // Reset requires producer and consumer to be joined, e.g. while changing device.
    void reset() noexcept {read_.store(0);write_.store(0);dropped_.store(0);}
private:
    std::array<T,Capacity> data_{};
    alignas(64) std::atomic<size_t> read_{0};
    alignas(64) std::atomic<size_t> write_{0};
    std::atomic<uint64_t> dropped_{0};
};
struct AnalysisBlock {
    uint64_t firstFrame=0;
    uint32_t epoch=0,count=0;
    double sampleRate=0;
    std::array<float,256> mono{};
};
using AnalysisQueue=RealtimeQueue<AnalysisBlock,64>;
}
