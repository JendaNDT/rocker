// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include <condition_variable>
#include <deque>
#include <functional>
#include <future>
#include <mutex>
#include <thread>
namespace rocker {
// Device operations and maintenance execute serially here, never on the audio callback.
// Synchronous callers must use a managed worker; cached telemetry does not submit jobs.
class ControlExecutor {
public:
    explicit ControlExecutor(std::function<void()> maintenance,std::function<void()> shutdown)
        : worker_([this,maintenance=std::move(maintenance),shutdown=std::move(shutdown)](std::stop_token stop){
            try { while(!stop.stop_requested()){
                std::function<void()> operation;
                {
                    std::unique_lock lock(control_);
                    changed_.wait_for(lock,std::chrono::milliseconds(10),[&]{return !jobs_.empty() || stop.stop_requested();});
                    if(!jobs_.empty()){operation=std::move(jobs_.front().run);jobs_.pop_front();}
                }
                if(operation)operation();
                maintenance();
            }
            } catch(...) { /* Shutdown still silences and joins the driver. */ }
            {const std::lock_guard lock(control_);accepting_=false;for(auto& job:jobs_)job.cancel();jobs_.clear();}
            try { shutdown(); } catch(...) { }
        }) {}
    ~ControlExecutor(){worker_.request_stop();changed_.notify_one();worker_.join();}
    template<typename Operation> auto invoke(Operation operation)->decltype(operation()){
        using Result=decltype(operation());auto promise=std::make_shared<std::promise<Result>>();auto result=promise->get_future();
        {
            const std::lock_guard lock(control_);
            if(!accepting_)throw std::runtime_error("Audio control worker is unavailable.");
            if(jobs_.size()>=64)throw std::runtime_error("Audio control queue is full.");
            jobs_.push_back({[promise,operation=std::move(operation)]() mutable{
                try{if constexpr(std::is_void_v<Result>){operation();promise->set_value();}else promise->set_value(operation());}
                catch(...){promise->set_exception(std::current_exception());}
            },[promise]{try{promise->set_exception(std::make_exception_ptr(std::runtime_error("Audio control worker stopped.")));}catch(...){}}});
        }
        changed_.notify_one();return result.get();
    }
private:
    struct Job{std::function<void()> run,cancel;};
    std::mutex control_;std::condition_variable changed_;std::deque<Job> jobs_;
    bool accepting_=true;
    std::jthread worker_;
};
}
