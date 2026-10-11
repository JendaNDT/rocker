// SPDX-License-Identifier: AGPL-3.0-only
#include "media_clip.h"
#include <algorithm>
#include <cmath>
#include <fstream>
#include <iostream>
#include <limits>
#include <sstream>
#include <string>
using namespace rocker::media;
namespace {
int checks = 0, failures = 0;
std::ostringstream measurements;
bool firstMeasurement = true;
void check(bool condition, const std::string& name) {
    ++checks;
    if (!condition) { ++failures; std::cerr << "FAIL: " << name << "\n"; }
}
template<class Function> void rejects(Function operation, Error code, const char* name) {
    try { operation(); check(false, name); }
    catch (const Failure& error) { check(error.code == code, name); }
}
double rms(const std::vector<float>& data) {
    double sum = 0;
    for (float sample : data) sum += sample * sample;
    return std::sqrt(sum / data.size());
}
// Frequency from positive zero crossings in the middle half, avoiding codec
// priming and spectral boundary reconstruction. This is a synthetic sine check,
// not a claim about perceived quality on real guitar recordings.
double pitch(const Clip& clip, std::size_t channel = 0) {
    const auto& data = clip.channels[channel];
    double first = -1, last = -1;
    int crossings = 0;
    for (std::size_t i = data.size()/4 + 1; i < data.size()*3/4; ++i) {
        if (data[i-1] <= 0 && data[i] > 0) {
            const double position = i - 1 + (-data[i-1]) / (data[i] - data[i-1]);
            if (first < 0) first = position;
            last = position;
            ++crossings;
        }
    }
    return crossings > 1 ? (crossings-1)*clip.sampleRate/(last-first) : 0;
}
}
int main(int argc, char** argv) {
    if (argc != 2) return 2;
    const std::filesystem::path fixtures(argv[1]);
    try {
        for (const auto* file : {"a4-440.wav", "a4-440.mp3", "a4-440.ogg"}) {
            const auto clip = decode(fixtures / file);
            check(clip.channels.size() == 1 && clip.sampleRate == 48000, std::string(file) + " shape");
            check(std::abs(clip.frames()/clip.sampleRate - 1) < 0.05, std::string(file) + " duration");
            check(std::abs(pitch(clip) - 440) < 2, std::string(file) + " pitch");
            check(rms(clip.channels[0]) > 0.05, std::string(file) + " non-silent");
            for (double speed : {0.25, 0.5, 0.75, 1.0, 1.5, 2.0}) {
                const auto output = stretch(clip, speed);
                check(output.frames() == static_cast<std::size_t>(std::llround(clip.frames()/speed)), std::string(file) + " stretched duration");
                const auto frequency = pitch(output);
                std::cout << file << " speed=" << speed << " pitch=" << frequency << " frames=" << output.frames() << "\n";
                if (!firstMeasurement) measurements << ',';
                firstMeasurement = false;
                measurements << "{\"file\":\"" << file << "\",\"speed\":" << speed
                             << ",\"pitchHz\":" << frequency << ",\"frames\":" << output.frames() << '}';
                check(std::abs(frequency - 440) < 3, std::string(file) + " preserved pitch");
                check(rms(output.channels[0]) > 0.04, std::string(file) + " stretched non-silent");
            }
            check(stretch(clip, 1).channels == clip.channels, std::string(file) + " identity");
        }
        auto low = decode(fixtures / "low-e2.wav");
        for (double speed : {0.25, 0.5, 0.75, 1.5, 2.0})
            check(std::abs(pitch(stretch(low, speed)) - 82.406889) < 1.5, "low E pitch preserved");
        for (int active : {0, 1}) {
            auto stereo = decode(fixtures / (active == 0 ? "input-1-only.wav" : "input-2-only.wav"));
            check(stereo.channels.size() == 2 && stereo.frames() == 48000, "stereo shape");
            check(rms(stereo.channels[1-active]) == 0, "decoded channel isolation");
            auto slower = stretch(stereo, 0.5);
            check(rms(slower.channels[1-active]) < 1e-6, "stretched channel isolation");
            check(std::abs(pitch(slower, active) - (active == 0 ? 110 : 220)) < 2, "stereo pitch");
        }
        auto silent = decode(fixtures / "silence.wav");
        check(rms(stretch(silent, 0.5).channels[0]) == 0, "silence remains silent");
        auto impulse = stretch(decode(fixtures / "impulses.wav"), 0.5);
        double tailPeak = 0;
        for (std::size_t i = 84000; i < impulse.frames(); ++i) tailPeak = std::max(tailPeak, double(std::abs(impulse.channels[0][i])));
        check(tailPeak > 0.01, "flush preserves final impulse");
        auto repeated = stretch(decode(fixtures / "repeated-e2.wav"), 0.5);
        check(repeated.frames() == 192000 && rms(repeated.channels[0]) > 0.01, "repeated notes survive");
        rejects([&] { decode(fixtures / "missing.wav"); }, Error::invalidInput, "missing file");
        // Explicitly unsupported in this slice, never silently decoded as empty.
        rejects([&] { decode(fixtures / "a4-440.webm"); }, Error::unsupportedFormat, "WebM needs demuxer");
        rejects([&] { decode(fixtures / "a4-440.m4a"); }, Error::unsupportedFormat, "AAC needs Media Foundation");
        rejects([&] { decode(fixtures / "a4-440.wav", {1, 64000000, 128000000}); }, Error::limitExceeded, "encoded limit");
        rejects([&] { decode(fixtures / "a4-440.wav", {64000000, 1, 128000000}); }, Error::limitExceeded, "decoded limit");
        for (double rate : {0.0, -1.0, 0.24, 2.01, std::numeric_limits<double>::quiet_NaN(), std::numeric_limits<double>::infinity()})
            rejects([&] { stretch(low, rate); }, Error::invalidRate, "invalid speed");
        rejects([&] { stretch(low, 0.5, {64000000, 1, 128000000}); }, Error::limitExceeded, "output limit");
        rejects([&] { stretch(low, 1, {64000000, 64000000, 1}); }, Error::limitExceeded, "combined limit including identity copy");
        rejects([&] { stretch(Clip{48000, {{0, 0}}}, 0.5); }, Error::tooShort, "short clip");
        rejects([&] { stretch(Clip{48000, {{0, 0}, {0}}}, 1); }, Error::invalidInput, "unequal channels");
        rejects([&] { stretch(Clip{48000, {{std::numeric_limits<float>::quiet_NaN()}}}, 1); }, Error::invalidInput, "invalid PCM");
        rejects([&] { stretch(Clip{}, 1); }, Error::invalidInput, "empty PCM");
    } catch (const std::exception& error) { ++failures; std::cerr << error.what() << "\n"; }
    std::ofstream report("media-checks.json");
    report << "{\"result\":\"" << (failures ? "failed" : "passed") << "\",\"checks\":" << checks
           << ",\"measurements\":[" << measurements.str() << "],\"failures\":" << failures << ",\"decodedFormats\":[\"WAV\",\"MP3\",\"Ogg/Opus\"],\"speedRange\":[0.25,2],\"realGuitarListening\":\"deferred\"}\n";
    std::cout << checks - failures << "/" << checks << " media checks passed\n";
    return failures ? 1 : 0;
}
