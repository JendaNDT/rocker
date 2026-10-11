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
struct Note { double start, end; int key; float velocity; };
constexpr double rate = 48000, maximumDuration = 180, release = 0.08;
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
                throw Failure(Error::limitExceeded, "MIDI is limited to three minutes in this integration stage.");
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
        if (m.isPitchWheel() && m.getPitchWheelValue() != 8192)
            throw Failure(Error::unsupportedFormat, "Pitch bends require the later notation articulation engine.");
        if (m.isNoteOn()) {
            if (channel == 9) throw Failure(Error::unsupportedFormat, "Percussion requires the later notation instrument engine.");
            if (notes.size() >= 5000) throw Failure(Error::limitExceeded, "MIDI exceeds 5,000 notes.");
            const auto key = m.getNoteNumber();
            endNote(channel, key, time);
            active[channel][key] = static_cast<int>(notes.size());
            notes.push_back({time, -1, key, m.getFloatVelocity()});
        } else if (m.isNoteOff()) {
            const auto key = m.getNoteNumber();
            if (sustain[channel]) held[channel][key] = true;
            else endNote(channel, key, time);
        } else if (m.isController()) {
            const auto controller = m.getControllerNumber();
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
        const auto harmonics = std::min(8, static_cast<int>(rate * 0.45 / frequency));
        // Recurrence keeps the bounded worker synthesis independent of SoundFonts.
        std::array<double, 8> phase{}, step{}, partial{}, decay{};
        for (int h = 0; h < harmonics; ++h) {
            step[h] = 2 * std::numbers::pi * frequency * (h + 1) / rate;
            partial[h] = 1.0 / ((h + 1) * (h + 1));
            decay[h] = std::exp(-(1.2 + h * 0.7) / rate);
        }
        for (size_t frame = first; frame < last; ++frame) {
            const auto time = static_cast<double>(frame) / rate;
            const auto envelope = std::min(1.0, (time - note.start) / 0.003)
                * (time < note.end ? 1.0 : std::max(0.0, 1.0 - (time - note.end) / release));
            double value = 0;
            for (int h = 0; h < harmonics; ++h) {
                value += std::sin(phase[h]) * partial[h];
                phase[h] += step[h];
                if (phase[h] >= 2 * std::numbers::pi) phase[h] -= 2 * std::numbers::pi;
                partial[h] *= decay[h];
            }
            pcm[frame] += static_cast<float>(value * envelope * note.velocity * 0.18);
        }
    }
    float peak = 0;
    for (const auto sample : pcm) peak = std::max(peak, std::abs(sample));
    if (peak > 0.8f) for (auto& sample : pcm) sample *= 0.8f / peak;
    return clip;
}
}
