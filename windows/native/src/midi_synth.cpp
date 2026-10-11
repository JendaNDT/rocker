// SPDX-License-Identifier: AGPL-3.0-only
#include "media_clip.h"
#include <juce_audio_basics/juce_audio_basics.h>
#include <array>
#include <cmath>
#include <numbers>
#include <algorithm>
#include <cstring>

namespace rocker::media {
namespace {
struct Event { juce::MidiMessage message; };
struct Note { double start, end; int key; float velocity; int channel; bool drum; };
struct ChannelSound { double wheel=0,range=2,volume=1,expression=1; };
struct SoundPoint { double time;ChannelSound sound; };
constexpr double rate = 48000, maximumDuration = 300, release = 0.08;
[[noreturn]] void invalid(const char* message) { throw Failure(Error::invalidInput, message); }
}
Clip synthesizeMidi(const uint8_t* bytes, uint32_t count, Limits limits) {
    if (!bytes || count < 14 || count > 4u * 1024 * 1024 || count > limits.encodedBytes)
        invalid("MIDI input is empty or exceeds the 4 MB limit.");
    if (std::memcmp(bytes, "MThd", 4) != 0 || bytes[8] != 0 || bytes[9] > 1)
        invalid("Only standard MIDI files of type 0 or 1 are supported.");
    juce::MemoryInputStream input(bytes, count, false);
    juce::MidiFile midi;
    if (!midi.readFrom(input, false) || midi.getTimeFormat() <= 0 || midi.getNumTracks() > 64)
        invalid("Invalid MIDI file, track count or unsupported SMPTE time division.");
    size_t eventCount = 0;
    for (int t = 0; t < midi.getNumTracks(); ++t) eventCount += static_cast<size_t>(midi.getTrack(t)->getNumEvents());
    if (eventCount > 100000) throw Failure(Error::limitExceeded, "MIDI exceeds 100,000 events.");
    midi.convertTimestampTicksToSeconds();
    std::vector<Event> events;
    events.reserve(eventCount);
    for (int t = 0; t < midi.getNumTracks(); ++t)
        for (int i = 0; i < midi.getTrack(t)->getNumEvents(); ++i) {
            auto message = midi.getTrack(t)->getEventPointer(i)->message;
            if (!std::isfinite(message.getTimeStamp()) || message.getTimeStamp() < 0 || message.getTimeStamp() > maximumDuration)
                throw Failure(Error::limitExceeded, "MIDI is limited to five minutes.");
            events.push_back({message});
        }
    std::stable_sort(events.begin(), events.end(), [](const auto& a, const auto& b) {
        return a.message.getTimeStamp() < b.message.getTimeStamp();
    });
    std::array<std::array<int, 128>, 16> active;
    for (auto& channel : active) channel.fill(-1);
    std::array<std::array<bool, 128>, 16> held{};
    std::array<bool, 16> sustain{};
    std::vector<Note> notes;
    std::array<ChannelSound,16> sound{};std::array<int,16> rpnHigh{},rpnLow{},rangeHigh{},rangeLow{};rpnHigh.fill(127);rpnLow.fill(127);rangeHigh.fill(2);
    std::array<std::vector<SoundPoint>,16> curves;for(auto& curve:curves)curve.push_back({0,{}});
    auto endNote = [&](int channel, int key, double time) {
        const int index = active[channel][key];
        if (index >= 0) { notes[static_cast<size_t>(index)].end = time; active[channel][key] = -1; held[channel][key] = false; }
    };
    double lastEvent = 0;
    for (const auto& event : events) {
        const auto& m = event.message;
        const auto time = m.getTimeStamp();
        lastEvent = std::max(lastEvent, time);
        const auto channel = m.getChannel() - 1;
        if (channel < 0 || channel >= 16) continue;
        if(m.isPitchWheel()){sound[channel].wheel=(m.getPitchWheelValue()-8192)/8192.;curves[channel].push_back({time,sound[channel]});}
        if (m.isNoteOn()) {

            if (notes.size() >= 5000) throw Failure(Error::limitExceeded, "MIDI exceeds 5,000 notes.");
            const auto key = m.getNoteNumber();
            if(channel!=9){endNote(channel,key,time);active[channel][key]=static_cast<int>(notes.size());}
            notes.push_back({time,-1,key,m.getFloatVelocity(),channel,channel==9});
        } else if (m.isNoteOff()) {
            const auto key = m.getNoteNumber();
            if (sustain[channel]) held[channel][key] = true;
            else endNote(channel, key, time);
        } else if (m.isController()) {
            const auto controller = m.getControllerNumber();
            if(controller==101)rpnHigh[channel]=m.getControllerValue();else if(controller==100)rpnLow[channel]=m.getControllerValue();
            else if((controller==6 || controller==38) && rpnHigh[channel]==0 && rpnLow[channel]==0){if(controller==6)rangeHigh[channel]=std::min(24,m.getControllerValue());else rangeLow[channel]=std::min(99,m.getControllerValue());sound[channel].range=rangeHigh[channel]+rangeLow[channel]/100.;curves[channel].push_back({time,sound[channel]});}
            else if(controller==7 || controller==11){(controller==7?sound[channel].volume:sound[channel].expression)=m.getControllerValue()/127.;curves[channel].push_back({time,sound[channel]});}
            else if(controller==121){sound[channel]={};rpnHigh[channel]=rpnLow[channel]=127;rangeHigh[channel]=2;rangeLow[channel]=0;curves[channel].push_back({time,sound[channel]});}
            if (controller == 64) {
                sustain[channel] = m.getControllerValue() >= 64;
                if (!sustain[channel]) for (int key = 0; key < 128; ++key) if (held[channel][key]) endNote(channel, key, time);
            } else if (controller == 120 || controller == 123) {
                for (int key = 0; key < 128; ++key) endNote(channel, key, time);
            }
        }
    }
    if (notes.empty()) invalid("MIDI contains no playable notes.");
    double duration = lastEvent;
    for (auto& note : notes) {
        if(note.drum){const int key=note.key;const double length=key==35 || key==36?.5:key==38 || key==40?.25:key==42 || key==44?.12:key==46?.45:key==49 || key==51 || key==52 || key==53 || key==55 || key==57 || key==59?.9:.35;note.end=note.start+length;}
        if (note.end < 0) note.end = std::max(note.start + 0.25, lastEvent);
        if (note.end <= note.start) note.end = note.start + 1.0 / rate;
        duration = std::max(duration, note.end + release);
    }
    const auto frames = static_cast<size_t>(std::ceil(duration * rate));
    if (frames > limits.pcmBytes / sizeof(float)) throw Failure(Error::limitExceeded, "Synthesized MIDI exceeds the PCM budget.");
    // Reject dense scores before synthesis, preventing unbounded worker CPU use.
    std::vector<std::pair<double, int>> edges;
    for (const auto& note : notes) { edges.emplace_back(note.start, 1); edges.emplace_back(note.end + release, -1); }
    std::sort(edges.begin(), edges.end());
    int voices = 0;
    for (auto edge : edges) if ((voices += edge.second) > 64) throw Failure(Error::limitExceeded, "MIDI exceeds 64 simultaneous voices.");
    double work = 0;
    for (const auto& note : notes) work += (note.end + release - note.start) * rate * 8;
    if (work > 250000000) throw Failure(Error::limitExceeded, "MIDI exceeds the synthesis work budget.");
    Clip clip{rate, {std::vector<float>(frames, 0)}};
    auto& pcm = clip.channels.front();
    for (const auto& note : notes) {
        const auto frequency = 440.0 * std::pow(2.0, (note.key - 69) / 12.0);
        const auto first = static_cast<size_t>(std::floor(note.start * rate));
        const auto last = std::min(frames, static_cast<size_t>(std::ceil((note.end + release) * rate)));
        constexpr int harmonics=8;
        // Recurrence keeps the bounded worker synthesis independent of SoundFonts.
        std::array<double, 8> phase{}, step{}, partial{}, decay{};
        for (int h = 0; h < harmonics; ++h) {
            step[h] = 2 * std::numbers::pi * frequency * (h + 1) / rate;
            partial[h] = 1.0 / ((h + 1) * (h + 1));
            decay[h] = std::exp(-(1.2 + h * 0.7) / rate);
        }
        const auto& curve=curves[note.channel];size_t point=0;while(point+1<curve.size() && curve[point+1].time<=note.start)++point;ChannelSound current=curve[point].sound;double gain=current.volume*current.expression;bool pitchDirty=true;uint32_t noise=0x639137u+static_cast<uint32_t>(note.key)*713u+static_cast<uint32_t>(first);double previousNoise=0;
        for (size_t frame = first; frame < last; ++frame) {
            const auto time = static_cast<double>(frame) / rate;
            while(point+1<curve.size() && curve[point+1].time<=time){current=curve[++point].sound;pitchDirty=true;}
            if(pitchDirty){const double ratio=std::pow(2.,current.wheel*current.range/12);for(int h=0;h<harmonics;++h)step[h]=2*std::numbers::pi*frequency*ratio*(h+1)/rate;pitchDirty=false;}
            gain+=std::clamp(current.volume*current.expression-gain,-1/(rate*.005),1/(rate*.005));
            const auto envelope = std::clamp((time - note.start) / 0.003,0.,1.)
                * (time < note.end ? 1.0 : std::max(0.0, 1.0 - (time - note.end) / release));
            double value = 0;
            if(note.drum){const double t=std::max(0.,time-note.start);noise=noise*1664525u+1013904223u;const double white=static_cast<double>(noise)/UINT32_MAX*2-1,high=(white-previousNoise)*.5;previousNoise=white;
                if(note.key==35 || note.key==36)value=std::sin(2*std::numbers::pi*(45*t+60*(1-std::exp(-30*t))/30))*std::exp(-10*t)+white*.08*std::exp(-120*t);
                else if(note.key==38 || note.key==40)value=(white*.7+std::sin(2*std::numbers::pi*185*t)*.3)*std::exp(-18*t);
                else if(note.key==42 || note.key==44 || note.key==46)value=high*std::exp(-(note.key==46?9.:35.)*t);
                else if(note.key==49 || note.key==51 || note.key==52 || note.key==53 || note.key==55 || note.key==57 || note.key==59)value=(high*.7+std::sin(2*std::numbers::pi*1703*t)*.15+std::sin(2*std::numbers::pi*2309*t)*.15)*std::exp(-5*t);
                else value=(std::sin(2*std::numbers::pi*(90+(note.key%12)*11)*t)*.85+white*.15)*std::exp(-14*t);
            }
            for (int h = 0; !note.drum && h < harmonics; ++h) {
                if(step[h]<std::numbers::pi*.9)value += std::sin(phase[h]) * partial[h];
                phase[h] += step[h];
                if (phase[h] >= 2 * std::numbers::pi) phase[h] =std::fmod(phase[h],2*std::numbers::pi);
                partial[h] *= decay[h];
            }
            pcm[frame] += static_cast<float>(value * envelope * note.velocity * gain * 0.18);
        }
    }
    float peak = 0;
    for (const auto sample : pcm) peak = std::max(peak, std::abs(sample));
    if (peak > 0.8f) for (auto& sample : pcm) sample *= 0.8f / peak;
    return clip;
}
}
