// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include "biquad.h"
#include "realtime_queue.h"
#include <complex>
#include <limits>
namespace rocker {
struct AnalysisObservation {
    uint32_t epoch=0,flags=0;uint64_t frame=0,sequence=0,hitCount=0,onsetFrame=0;
    double rate=0,rms=0,peak=0,frequency=0,clarity=0,centroid=0,hitCentroid=0,windowSeconds=0;
};
// Owned by the analysis worker only. YIN-style cumulative mean normalized
// differences are implemented here from the mathematical method, without a
// copied third-party detector. CPU-heavy pitch/FFT never runs in the callback.
class AnalysisProcessor {
public:
    void gate(double value) noexcept {gate_=value;}
    const AnalysisObservation& observation() const noexcept{return observed_;}
    bool consume(const AnalysisBlock& block) noexcept {
        if(block.count==0 || block.count>256 || !std::isfinite(block.sampleRate) || block.sampleRate<8000 || block.sampleRate>192000)return false;
        if(block.epoch!=observed_.epoch || block.sampleRate!=rate_ || block.firstFrame!=nextFrame_)reset(block);
        double peak=0;bool computed=false;
        for(uint32_t i=0;i<block.count;++i){double sample=std::isfinite(block.mono[i])?std::clamp(static_cast<double>(block.mono[i]),-1.0,1.0):0;
            peak=std::max(peak,std::abs(sample));raw_[rawWrite_++%raw_.size()]=sample;
            const double filtered=lowpass2_.process(lowpass1_.process(sample));
            resamplePhase_+=pitchRate_/rate_;
            if(resamplePhase_>=1){const double fraction=1-(resamplePhase_-1)/(pitchRate_/rate_);const double value=previous_+(filtered-previous_)*std::clamp(fraction,0.0,1.0);resamplePhase_-=1;
                pitch_[pitchWrite_++%pitch_.size()]=value;
                if(++hop_>=256){hop_=0;analyze();computed=true;}
            }
            previous_=filtered;
        }
        nextFrame_=block.firstFrame+block.count;observed_.frame=nextFrame_;const auto levelCount=std::min<uint64_t>(rawWrite_,std::min<uint64_t>(raw_.size(),static_cast<uint64_t>(std::ceil(rate_*0.02))));double levelEnergy=0;for(uint64_t i=0;i<levelCount;++i){const double value=raw_[(rawWrite_-1-i)%raw_.size()];levelEnergy+=value*value;}observed_.rms=levelCount?std::sqrt(levelEnergy/levelCount):0;observed_.peak=peak;
        if(observed_.rms*3<gate_ || observed_.rms<1e-7){observed_.frequency=observed_.clarity=0;smoothed_=0;observed_.flags&=~1u;}
        else if(observed_.frequency>0)observed_.flags|=1u;
        // Match the original rising-edge threshold and 320 ms refractory time.
        if(!pending_ && previousLevel_<0.04 && observed_.rms>=0.04 && (!hasHit_ || block.firstFrame>=lastHitFrame_+static_cast<uint64_t>(rate_*0.32))){pending_=true;pendingFrame_=block.firstFrame;captureCount_=0;captureSum_=0;}
        previousLevel_=observed_.rms;
        if(pending_ && computed){const auto age=static_cast<double>(nextFrame_-pendingFrame_)/rate_;
            if(age>=0.04 && age<0.17){captureSum_+=observed_.centroid;++captureCount_;}
            if(age>=0.17){if(captureCount_){observed_.hitCentroid=captureSum_/captureCount_;++observed_.hitCount;observed_.onsetFrame=pendingFrame_;lastHitFrame_=pendingFrame_;hasHit_=true;}pending_=false;}
        }
        if(computed)++observed_.sequence;
        return true;
    }
private:
    void reset(const AnalysisBlock& block) noexcept {
        observed_={};observed_.epoch=block.epoch;observed_.rate=rate_=block.sampleRate;pitchRate_=std::min(12000.0,rate_);observed_.windowSeconds=pitch_.size()/pitchRate_;
        nextFrame_=block.firstFrame;pitchWrite_=rawWrite_=0;hop_=0;resamplePhase_=previous_=smoothed_=previousLevel_=0;hasHit_=pending_=false;captureCount_=0;captureSum_=0;
        raw_.fill(0);pitch_.fill(0);lowpass1_.reset();lowpass2_.reset();const double cutoff=std::min(4000.0,rate_*0.4);lowpass1_.configure(Biquad::Lowpass,rate_,cutoff,0.707,0);lowpass2_.configure(Biquad::Lowpass,rate_,cutoff,0.707,0);
    }
    void analyze() noexcept {
        observed_.centroid=centroid();
        if(pitchWrite_<pitch_.size())return;
        observed_.flags|=2u;
        for(size_t i=0;i<pitch_.size();++i)window_[i]=pitch_[(pitchWrite_+i)%pitch_.size()];
        double sum=0;difference_[0]=1;const auto maxLag=static_cast<size_t>(pitchRate_/30),minLag=static_cast<size_t>(pitchRate_/1400);
        for(size_t lag=1;lag<=maxLag+1;++lag){double d=0;for(size_t i=0;i<1024;++i){const double delta=window_[i]-window_[i+lag];d+=delta*delta;}sum+=d;difference_[lag]=sum>1e-20?d*lag/sum:1;}
        size_t selected=0;
        for(size_t lag=std::max(size_t{2},minLag);lag<=maxLag;++lag)if(difference_[lag]<0.1){while(lag<maxLag && difference_[lag+1]<difference_[lag])++lag;selected=lag;break;}
        observed_.frequency=observed_.clarity=0;observed_.flags&=~1u;
        if(selected){double lower=selected-1.0,upper=selected+1.0;
            // Refine a fractional period with sinc samples, avoiding the high-note
            // bias of a three-bin parabola on normalized YIN differences.
            constexpr double ratio=0.6180339887498949;double a=upper-ratio*(upper-lower),b=lower+ratio*(upper-lower),fa=fractionalDifference(a),fb=fractionalDifference(b);
            for(int i=0;i<20;++i){if(fa<fb){upper=b;b=a;fb=fa;a=upper-ratio*(upper-lower);fa=fractionalDifference(a);}else{lower=a;a=b;fa=fb;b=lower+ratio*(upper-lower);fb=fractionalDifference(b);}}
            const double frequency=pitchRate_/((lower+upper)*0.5);observed_.clarity=std::clamp(1-difference_[selected],0.0,1.0);
            if(frequency>=30 && frequency<=1400 && observed_.clarity>=0.9){smoothed_=smoothed_>0?0.8*smoothed_+0.2*frequency:frequency;observed_.frequency=smoothed_;observed_.flags|=1u;}else smoothed_=0;
        }else smoothed_=0;
    }
    double fractionalDifference(double lag) const noexcept {
        const auto whole=static_cast<size_t>(lag);const double fraction=lag-whole;double error=0,sum=0;std::array<double,16> weights{};
        // Windowed-sinc reconstruction retains upper guitar harmonics at 8 kHz;
        // cubic interpolation has a measurable several-cent bias near E6.
        for(int k=-7;k<=8;++k){const double distance=fraction-k,angle=std::numbers::pi*distance;const double sinc=std::abs(distance)<1e-12?1:std::sin(angle)/angle;const double window=std::abs(distance)>8?0:.42+.5*std::cos(angle/8)+.08*std::cos(angle/4);weights[k+7]=sinc*window;sum+=weights[k+7];}
        for(auto& weight:weights)weight/=sum;
        for(size_t i=8;i<1032;++i){double delayed=0;for(int k=-7;k<=8;++k)delayed+=weights[k+7]*window_[i+whole+k];const double delta=window_[i]-delayed;error+=delta*delta;}return error;
    }
    double centroid() noexcept {
        double mean=0;for(double value:raw_)mean+=value;mean/=raw_.size();
        for(size_t i=0;i<raw_.size();++i){const double hann=0.5-0.5*std::cos(2*std::numbers::pi*i/(raw_.size()-1));spectrum_[i]={(raw_[(rawWrite_+i)%raw_.size()]-mean)*hann,0};}
        for(size_t i=1,j=0;i<spectrum_.size();++i){size_t bit=spectrum_.size()>>1;for(;j&bit;bit>>=1)j^=bit;j^=bit;if(i<j)std::swap(spectrum_[i],spectrum_[j]);}
        for(size_t length=2;length<=spectrum_.size();length*=2){const auto step=std::polar(1.0,-2*std::numbers::pi/length);for(size_t first=0;first<spectrum_.size();first+=length){std::complex<double> weight{1,0};for(size_t j=0;j<length/2;++j){const auto even=spectrum_[first+j],odd=spectrum_[first+j+length/2]*weight;spectrum_[first+j]=even+odd;spectrum_[first+j+length/2]=even-odd;weight*=step;}}}
        double mass=0,weighted=0;for(size_t i=0;i<spectrum_.size()/2;++i){const double magnitude=std::abs(spectrum_[i]);mass+=magnitude;weighted+=magnitude*i*rate_/spectrum_.size();}return mass>1e-12?weighted/mass:0;
    }
    AnalysisObservation observed_;Biquad lowpass1_,lowpass2_;
    std::array<double,2048> pitch_{},raw_{},window_{};std::array<double,512> difference_{};std::array<std::complex<double>,2048> spectrum_{};
    uint64_t pitchWrite_=0,rawWrite_=0,nextFrame_=0,pendingFrame_=0,lastHitFrame_=0;uint32_t hop_=0,captureCount_=0;
    double rate_=0,pitchRate_=12000,resamplePhase_=0,previous_=0,smoothed_=0,gate_=0.06,previousLevel_=0,captureSum_=0;bool pending_=false,hasHit_=false;
};
}
