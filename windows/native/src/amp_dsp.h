// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include "biquad.h"
#include "realtime_queue.h"
#include <array>
#include <vector>
namespace rocker {
struct AmpParameters {
    bool enabled=false;uint32_t distortion=1,cabinet=0;
    // compression, drive, bass, mid, treble, delay, reverb, chorus, level
    std::array<double,9> values{0,0.1,0.1,0,0.15,0.1,0,0,0.6};
};
class DelayLine {
public:
    void prepare(size_t size){samples_.assign(size,0);tags_.assign(size,0);position_=0;generation_=1;}
    void reset() noexcept{++generation_;if(generation_==0)++generation_;position_=0;}
    double read(double delay) const noexcept {
        const double index=position_-delay;const auto base=static_cast<int64_t>(std::floor(index));const auto size=static_cast<int64_t>(samples_.size());
        const auto at=[&](int64_t n){const auto i=static_cast<size_t>((n%size+size)%size);return tags_[i]==generation_?samples_[i]:0.0;};
        return at(base)+(index-base)*(at(base+1)-at(base));
    }
    void write(double sample) noexcept {samples_[position_]=std::clamp(sample,-16.0,16.0);tags_[position_]=generation_;position_=(position_+1)%samples_.size();}
private:std::vector<double> samples_;std::vector<uint32_t> tags_;size_t position_=0;uint32_t generation_=1;
};
class DistortionOversampler {
public:
    void prepare() noexcept {
        double sum=0;for(int i=0;i<31;++i){const double n=i-15;const double sinc=n==0?0.5:std::sin(std::numbers::pi*0.5*n)/(std::numbers::pi*n);const double window=0.42-0.5*std::cos(2*std::numbers::pi*i/30)+0.08*std::cos(4*std::numbers::pi*i/30);coeff_[i]=sinc*window;sum+=coeff_[i];}
        for(auto& c:coeff_)c/=sum;
        reset();
    }
    void reset() noexcept{up_.fill(0);down_.fill(0);upPos_=downPos_=0;}
    double process(double x,const std::array<double,3>& weights) noexcept {
        double result=0;for(int phase=0;phase<2;++phase){const double up=filter(up_,upPos_,phase==0?2*x:0);const double v=std::clamp(up,-1.0,1.0);
            const double shaped=weights[0]*std::tanh(2*v)+weights[2]*std::tanh(9*v)+weights[1]*((43*20*std::numbers::pi/180)*v)/(std::numbers::pi+40*std::abs(v));
            const double down=filter(down_,downPos_,shaped);if(phase==0)result=down;
        }return result;
    }
private:
    double filter(std::array<double,31>& data,size_t& position,double x) noexcept {data[position]=x;double sum=0;for(size_t i=0;i<31;++i)sum+=coeff_[i]*data[(position+31-i)%31];position=(position+1)%31;return sum;}
    std::array<double,31> coeff_{},up_{},down_{};size_t upPos_=0,downPos_=0;
};
class AmpProcessor {
public:
    void prepare(double rate){
        rate_=rate;delay_.prepare(static_cast<size_t>(rate)+2);chorus_.prepare(static_cast<size_t>(rate*0.1)+2);
        for(size_t i=0;i<combs_.size();++i){combFrames_[i]=std::round(rate*combSeconds_[i]);combs_[i].prepare(static_cast<size_t>(combFrames_[i])+2);feedback_[i]=std::exp(-5*combSeconds_[i]/2.2);}
        attack_=std::exp(-1.0/(rate*0.003));release_=std::exp(-1.0/(rate*0.25));shapeSmooth_=1-std::exp(-1.0/(rate*0.02));
        oversampler_.prepare();AmpParameters pending;while(commands_.pop(pending))target_=pending;
        current_=target_.values;cab_=cabinet(target_.cabinet);blend_=target_.enabled?1:0;shape_.fill(0);shape_[target_.distortion]=1;resetState();refresh();setLatency(target_.enabled?15u:0u);
    }
    bool configure(const AmpParameters& value,bool stopped) noexcept {
        if(stopped){AmpParameters old;while(commands_.pop(old)){}target_=value;current_=value.values;setLatency(value.enabled?15u:0u);return true;}
        return commands_.push(value);
    }
    void beginBlock() noexcept {AmpParameters update;while(commands_.pop(update))target_=update;}
    uint64_t faults() const noexcept{return faults_.load();}
    uint32_t latency() const noexcept{return latency_.load();}
    double process(double raw) noexcept {
        const double desired=target_.enabled?1:0;blend_+=std::clamp(desired-blend_,-1.0/(rate_*0.02),1.0/(rate_*0.02));
        if(blend_==0){if(!cleared_){resetState();cleared_=true;}current_=target_.values;setLatency(0);shape_.fill(0);shape_[target_.distortion]=1;return raw;}
        cleared_=false;setLatency(15);
        for(size_t i=0;i<shape_.size();++i)shape_[i]+=((i==target_.distortion?1.0:0.0)-shape_[i])*shapeSmooth_;
        for(size_t i=0;i<current_.size();++i)current_[i]+=std::clamp(target_.values[i]-current_[i],-2.0/(rate_*0.02),2.0/(rate_*0.02));
        const auto cabTarget=cabinet(target_.cabinet);for(size_t i=0;i<cab_.size();++i)cab_[i]+=(cabTarget[i]-cab_[i])/(rate_*0.02);
        if((coefficientFrame_++&31)==0)refresh();
        const double magnitude=std::abs(raw);envelope_=magnitude+(envelope_-magnitude)*(magnitude>envelope_?attack_:release_);
        double compression=1;
        if(current_[0]>0 && envelope_>1e-12){const double db=20*std::log10(envelope_),threshold=-36*current_[0],ratio=1+11*current_[0],knee=24*current_[0],over=db-threshold;double reduction=0;
            if(over>knee/2)reduction=(1/ratio-1)*over;else if(knee>0 && over>-knee/2)reduction=(1/ratio-1)*std::pow(over+knee/2,2)/(2*knee);compression=std::pow(10.0,reduction/20);}
        double processed=oversampler_.process(raw*compression*(1+24*current_[1]),shape_);
        for(auto& filter:filters_)processed=filter.process(processed);
        if(!std::isfinite(processed)){faults_.fetch_add(1);resetState();processed=0;}processed=std::clamp(processed,-8.0,8.0);
        const double echo=delay_.read(rate_*0.3);delay_.write(processed+echo*0.35);
        double reverberated=0;for(size_t i=0;i<combs_.size();++i){const double wet=combs_[i].read(combFrames_[i]);combDamp_[i]=0.5*combDamp_[i]+0.5*wet;combs_[i].write(processed+feedback_[i]*combDamp_[i]);reverberated+=wet/combs_.size();}
        const double chorus=chorus_.read(rate_*(0.02+0.0025*std::sin(lfo_)));chorus_.write(processed);lfo_+=2*std::numbers::pi*0.8/rate_;if(lfo_>=2*std::numbers::pi)lfo_-=2*std::numbers::pi;
        const double effected=std::tanh((processed+echo*current_[5]*0.6+reverberated*current_[6]*0.6+chorus*current_[7]*0.7)*current_[8]);
        return raw+(effected-raw)*blend_;
    }
private:
    void setLatency(uint32_t value) noexcept{if(latencyState_!=value){latencyState_=value;latency_.store(value);}}
    static std::array<double,5> cabinet(uint32_t kind) noexcept {return kind==1?std::array<double,5>{90,4200,2400,0.7,1.5}:kind==2?std::array<double,5>{75,7000,3600,0.8,4.5}:std::array<double,5>{85,5500,3000,0.7,3};}
    void refresh() noexcept {
        filters_[0].configure(Biquad::LowShelf,rate_,120,0.707,current_[2]*12);filters_[1].configure(Biquad::Peak,rate_,800,1,current_[3]*12);filters_[2].configure(Biquad::HighShelf,rate_,3500,0.707,current_[4]*12);
        filters_[3].configure(Biquad::Highpass,rate_,cab_[0],0.707,0);filters_[4].configure(Biquad::Lowpass,rate_,cab_[1],0.707,0);filters_[5].configure(Biquad::Peak,rate_,cab_[2],cab_[3],cab_[4]);
    }
    void resetState() noexcept {for(auto& filter:filters_)filter.reset();oversampler_.reset();delay_.reset();chorus_.reset();for(auto& comb:combs_)comb.reset();combDamp_.fill(0);envelope_=lfo_=0;coefficientFrame_=0;}
    RealtimeQueue<AmpParameters,32> commands_;AmpParameters target_;std::array<double,9> current_=target_.values;std::array<double,5> cab_{85,5500,3000,0.7,3};
    std::array<Biquad,6> filters_;DistortionOversampler oversampler_;DelayLine delay_,chorus_;std::array<DelayLine,8> combs_;
    const std::array<double,8> combSeconds_{0.0311,0.0373,0.0419,0.0437,0.0523,0.0579,0.0611,0.0677};
    std::array<double,3> shape_{0,1,0};std::array<double,8> combFrames_{},feedback_{},combDamp_{};double attack_=0,release_=0,shapeSmooth_=0,rate_=48000,blend_=0,envelope_=0,lfo_=0;uint64_t coefficientFrame_=0;bool cleared_=true;uint32_t latencyState_=0;std::atomic<uint32_t> latency_{0};std::atomic<uint64_t> faults_{0};
};
}
