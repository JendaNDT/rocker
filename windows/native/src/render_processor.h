// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include "media_clip.h"
#include "tone_processor.h"
#include "input_graph.h"

namespace rocker {
// Clip ownership and cursor changes require the driver to have joined callbacks.
// The callback reads immutable PCM, never locks, allocates or releases ownership.
class RenderProcessor {
public:
    RenderProcessor(double frequency, double amplitude) : tone_(frequency, amplitude) {}
    void prepare(double rate) {
        rate_ = rate; gain_ = 0; tone_.prepare(rate);
        frames_.store(0, std::memory_order_relaxed);
        input_.prepare(rate,epoch_.fetch_add(1)+1);
    }
    void authority(const std::atomic<uint64_t>& intent) noexcept { authority_ = &intent; }
    void authorize(uint64_t ticket) noexcept { ticket_.store(ticket); }
    void enable(bool value) noexcept { enabled_.store(value); tone_.enable(value); }
    void selectTone() noexcept { clipMode_ = false;liveMode_=false; }
    void selectClip() noexcept { clipMode_ = true;liveMode_=false; }
    void selectLive() noexcept { clipMode_=false;liveMode_=true; }
    uint32_t mode() const noexcept{return liveMode_?2u:clipMode_?1u:0u;}
    InputGraph& input() noexcept{return input_;}
    void install(media::Clip clip, double speed) {
        clip_ = std::move(clip); speed_ = speed; clipMode_ = true;liveMode_=false;loop_.store(0);loopCycles_.store(0);seek(0);
    }
    bool loopEnabled() const noexcept{return loop_.load()!=0;}
    uint64_t loopCycles() const noexcept{return loopCycles_.load();}
    bool loop(double start,double end,bool enabled) noexcept {
        if(!enabled){loop_.store(0);return true;}
        if(!hasClip() || start<0 || end<=start || end>duration() || !std::isfinite(start) || !std::isfinite(end))return false;
        const auto first=static_cast<uint64_t>(std::llround(start*clip_.sampleRate)),last=static_cast<uint64_t>(std::llround(end*clip_.sampleRate));
        if(last>clip_.frames() || last>UINT32_MAX || first>=last)return false;
        loop_.store((last<<32)|first);loopCycles_.store(0);if(cursor_<first || cursor_>=last)cursor_=static_cast<double>(first);ended_.store(false);position_.store(cursor_/clip_.sampleRate);return true;
    }
    bool hasClip() const noexcept { return clip_.frames() != 0; }
    void seek(double seconds) noexcept {
        cursor_ = std::clamp(seconds * clip_.sampleRate, 0.0, static_cast<double>(clip_.frames()));
        const auto activeLoop=loop_.load();if(activeLoop && (cursor_<static_cast<double>(activeLoop&UINT32_MAX) || cursor_>=static_cast<double>(activeLoop>>32)))cursor_=static_cast<double>(activeLoop&UINT32_MAX);
        position_.store(clip_.sampleRate > 0 ? cursor_ / clip_.sampleRate : 0);
        ended_.store(hasClip() && !loopEnabled() && cursor_ >= static_cast<double>(clip_.frames()));
        gain_ = 0;
    }
    double duration() const noexcept { return hasClip() ? clip_.frames() / clip_.sampleRate : 0; }
    double position() const noexcept { return position_.load(); }
    double speed() const noexcept { return speed_; }
    double volume() const noexcept { return volume_.load(); }
    void volume(double value) noexcept { volume_.store(value); }
    bool ended() const noexcept { return ended_.load(); }
    bool clipMode() const noexcept { return clipMode_; }
    uint64_t frames() const noexcept { return (clipMode_ || liveMode_) ? frames_.load() : tone_.frames(); }
    void render(float* const* outputs, int channels, int count) noexcept {
        // A late driver start cannot resurrect a cancelled request, even if
        // cancellation raced with enable(). The authority outlives joined callbacks.
        if (authority_ && ticket_.load() != authority_->load()) {
            if (outputs && count > 0) for (int c = 0; c < channels; ++c)
                if (outputs[c]) std::fill_n(outputs[c], count, 0.0f);
            return;
        }
        if(liveMode_){if(outputs && count>0)for(int c=0;c<channels;++c)if(outputs[c])std::fill_n(outputs[c],count,0.0f);frames_.fetch_add(count>0?static_cast<uint64_t>(count):0);return;}
        if (!clipMode_) { tone_.render(outputs, channels, count); return; }
        if (!outputs || channels <= 0 || count <= 0) return;
        const auto target = enabled_.load() ? volume_.load() : 0.0;
        const auto increment = clip_.sampleRate / rate_;
        const auto loop=loop_.load();const auto loopFirst=static_cast<double>(loop&UINT32_MAX),loopLast=static_cast<double>(loop>>32);
        for (int i = 0; i < count; ++i) {
            gain_ += std::clamp(target - gain_, -1.0 / (rate_ * 0.01), 1.0 / (rate_ * 0.01));
            if(loop && cursor_>=loopLast){cursor_=loopFirst+std::fmod(cursor_-loopLast,loopLast-loopFirst);loopCycles_.fetch_add(1);}
            const auto valid = hasClip() && cursor_ < static_cast<double>(clip_.frames());
            const auto index = valid ? static_cast<size_t>(cursor_) : 0;
            const auto fraction = cursor_ - static_cast<double>(index);
            // Fade the final 5 ms rather than jumping from a non-zero last sample.
            const auto fade=loop?std::min(clip_.sampleRate*0.005,(loopLast-loopFirst)*0.5):clip_.sampleRate*0.005;
            const auto tail=valid?(loop?std::min({1.0,(cursor_-loopFirst)/fade,(loopLast-cursor_)/fade}):std::min(1.0,(clip_.frames()-cursor_)/fade)):0.0;
            for (int c = 0; c < channels; ++c) {
                if (!outputs[c]) continue;
                double sample = 0;
                if (valid && (clip_.channels.size() == 1 || c < 2)) {
                    const auto source = clip_.channels.size() == 1 ? 0u : static_cast<size_t>(c);
                    const auto& pcm = clip_.channels[source];
                    const auto next = std::min(index + 1,loop?static_cast<size_t>(loopLast)-1:pcm.size()-1);
                    sample = pcm[index] + fraction * (pcm[next] - pcm[index]);
                }
                outputs[c][i] = static_cast<float>(std::clamp(sample * gain_ * tail, -1.0, 1.0));
            }
            if (valid && enabled_.load(std::memory_order_relaxed))
                cursor_ = loop?cursor_+increment:std::min(cursor_ + increment, static_cast<double>(clip_.frames()));
        }
        if(loop && cursor_>=loopLast){cursor_=loopFirst+std::fmod(cursor_-loopLast,loopLast-loopFirst);loopCycles_.fetch_add(1);}
        position_.store(hasClip() ? cursor_ / clip_.sampleRate : 0);
        ended_.store(hasClip() && !loopEnabled() && cursor_ >= static_cast<double>(clip_.frames()));
        frames_.fetch_add(static_cast<uint64_t>(count));
    }
    void process(const float* const* inputs,int inputChannels,float* const* outputs,int channels,int count) noexcept {
        render(outputs,channels,count);
        if(authority_ && ticket_.load()!=authority_->load())return;
        input_.process(inputs && inputChannels>0?inputs[0]:nullptr,outputs,channels,count);
    }
private:
    static_assert(std::atomic<double>::is_always_lock_free);
    const std::atomic<uint64_t>* authority_ = nullptr;
    std::atomic<uint64_t> ticket_{0};
    InputGraph input_;
    std::atomic<uint32_t> epoch_{0};
    ToneProcessor tone_;
    media::Clip clip_;
    double rate_ = 48000, cursor_ = 0, gain_ = 0, speed_ = 1;
    bool clipMode_ = false,liveMode_=false;
    std::atomic<double> position_{0}, volume_{0.15};
    std::atomic<bool> enabled_{false}, ended_{false};
    std::atomic<uint64_t> frames_{0},loop_{0},loopCycles_{0};
};
}
