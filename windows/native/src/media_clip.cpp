// SPDX-License-Identifier: AGPL-3.0-only
#include "media_clip.h"
#include "media_decoders.h"
#include <juce_audio_formats/juce_audio_formats.h>
#include <signalsmith-stretch/signalsmith-stretch.h>
#include <algorithm>
#include <cmath>
#include <cstring>
#include <limits>
#include <memory>

namespace rocker::media {
namespace {
void validateShape(double rate, std::size_t channels, std::uint64_t frames, Limits limits) {
    if (!std::isfinite(rate) || rate < 8000 || rate > 192000 || channels < 1 || channels > 2 || frames == 0)
        throw Failure(Error::invalidInput, "Unsupported PCM shape");
    if (frames > static_cast<std::uint64_t>(std::numeric_limits<int>::max()) ||
        frames > limits.pcmBytes / (channels * sizeof(float)))
        throw Failure(Error::limitExceeded, "Decoded PCM exceeds memory budget");
}
juce::String fileName(const std::filesystem::path& path) {
#ifdef _WIN32
    return juce::String(path.c_str());
#else
    return juce::String::fromUTF8(path.c_str());
#endif
}
}
Clip decode(const std::filesystem::path& path, Limits limits) {
    const juce::File file(fileName(std::filesystem::absolute(path)));
    if (!file.existsAsFile() || file.getSize() <= 0)
        throw Failure(Error::invalidInput, "Audio file is missing or empty");
    if (static_cast<std::uint64_t>(file.getSize()) > limits.encodedBytes)
        throw Failure(Error::limitExceeded, "Encoded audio exceeds memory budget");
    juce::AudioFormatManager formats;
    formats.registerBasicFormats();
    // Probe content, not extension: legacy .ogg fixtures contain Opus, while
    // JUCE's OpusAudioFormat advertises only .opus.
    auto stream = file.createInputStream();
    if (!stream) throw Failure(Error::decodeFailed, "Cannot open the audio stream");
    if (static_cast<std::uint64_t>(stream->getTotalLength()) > limits.encodedBytes)
        throw Failure(Error::limitExceeded, "Opened audio exceeds memory budget");
    unsigned char signature[12]{};
    const auto signatureBytes = stream->read(signature, sizeof(signature));
    stream->setPosition(0);
    if (signatureBytes >= 4 && signature[0] == 0x1a && signature[1] == 0x45 && signature[2] == 0xdf && signature[3] == 0xa3)
        return detail::decodeWebm(path, limits);
    if (signatureBytes >= 8 && std::memcmp(signature + 4, "ftyp", 4) == 0)
        return detail::decodeMp4(path, limits);
    std::unique_ptr<juce::AudioFormatReader> reader(formats.createReaderFor(std::move(stream)));
    if (!reader) throw Failure(Error::unsupportedFormat, "No decoder recognized the audio content");
    if (reader->lengthInSamples <= 0) throw Failure(Error::decodeFailed, "Audio has no decoded frames");
    validateShape(reader->sampleRate, reader->numChannels, reader->lengthInSamples, limits);
    Clip clip{reader->sampleRate, std::vector<std::vector<float>>(reader->numChannels)};
    std::vector<float*> pointers;
    for (auto& channel : clip.channels) {
        channel.resize(static_cast<std::size_t>(reader->lengthInSamples));
        pointers.push_back(channel.data());
    }
    if (!reader->read(pointers.data(), static_cast<int>(pointers.size()), 0,
                      static_cast<int>(reader->lengthInSamples)))
        throw Failure(Error::decodeFailed, "Audio decoder failed to read PCM");
    for (const auto& channel : clip.channels)
        for (float sample : channel)
            if (!std::isfinite(sample)) throw Failure(Error::decodeFailed, "Non-finite decoded audio");
    return clip;
}
Clip stretch(const Clip& input, double speed, Limits limits) {
    if (!std::isfinite(speed) || speed < 0.25 || speed > 2)
        throw Failure(Error::invalidRate, "Playback speed must be within 0.25x-2x");
    validateShape(input.sampleRate, input.channels.size(), input.frames(), limits);
    for (const auto& channel : input.channels) {
        if (channel.size() != input.frames()) throw Failure(Error::invalidInput, "Unequal PCM channel lengths");
        for (float sample : channel)
            if (!std::isfinite(sample)) throw Failure(Error::invalidInput, "Non-finite source PCM");
    }
    const auto frames = static_cast<std::uint64_t>(std::llround(input.frames() / speed));
    validateShape(input.sampleRate, input.channels.size(), frames, limits);
    const auto bytesPerFrame = input.channels.size() * sizeof(float);
    if (frames + input.frames() > limits.combinedPcmBytes / bytesPerFrame)
        throw Failure(Error::limitExceeded, "Source and stretched PCM exceed memory budget");
    if (speed == 1) return input;
    // Large time changes randomize spectral phases. Use an explicit engine/seed
    // so repeated offline renders can be cached and compared reproducibly.
    signalsmith::stretch::SignalsmithStretch<float, std::mt19937> processor(0);
    processor.presetDefault(static_cast<int>(input.channels.size()), static_cast<float>(input.sampleRate));
    processor.setTransposeFactor(1);
    if (input.frames() < static_cast<std::size_t>(std::ceil(processor.outputSeekLength(speed))))
        throw Failure(Error::tooShort, "Clip is too short for pitch-preserving stretching");
    Clip result{input.sampleRate, std::vector<std::vector<float>>(input.channels.size())};
    std::vector<const float*> sources;
    std::vector<float*> outputs;
    for (std::size_t c = 0; c < input.channels.size(); ++c) {
        result.channels[c].resize(static_cast<std::size_t>(frames));
        sources.push_back(input.channels[c].data());
        outputs.push_back(result.channels[c].data());
    }
    // exact() performs pre-roll and flush internally, removing algorithmic
    // delay from this offline result instead of truncating the end of the clip.
    if (!processor.exact(sources, static_cast<int>(input.frames()), outputs, static_cast<int>(frames)))
        throw Failure(Error::tooShort, "Clip is too short for pitch-preserving stretching");
    for (const auto& channel : result.channels)
        for (float sample : channel)
            if (!std::isfinite(sample)) throw Failure(Error::decodeFailed, "Non-finite stretched PCM");
    return result;
}
}
