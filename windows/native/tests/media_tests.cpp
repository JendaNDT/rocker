// SPDX-License-Identifier: AGPL-3.0-only
#include "media_clip.h"
#include <algorithm>
#include <cmath>
#include <chrono>
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
struct TemporaryDirectory {
    std::filesystem::path path;
    TemporaryDirectory() {
        for (int attempt = 0; attempt < 10; ++attempt) {
            path = std::filesystem::temp_directory_path() / ("rocker-media-" + std::to_string(std::chrono::steady_clock::now().time_since_epoch().count()));
            if (std::filesystem::create_directory(path)) return;
        }
        throw std::runtime_error("Cannot create temporary fixtures");
    }
    ~TemporaryDirectory() { std::error_code ignored; std::filesystem::remove_all(path, ignored); }
};
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
    if (argc != 3) return 2;
    const std::filesystem::path fixtures(argv[1]);
    const std::filesystem::path additional(argv[2]);
    try {
        TemporaryDirectory temporary;
        std::vector<const char*> formats{"a4-440.wav", "a4-440.mp3", "a4-440.ogg", "a4-440.webm"};
#ifdef _WIN32
        formats.push_back("a4-440.m4a");
#endif
        for (const auto* file : formats) {
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
        check(stretch(low, 0.25).channels == stretch(low, 0.25).channels, "repeatable quarter-speed render");
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
#ifndef _WIN32
        rejects([&] { decode(fixtures / "a4-440.m4a"); }, Error::platformUnavailable, "AAC requires Windows Media Foundation");
#endif
        check(decode(fixtures / "a4-440.webm").frames() == 48000, "WebM codec delay and discard padding");
        rejects([&] { decode(fixtures / "a4-440.webm", {64000000, 1, 128000000}); }, Error::limitExceeded, "WebM decoded budget");
#ifdef _WIN32
        rejects([&] { decode(fixtures / "a4-440.m4a", {64000000, 1, 128000000}); }, Error::limitExceeded, "AAC decoded budget");
#endif
        const auto renamed = temporary.path / "recording.bin";
        std::filesystem::copy_file(fixtures / "a4-440.webm", renamed);
        check(decode(renamed).frames() == 48000, "WebM content detection without extension");
        std::ifstream original(fixtures / "a4-440.webm", std::ios::binary);
        std::vector<char> encoded((std::istreambuf_iterator<char>(original)), {});
        const auto head = std::search(encoded.begin(), encoded.end(), "OpusHead", "OpusHead" + 8);
        if (head == encoded.end()) throw std::runtime_error("Reference OpusHead missing");
        const auto index = static_cast<std::size_t>(head - encoded.begin());
        auto writeFixture = [&](const char* name, const std::vector<char>& bytes) {
            const auto path = temporary.path / name;
            std::ofstream out(path, std::ios::binary);
            out.write(bytes.data(), static_cast<std::streamsize>(bytes.size()));
            if (!out) throw std::runtime_error("Cannot write malformed fixture");
            return path;
        };
        auto truncated = encoded;
        truncated.resize(truncated.size()/2);
        auto malformed = writeFixture("truncated.webm", truncated);
        rejects([&] { decode(malformed); }, Error::decodeFailed, "truncated WebM");
        auto mapping = encoded;
        mapping[index + 18] = 3;
        malformed = writeFixture("mapping.webm", mapping);
        rejects([&] { decode(malformed); }, Error::unsupportedFormat, "unsupported Opus mapping");
        auto invalidHead = encoded;
        invalidHead[index] = 'X';
        malformed = writeFixture("header.webm", invalidHead);
        rejects([&] { decode(malformed); }, Error::decodeFailed, "invalid OpusHead");
        auto stereo = decode(additional / "input-2-only.webm");
        check(stereo.channels.size() == 2 && stereo.frames() == 48000, "WebM stereo shape");
        check(rms(stereo.channels[0]) < 0.005 && rms(stereo.channels[1]) > 0.05, "WebM selected channel isolation");
        check(std::abs(pitch(stereo, 1) - 220) < 2, "WebM stereo pitch");
        check(rms(stretch(stereo, 0.5).channels[0]) < 0.005, "WebM stretched channel isolation");
#ifdef _WIN32
        auto aacStereo = decode(additional / "input-2-only.m4a");
        check(aacStereo.channels.size() == 2 && aacStereo.sampleRate == 48000, "AAC stereo shape");
        check(rms(aacStereo.channels[0]) < 0.005 && rms(aacStereo.channels[1]) > 0.05, "AAC channel isolation");
        check(std::abs(pitch(aacStereo, 1) - 220) < 2, "AAC stereo pitch");
        check(rms(stretch(aacStereo, 0.5).channels[0]) < 0.005, "AAC stretched channel isolation");
        const auto unicode = temporary.path / std::filesystem::path(u8"nahrávka-žluťoučký.m4a");
        std::filesystem::copy_file(fixtures / "a4-440.m4a", unicode);
        check(std::abs(pitch(decode(unicode)) - 440) < 2, "AAC Unicode path");
        malformed = writeFixture("invalid.m4a", {0,0,0,24,'f','t','y','p','M','4','A',' '});
        rejects([&] { decode(malformed); }, Error::decodeFailed, "invalid MP4");
#endif
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
           << ",\"measurements\":[" << measurements.str() << "],\"failures\":" << failures << ",\"decodedFormats\":[\"WAV\",\"MP3\",\"Ogg/Opus\",\"WebM/Opus\""
#ifdef _WIN32
           << ",\"MP4/AAC\""
#endif
           << "],\"speedRange\":[0.25,2],\"realGuitarListening\":\"deferred\"}\n";
    std::cout << checks - failures << "/" << checks << " media checks passed\n";
    return failures ? 1 : 0;
}
