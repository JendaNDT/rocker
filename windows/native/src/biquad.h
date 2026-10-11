// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include <algorithm>
#include <cmath>
#include <numbers>
namespace rocker {
// Normalized RBJ transfer functions, transposed direct form II. Coefficients
// depend on the active device rate; callback-owned state never allocates.
class Biquad {
public:
    enum Kind{Highpass,Lowpass,Peak,LowShelf,HighShelf};
    void configure(Kind kind,double rate,double frequency,double q,double db) noexcept {
        const double w=2*std::numbers::pi*std::clamp(frequency,5.0,rate*0.45)/rate,c=std::cos(w),s=std::sin(w),a=std::pow(10.0,db/40),alpha=s/(2*q);
        double b0=1,b1=0,b2=0,a0=1,a1=0,a2=0;
        if(kind==Highpass){b0=(1+c)/2;b1=-(1+c);b2=b0;a0=1+alpha;a1=-2*c;a2=1-alpha;}
        if(kind==Lowpass){b0=(1-c)/2;b1=1-c;b2=b0;a0=1+alpha;a1=-2*c;a2=1-alpha;}
        if(kind==Peak){b0=1+alpha*a;b1=-2*c;b2=1-alpha*a;a0=1+alpha/a;a1=-2*c;a2=1-alpha/a;}
        const double shelfAlpha=s*std::sqrt(2.0)/2,beta=2*std::sqrt(a)*shelfAlpha;
        if(kind==LowShelf){b0=a*((a+1)-(a-1)*c+beta);b1=2*a*((a-1)-(a+1)*c);b2=a*((a+1)-(a-1)*c-beta);a0=(a+1)+(a-1)*c+beta;a1=-2*((a-1)+(a+1)*c);a2=(a+1)+(a-1)*c-beta;}
        if(kind==HighShelf){b0=a*((a+1)+(a-1)*c+beta);b1=-2*a*((a-1)+(a+1)*c);b2=a*((a+1)+(a-1)*c-beta);a0=(a+1)-(a-1)*c+beta;a1=2*((a-1)-(a+1)*c);a2=(a+1)-(a-1)*c-beta;}
        b0_=b0/a0;b1_=b1/a0;b2_=b2/a0;a1_=a1/a0;a2_=a2/a0;
    }
    double process(double x) noexcept {const double y=b0_*x+z1_;z1_=b1_*x-a1_*y+z2_;z2_=b2_*x-a2_*y;if(std::abs(z1_)<1e-20)z1_=0;if(std::abs(z2_)<1e-20)z2_=0;return y;}
    void reset() noexcept{z1_=z2_=0;}
private:double b0_=1,b1_=0,b2_=0,a1_=0,a2_=0,z1_=0,z2_=0;
};
}
