// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include "media_clip.h"
#include "realtime_queue.h"
#include <algorithm>
#include <cmath>
#include <atomic>
namespace rocker::media {
struct ProjectTrackSpec {std::filesystem::path path;double gain=1,pan=0;bool muted=false,solo=false;uint64_t offset=0,trim=0;};
struct ProjectTrackAudio {ProjectTrackSpec spec;Clip clip;};
struct ProjectMixChange {uint32_t index;double gain,pan;bool muted,solo;};
// The same mixer is used by the callback and offline export. Original files
// and cache ownership never change here; timeline edits prepare a new source.
class ProjectSource final:public PcmSource {
    struct Node {ProjectTrackAudio audio;double fromGain=0,targetGain=0,fromPan=0,targetPan=0;uint64_t rampStart=0;};
public:
    ProjectSource(std::vector<ProjectTrackAudio> tracks,uint32_t rate):rate_(rate){
        if(rate<8000||rate>192000||tracks.empty()||tracks.size()>256)throw Failure(Error::invalidInput,"Invalid project rate or track count");
        uint64_t cacheBytes=0;
        for(auto& track:tracks){
            if(!track.clip.frames() || track.clip.channelCount()<1 || track.clip.channelCount()>2 || track.clip.sampleRate<8000 || track.clip.sampleRate>192000 || track.spec.trim>=track.clip.frames() || !std::isfinite(track.spec.gain)||track.spec.gain<0||track.spec.gain>1 || !std::isfinite(track.spec.pan)||track.spec.pan< -1||track.spec.pan>1)throw Failure(Error::invalidInput,"Invalid project track");
            const auto length=static_cast<uint64_t>(std::ceil((track.clip.frames()-track.spec.trim)*rate_/track.clip.sampleRate));
            if(track.spec.offset>UINT32_MAX || length>UINT32_MAX-track.spec.offset)throw Failure(Error::limitExceeded,"Project exceeds the supported sample timeline");
            frames_=std::max(frames_,track.spec.offset+length);cacheBytes+=track.clip.frames()*track.clip.channelCount()*sizeof(float);if(cacheBytes>8ull*1024*1024*1024)throw Failure(Error::limitExceeded,"Project decoded cache exceeds 8 GiB");
            Node node;node.audio=std::move(track);node.fromPan=node.targetPan=node.audio.spec.pan;nodes_.push_back(std::move(node));
        }
        setTargets(0,true);peaks_.assign(1024,0);
        for(const auto& node:nodes_){const auto source=rocker::media::waveform(node.audio.clip);for(size_t i=0;i<source.size();++i){const auto frame=node.audio.spec.offset+static_cast<uint64_t>(i*(node.audio.clip.frames()-node.audio.spec.trim)*rate_/(node.audio.clip.sampleRate*source.size()));peaks_[std::min<size_t>(1023,frame*1024/frames_)]+=static_cast<float>(source[i]*node.targetGain);}}
        for(auto& peak:peaks_)peak=std::min(1.f,peak);
    }
    const Clip* trackInfo(uint32_t index) const noexcept{return index<nodes_.size()?&nodes_[index].audio.clip:nullptr;}
    bool update(ProjectMixChange change) noexcept{return change.index<nodes_.size() && std::isfinite(change.gain) && change.gain>=0 && change.gain<=1 && std::isfinite(change.pan) && change.pan>=-1 && change.pan<=1 && changes_.push(change);}
    void beginBlock() noexcept override {
        ProjectMixChange value;bool changed=false;
        while(changes_.pop(value)){if(!changed){for(auto& node:nodes_){node.fromGain=gain(node,last_);node.fromPan=pan(node,last_);}changed=true;}auto& spec=nodes_[value.index].audio.spec;spec.gain=value.gain;spec.pan=value.pan;spec.muted=value.muted;spec.solo=value.solo;}
        if(changed)setTargets(last_,false);
    }
    bool ownsEnvelope() const noexcept override{return true;}
    bool read(uint64_t frame,std::array<float,2>& output) noexcept override {
        if(frame>=frames_)return false;
        std::array<double,2> sum{};
        for(auto& node:nodes_){const auto& track=node.audio;const auto& spec=track.spec;const double level=gain(node,frame);if(level==0 || frame<spec.offset)continue;
            const double source=spec.trim+(frame-spec.offset)*track.clip.sampleRate/rate_;if(source>=track.clip.frames())continue;
            const auto first=static_cast<uint64_t>(source),next=std::min<uint64_t>(first+1,track.clip.frames()-1);std::array<float,2> a{},b{};if(!sample(track.clip,first,a)||!sample(track.clip,next,b))return false;
            const double fraction=source-first;const double edge=std::min({1.,(source-spec.trim)/(track.clip.sampleRate*.005),(track.clip.frames()-source)/(track.clip.sampleRate*.005)});const double balance=pan(node,frame);
            sum[0]+=(a[0]+fraction*(b[0]-a[0]))*level*edge*(balance>0?1-balance:1);sum[1]+=(a[1]+fraction*(b[1]-a[1]))*level*edge*(balance<0?1+balance:1);
        }
        output={static_cast<float>(std::clamp(sum[0],-1.,1.)),static_cast<float>(std::clamp(sum[1],-1.,1.))};last_=frame;return true;
    }
    void request(uint64_t frame) noexcept override {
        if(last_>frame+2 || frame>last_+2){for(auto& node:nodes_){node.fromGain=node.targetGain;node.fromPan=node.targetPan;node.rampStart=frame;}last_=frame;}
        for(auto& node:nodes_)if(node.audio.clip.stream){const auto& spec=node.audio.spec;const auto source=spec.trim+(frame>spec.offset?(frame-spec.offset)*node.audio.clip.sampleRate/rate_:0);node.audio.clip.stream->request(static_cast<uint64_t>(std::min<double>(source,node.audio.clip.frames()-1)));}
    }
    void loopHint(uint64_t frame) noexcept override {for(auto& node:nodes_)if(node.audio.clip.stream){const auto& spec=node.audio.spec;node.audio.clip.stream->loopHint(frame==UINT64_MAX?UINT64_MAX:static_cast<uint64_t>(spec.trim+(frame>spec.offset?(frame-spec.offset)*node.audio.clip.sampleRate/rate_:0)));}}
    uint64_t frames() const noexcept override{return frames_;}uint32_t channels() const noexcept override{return 2;}
    bool failed() const noexcept override{for(const auto& node:nodes_)if(node.audio.clip.stream && node.audio.clip.stream->failed())return true;return false;}
    const std::vector<float>& waveform() const noexcept override{return peaks_;}
private:
    static bool sample(const Clip& clip,uint64_t frame,std::array<float,2>& value) noexcept{if(clip.stream)return clip.stream->read(frame,value);value[0]=clip.channels[0][frame];value[1]=clip.channelCount()==1?value[0]:clip.channels[1][frame];return true;}
    double blend(const Node& node,uint64_t frame) const noexcept{return std::clamp((static_cast<double>(frame)-node.rampStart)/(rate_*.01),0.,1.);}
    double gain(const Node& node,uint64_t frame) const noexcept{return node.fromGain+(node.targetGain-node.fromGain)*blend(node,frame);}
    double pan(const Node& node,uint64_t frame) const noexcept{return node.fromPan+(node.targetPan-node.fromPan)*blend(node,frame);}
    void setTargets(uint64_t frame,bool initial) noexcept{bool solo=false;for(const auto& node:nodes_)solo|=node.audio.spec.solo;for(auto& node:nodes_){node.targetGain=node.audio.spec.muted||(solo&&!node.audio.spec.solo)?0:node.audio.spec.gain;node.targetPan=node.audio.spec.pan;node.rampStart=frame;if(initial){node.fromGain=node.targetGain;node.fromPan=node.targetPan;}}}
    uint32_t rate_;uint64_t frames_=0,last_=0;std::vector<Node> nodes_;std::vector<float> peaks_;RealtimeQueue<ProjectMixChange,64> changes_;
};
inline Clip prepareProject(const std::vector<ProjectTrackSpec>& tracks,uint32_t rate,const std::atomic<uint64_t>& generation,uint64_t expected){
    std::vector<ProjectTrackAudio> loaded;for(const auto& track:tracks){if(generation.load()!=expected)throw Failure(Error::decodeFailed,"Project load was cancelled");loaded.push_back({track,prepareFile(track.path,1,generation,expected)});}Clip result;result.sampleRate=rate;result.stream=std::make_shared<ProjectSource>(std::move(loaded),rate);return result;
}
}
