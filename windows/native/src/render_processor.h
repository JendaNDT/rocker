// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include "media_clip.h"
#include "tone_processor.h"
#include "input_graph.h"
#include "callback_stats.h"

namespace rocker {
// Clip ownership requires joined callbacks. Transport commands use a bounded SPSC queue.
// The callback reads immutable PCM, never locks, allocates or releases ownership.
class RenderProcessor {
public:
    RenderProcessor(double frequency, double amplitude) : tone_(frequency, amplitude) {}
    void prepare(double rate) {
        rate_ = rate;callbacks_.prepare(rate); gain_ = 0; tone_.prepare(rate);
        frames_.store(0, std::memory_order_relaxed);
        input_.prepare(rate,epoch_.fetch_add(1)+1);
    }
    void authority(const std::atomic<uint64_t>& intent) noexcept { authority_ = &intent; }
    void authorize(uint64_t ticket) noexcept { ticket_.store(ticket); }
    void enable(bool value) noexcept { enabled_.store(value); tone_.enable(value); }
    void selectTone() noexcept { clipMode_ = false;liveMode_=false;idleMode_=0; }
    void selectClip() noexcept { clipMode_ = true;liveMode_=false;idleMode_=0;mediaPlaying_.store(true); }
    void selectLive() noexcept { clipMode_=false;liveMode_=true;idleMode_=0; }
    void selectReference() noexcept {clipMode_=false;liveMode_=false;idleMode_=4;}
    void selectMetronome() noexcept {clipMode_=false;liveMode_=false;idleMode_=3;}
    uint32_t mode() const noexcept{const auto idle=idleMode_.load();return idle?idle:liveMode_?2u:clipMode_?1u:0u;}
    const CallbackStats& callbacks() const noexcept{return callbacks_;}
    InputGraph& input() noexcept{return input_;}
    bool playing() const noexcept{return clipMode_ && mediaPlaying_.load() && !ended_.load();}
    bool requestSeek(double seconds,bool joined) noexcept{return command({1,0,seconds},joined);}
    bool requestLoop(uint64_t bounds,bool joined) noexcept{return command({2,bounds,0},joined);}
    bool requestPlayback(bool playing,bool joined) noexcept{return command({3,playing?1u:0u,0},joined);}
    uint64_t acknowledgedClip() const noexcept{return clipAck_.load(std::memory_order_acquire);}
    struct ClipLease {uint64_t current,pending,consumed;bool valid;};
    ClipLease clipLease() const noexcept{const auto before=leaseSequence_.load(std::memory_order_acquire);const ClipLease result{clipAck_.load(),pendingAck_.load(),consumedAck_.load(),true};const auto after=leaseSequence_.load(std::memory_order_acquire);return {result.current,result.pending,result.consumed,before==after && !(before&1)};}
    bool stage(const media::Clip* clip,double speed,uint64_t generation,bool joined,uint64_t afterCycles=0) noexcept{return command({afterCycles?6u:4u,generation,speed,clip,4,afterCycles},joined);}
    bool rampPending() const noexcept{return pendingRamp_.load();}
    double loopStart() const noexcept{return (loop_.load()&UINT32_MAX)/sourceRate();}
    bool cancelRamp(bool joined) noexcept{return command({7,0,0},joined);}
    bool waiting() const noexcept{return waiting_.load();}
    bool requestCountIn(uint32_t bars,uint32_t beats,double bpm) noexcept{return command({5,bars,bpm,nullptr,beats},false);}
    bool buffering() const noexcept{return buffering_.load();}
    bool streamFailed() const noexcept{return streamFailed_.load();}
    void install(media::Clip clip, double speed) {
        TransportCommand pending;while(commands_.pop(pending))apply(pending);ownedClip_=std::move(clip);clip_=&ownedClip_;cacheShape();speed_=speed; clipMode_ = true;liveMode_=false;idleMode_=0;loop_.store(0);loopCycles_.store(0);seek(0);
    }
    bool loopEnabled() const noexcept{return loop_.load()!=0;}
    uint64_t loopCycles() const noexcept{return loopCycles_.load();}
    bool loop(double start,double end,bool enabled) noexcept {
        if(!enabled){loop_.store(0);return true;}
        if(!hasClip() || start<0 || end<=start || end>duration() || !std::isfinite(start) || !std::isfinite(end))return false;
        const auto first=static_cast<uint64_t>(std::llround(start*clip_->sampleRate)),last=static_cast<uint64_t>(std::llround(end*clip_->sampleRate));
        if(last>clip_->frames() || last>UINT32_MAX || first>=last)return false;
        sourceLoopStart_=first/clip_->sampleRate*speed_.load();sourceLoopEnd_=last/clip_->sampleRate*speed_.load();loop_.store((last<<32)|first);loopCycles_.store(0);if(cursor_<first || cursor_>=last)cursor_=static_cast<double>(first);ended_.store(false);position_.store(cursor_/clip_->sampleRate);return true;
    }
    bool hasClip() const noexcept { return clipFrames_.load()!=0; }
    void seek(double seconds) noexcept {
        cursor_ = std::clamp(seconds * clip_->sampleRate, 0.0, static_cast<double>(clip_->frames()));
        const auto activeLoop=loop_.load();if(activeLoop && (cursor_<static_cast<double>(activeLoop&UINT32_MAX) || cursor_>=static_cast<double>(activeLoop>>32)))cursor_=static_cast<double>(activeLoop&UINT32_MAX);
        position_.store(clip_->sampleRate > 0 ? cursor_ / clip_->sampleRate : 0);
        ended_.store(hasClip() && !loopEnabled() && cursor_ >= static_cast<double>(clip_->frames()));
        gain_ = 0;if(clip_->stream)clip_->stream->request(static_cast<uint64_t>(cursor_));
    }
    double sourceRate() const noexcept{return sourceRate_.load();}
    double duration() const noexcept {const auto rate=sourceRate_.load();return rate>0?clipFrames_.load()/rate:0;}
    double position() const noexcept { return position_.load(); }
    double speed() const noexcept { return speed_; }
    double volume() const noexcept { return volume_.load(); }
    void volume(double value) noexcept { volume_.store(value); }
    bool ended() const noexcept { return ended_.load(); }
    bool clipMode() const noexcept { return clipMode_; }
    uint64_t frames() const noexcept { return (clipMode_ || liveMode_ || idleMode_) ? frames_.load() : tone_.frames(); }
    void render(float* const* outputs, int channels, int count) noexcept {
        struct Acknowledge {RenderProcessor& self;~Acknowledge(){self.publishClipLease();}} acknowledge{*this};
        TransportCommand change;while(commands_.pop(change))apply(change);
        // A late driver start cannot resurrect a cancelled request, even if
        // cancellation raced with enable(). The authority outlives joined callbacks.
        if (authority_ && ticket_.load() != authority_->load()) {
            if (outputs && count > 0) for (int c = 0; c < channels; ++c)
                if (outputs[c]) std::fill_n(outputs[c], count, 0.0f);
            return;
        }
        if(liveMode_ || idleMode_){if(outputs && count>0)for(int c=0;c<channels;++c)if(outputs[c])std::fill_n(outputs[c],count,0.0f);frames_.fetch_add(count>0?static_cast<uint64_t>(count):0);return;}
        if (!clipMode_) { tone_.render(outputs, channels, count); return; }
        if (!outputs || channels <= 0 || count <= 0) return;
        const auto target = enabled_.load() && mediaPlaying_.load() ? volume_.load() : 0.0;
        auto increment = clip_->sampleRate / rate_;
        if(clip_->stream)clip_->stream->loopHint(loop_.load()?loop_.load()&UINT32_MAX:UINT64_MAX);
        bool missed=false;
        auto loop=loop_.load();auto loopFirst=static_cast<double>(loop&UINT32_MAX),loopLast=static_cast<double>(loop>>32);
        for (int i = 0; i < count; ++i) {
            const bool delayed=delayFrames_>0;if(delayed)--delayFrames_;
            gain_ += std::clamp((delayed?0:target) - gain_, -1.0 / (rate_ * 0.01), 1.0 / (rate_ * 0.01));
            if(loop && cursor_>=loopLast){cursor_=loopFirst+std::fmod(cursor_-loopLast,loopLast-loopFirst);loopCycles_.fetch_add(1);if(applyPendingLoop()){loop=loop_.load();loopFirst=static_cast<double>(loop&UINT32_MAX);loopLast=static_cast<double>(loop>>32);increment=clip_->sampleRate/rate_;}}
            const auto valid = hasClip() && cursor_ < static_cast<double>(clip_->frames());
            const auto index = valid ? static_cast<size_t>(cursor_) : 0;
            const auto fraction = cursor_ - static_cast<double>(index);
            // Fade the final 5 ms rather than jumping from a non-zero last sample.
            const auto fade=loop?std::min(clip_->sampleRate*0.005,(loopLast-loopFirst)*0.5):clip_->sampleRate*0.005;
            const auto tail=valid?(loop?std::min({1.0,(cursor_-loopFirst)/fade,(loopLast-cursor_)/fade}):std::min(1.0,(clip_->frames()-cursor_)/fade)):0.0;
            std::array<float,2> streamed{},streamedNext{};bool available=true;
            if(valid && clip_->stream){clip_->stream->request(index);const auto next=std::min(index+1,loop?static_cast<size_t>(loopLast)-1:clip_->frames()-1);available=clip_->stream->read(index,streamed) && clip_->stream->read(next,streamedNext);if(!available){missed=true;gain_=0;}}
            for (int c = 0; c < channels; ++c) {
                if (!outputs[c]) continue;
                double sample = 0;
                if(valid && available && clip_->stream && c<2){sample=streamed[c]+fraction*(streamedNext[c]-streamed[c]);}
                else if (valid && !clip_->stream && (clip_->channels.size() == 1 || c < 2)) {
                    const auto source = clip_->channels.size() == 1 ? 0u : static_cast<size_t>(c);
                    const auto& pcm = clip_->channels[source];
                    const auto next = std::min(index + 1,loop?static_cast<size_t>(loopLast)-1:pcm.size()-1);
                    sample = pcm[index] + fraction * (pcm[next] - pcm[index]);
                }
                outputs[c][i] = static_cast<float>(std::clamp(sample * gain_ * tail, -1.0, 1.0));
            }
            if (valid && available && !delayed && enabled_.load(std::memory_order_relaxed) && mediaPlaying_.load(std::memory_order_relaxed))
                cursor_ = loop?cursor_+increment:std::min(cursor_ + increment, static_cast<double>(clip_->frames()));
        }
        if(loop && cursor_>=loopLast){cursor_=loopFirst+std::fmod(cursor_-loopLast,loopLast-loopFirst);loopCycles_.fetch_add(1);if(applyPendingLoop()){loop=loop_.load();loopFirst=static_cast<double>(loop&UINT32_MAX);loopLast=static_cast<double>(loop>>32);increment=clip_->sampleRate/rate_;}}
        waiting_.store(delayFrames_>0);buffering_.store(missed);streamFailed_.store(clip_->stream && clip_->stream->failed());
        position_.store(hasClip() ? cursor_ / clip_->sampleRate : 0);
        ended_.store(hasClip() && !loopEnabled() && cursor_ >= static_cast<double>(clip_->frames()));
        frames_.fetch_add(static_cast<uint64_t>(count));
    }
    void process(const float* const* inputs,int inputChannels,float* const* outputs,int channels,int count) noexcept {
        const auto before=CallbackStats::Clock::now();
        render(outputs,channels,count);
        if(!authority_ || ticket_.load()==authority_->load())input_.process(inputs && inputChannels>0?inputs[0]:nullptr,outputs,channels,count);
        callbacks_.completed(before,CallbackStats::Clock::now(),!outputs || channels<=0?0:count);
    }
private:
    struct TransportCommand {uint32_t kind;uint64_t bounds;double seconds;const media::Clip* clip=nullptr;uint32_t beats=4;uint64_t afterCycles=0;};
    bool command(TransportCommand value,bool joined) noexcept {
        if(joined){TransportCommand pending;while(commands_.pop(pending))apply(pending);apply(value);publishClipLease();return true;}
        return commands_.push(value);
    }
    void apply(TransportCommand value) noexcept {
        if(value.kind==4){consumedClipGeneration_=std::max(consumedClipGeneration_,value.bounds);pendingClip_={};pendingRamp_.store(false);clip_=value.clip;speed_.store(value.seconds);cacheShape();loop_.store(0);loopCycles_.store(0);seek(0);mediaPlaying_.store(false);clipMode_.store(true);liveMode_.store(false);idleMode_.store(0);currentClipGeneration_=value.bounds;}
        else if(value.kind==6){consumedClipGeneration_=std::max(consumedClipGeneration_,value.bounds);pendingClip_=value;pendingRamp_.store(true);}
        else if(value.kind==7){pendingClip_={};pendingRamp_.store(false);}
        else if(value.kind==5){const auto observed=input_.clock().observation();const double phase=observed.phase;delayFrames_=value.bounds?static_cast<uint64_t>(std::llround((value.bounds*value.beats+(phase>1e-10 && phase<1-1e-10?1-phase:0))*rate_*60/value.seconds)):0;waiting_.store(delayFrames_>0);mediaPlaying_.store(true);}
        else if(value.kind==1){delayFrames_=0;waiting_.store(false);seek(value.seconds);}
        else if(value.kind==2){if(value.bounds && ((value.bounds>>32)>clip_->frames() || (value.bounds&UINT32_MAX)>=(value.bounds>>32)))return;if(!value.bounds){pendingClip_={};pendingRamp_.store(false);}sourceLoopStart_=value.bounds?(value.bounds&UINT32_MAX)/clip_->sampleRate*speed_.load():0;sourceLoopEnd_=value.bounds?(value.bounds>>32)/clip_->sampleRate*speed_.load():0;loop_.store(value.bounds);loopCycles_.store(0);seek(position_.load());}
        else if(value.kind==3){if(!value.bounds){pendingClip_={};pendingRamp_.store(false);delayFrames_=0;waiting_.store(false);}clipMode_.store(true);liveMode_.store(false);idleMode_.store(0);if(value.bounds && ended_.load())seek(0);mediaPlaying_.store(value.bounds!=0);if(value.bounds)gain_=0;}
    }
    bool applyPendingLoop() noexcept {
        if(!pendingClip_.clip || loopCycles_.load()<pendingClip_.afterCycles)return false;
        const auto oldBounds=loop_.load();const auto ratio=speed_.load()/pendingClip_.seconds*pendingClip_.clip->sampleRate/clip_->sampleRate;
        const auto offset=cursor_-static_cast<double>(oldBounds&UINT32_MAX);
        const auto first=static_cast<uint64_t>(std::llround(sourceLoopStart_*pendingClip_.clip->sampleRate/pendingClip_.seconds)),last=static_cast<uint64_t>(std::llround(sourceLoopEnd_*pendingClip_.clip->sampleRate/pendingClip_.seconds));
        clip_=pendingClip_.clip;speed_.store(pendingClip_.seconds);cacheShape();currentClipGeneration_=pendingClip_.bounds;
        const auto end=std::min<uint64_t>(last,clip_->frames());
        loop_.store(end>first?(end<<32)|first:0);cursor_=std::min(first+offset*ratio,static_cast<double>(clip_->frames()));
        if(clip_->stream){clip_->stream->request(static_cast<uint64_t>(cursor_));clip_->stream->loopHint(first);}
        pendingClip_={};pendingRamp_.store(false);return true;
    }
    void publishClipLease() noexcept{leaseSequence_.fetch_add(1,std::memory_order_acq_rel);clipAck_.store(currentClipGeneration_);pendingAck_.store(pendingClip_.clip?pendingClip_.bounds:0);consumedAck_.store(consumedClipGeneration_);leaseSequence_.fetch_add(1,std::memory_order_release);}
    double sourceLoopStart_=0,sourceLoopEnd_=0;uint64_t consumedClipGeneration_=0;std::atomic<uint64_t> leaseSequence_{0},pendingAck_{0},consumedAck_{0};
    TransportCommand pendingClip_{};std::atomic<bool> pendingRamp_{false};
    void cacheShape() noexcept{clipFrames_.store(clip_->frames());sourceRate_.store(clip_->sampleRate);}
    RealtimeQueue<TransportCommand,64> commands_;
    uint64_t currentClipGeneration_=0;
    std::atomic<uint64_t> clipAck_{0},clipFrames_{0};
    std::atomic<double> sourceRate_{0};
    std::atomic<bool> buffering_{false},streamFailed_{false},waiting_{false};uint64_t delayFrames_=0;
    std::atomic<bool> mediaPlaying_{true};
    static_assert(std::atomic<double>::is_always_lock_free);
    const std::atomic<uint64_t>* authority_ = nullptr;
    std::atomic<uint64_t> ticket_{0};
    InputGraph input_;CallbackStats callbacks_;
    std::atomic<uint32_t> epoch_{0};
    ToneProcessor tone_;
    media::Clip ownedClip_;const media::Clip* clip_=&ownedClip_;
    double rate_ = 48000, cursor_ = 0, gain_ = 0;std::atomic<double> speed_{1};
    std::atomic<bool> clipMode_{false},liveMode_{false};std::atomic<uint32_t> idleMode_{0};
    std::atomic<double> position_{0}, volume_{0.15};
    std::atomic<bool> enabled_{false}, ended_{false};
    std::atomic<uint64_t> frames_{0},loop_{0},loopCycles_{0};
};
}
