// SPDX-License-Identifier: AGPL-3.0-only
#include "media_decoders.h"
#include <mkvparser/mkvparser.h>
// Reuse the exact Opus implementation built into pinned JUCE, not another codec.
#include <juce_audio_formats/codecs/opus/opus/include/opus.h>
#include <algorithm>
#include <cmath>
#include <cstring>
#include <fstream>
#include <limits>
#include <memory>

namespace rocker::media::detail {
namespace {
class Reader final : public mkvparser::IMkvReader {
public:
    Reader(const std::filesystem::path& path, Limits limits) : file_(path, std::ios::binary | std::ios::ate) {
        length_ = file_.is_open() ? static_cast<long long>(file_.tellg()) : -1;
        if (length_ <= 0) throw Failure(Error::decodeFailed, "Cannot read WebM file");
        if (static_cast<std::uint64_t>(length_) > limits.encodedBytes)
            throw Failure(Error::limitExceeded, "WebM exceeds encoded budget");
    }
    int Read(long long offset, long length, unsigned char* buffer) override {
        if (offset < 0 || length < 0 || offset > length_ || length > length_ - offset || (!buffer && length)) return -1;
        if (!length) return 0;
        file_.clear();
        file_.seekg(offset);
        file_.read(reinterpret_cast<char*>(buffer), length);
        return file_ ? 0 : -1;
    }
    int Length(long long* total, long long* available) override {
        if (total) *total = length_;
        if (available) *available = length_;
        return 0;
    }
private:
    std::ifstream file_;
    long long length_ = 0;
};
unsigned read16(const unsigned char* bytes) { return bytes[0] | (unsigned(bytes[1]) << 8); }
std::size_t nanosecondFrames(unsigned long long ns) {
    if (ns > 1365312500ull) throw Failure(Error::decodeFailed, "Invalid Opus delay or discard padding");
    return static_cast<std::size_t>(std::llround(ns * (48000.0 / 1000000000)));
}
}
Clip decodeWebm(const std::filesystem::path& path, Limits limits) {
    Reader reader(path, limits);
    mkvparser::EBMLHeader header;
    long long position = 0;
    if (header.Parse(&reader, position) != 0 || !header.m_docType || std::strcmp(header.m_docType, "webm") != 0)
        throw Failure(Error::decodeFailed, "Invalid WebM header");
    mkvparser::Segment* raw = nullptr;
    const auto created = mkvparser::Segment::CreateInstance(&reader, position, raw);
    std::unique_ptr<mkvparser::Segment> segment(raw);
    if (created != 0 || !segment || segment->Load() < 0)
        throw Failure(Error::decodeFailed, "Invalid or incomplete WebM segment");
    const auto* tracks = segment->GetTracks();
    if (!tracks || tracks->GetTracksCount() > 16) throw Failure(Error::unsupportedFormat, "Unsupported WebM track layout");
    const mkvparser::Track* track = nullptr;
    for (unsigned long i = 0; i < tracks->GetTracksCount(); ++i) {
        const auto* candidate = tracks->GetTrackByIndex(i);
        if (!candidate || candidate->GetType() != mkvparser::Track::kAudio) continue;
        if (track || !candidate->GetCodecId() || std::strcmp(candidate->GetCodecId(), "A_OPUS") != 0)
            throw Failure(Error::unsupportedFormat, "WebM requires exactly one Opus audio track");
        track = candidate;
    }
    if (!track || track->GetContentEncodingCount()) throw Failure(Error::unsupportedFormat, "Missing or encoded Opus track");
    size_t headerLength = 0;
    const auto* opusHeader = track->GetCodecPrivate(headerLength);
    if (!opusHeader || headerLength < 19 || std::memcmp(opusHeader, "OpusHead", 8) || opusHeader[8] > 15)
        throw Failure(Error::decodeFailed, "Invalid OpusHead");
    const int channels = opusHeader[9];
    if (channels < 1 || channels > 2 || opusHeader[18] != 0)
        throw Failure(Error::unsupportedFormat, "Unsupported Opus channel mapping");
    if (static_cast<const mkvparser::AudioTrack*>(track)->GetChannels() != channels)
        throw Failure(Error::decodeFailed, "OpusHead and WebM channels disagree");
    int error = OPUS_OK;
    std::unique_ptr<OpusDecoder, decltype(&opus_decoder_destroy)> decoder(
        opus_decoder_create(48000, channels, &error), &opus_decoder_destroy);
    if (!decoder || error != OPUS_OK) throw Failure(Error::decodeFailed, "Cannot initialize Opus decoder");
    const int gainValue = static_cast<int>(read16(opusHeader + 16));
    const int signedGain = gainValue < 32768 ? gainValue : gainValue - 65536;
    if (opus_decoder_ctl(decoder.get(), OPUS_SET_GAIN(signedGain)) != OPUS_OK)
        throw Failure(Error::decodeFailed, "Cannot set Opus output gain");
    std::size_t skip = track->GetCodecDelay() ? nanosecondFrames(track->GetCodecDelay()) : read16(opusHeader + 10);
    Clip clip{48000, std::vector<std::vector<float>>(channels)};
    std::vector<float> pcm(5760 * channels);
    std::vector<unsigned char> packet;
    const mkvparser::BlockEntry* entry = nullptr;
    if (track->GetFirst(entry) < 0) throw Failure(Error::decodeFailed, "Cannot read first Opus block");
    long long previousTime = -1;
    long long previousFrames = 0;
    while (entry && !entry->EOS()) {
        const auto* block = entry->GetBlock();
        if (!block || block->GetFrameCount() < 1) throw Failure(Error::decodeFailed, "Empty Opus block");
        const auto time = block->GetTime(entry->GetCluster());
        // Continuous MediaRecorder audio is supported. Do not silently remove
        // timeline gaps: discontinuous/editing-oriented containers need a later importer.
        if (time < 0 || (previousTime >= 0 && std::abs(double(time) - double(previousTime) - previousFrames * (1000000000.0 / 48000)) > 4000000))
            throw Failure(Error::unsupportedFormat, "Discontinuous WebM timeline");
        previousTime = time;
        previousFrames = 0;
        std::vector<std::vector<float>> blockPcm(channels);
        for (int f = 0; f < block->GetFrameCount(); ++f) {
            const auto& frame = block->GetFrame(f);
            if (frame.len < 1 || frame.len > 65536) throw Failure(Error::decodeFailed, "Invalid Opus packet size");
            packet.resize(static_cast<std::size_t>(frame.len));
            if (frame.Read(&reader, packet.data()) != 0) throw Failure(Error::decodeFailed, "Truncated Opus packet");
            const int decoded = opus_decode_float(decoder.get(), packet.data(), static_cast<opus_int32>(packet.size()), pcm.data(), 5760, 0);
            if (decoded <= 0) throw Failure(Error::decodeFailed, "Invalid Opus packet");
            previousFrames += decoded;
            if (static_cast<std::uint64_t>(previousFrames) > limits.pcmBytes / (channels * sizeof(float)))
                throw Failure(Error::limitExceeded, "WebM block exceeds PCM budget");
            for (int c = 0; c < channels; ++c)
                for (int n = 0; n < decoded; ++n) blockPcm[c].push_back(pcm[n * channels + c]);
        }
        std::size_t first = 0, last = blockPcm[0].size();
        const auto padding = block->GetDiscardPadding();
        if (padding > 0) {
            const auto trim = nanosecondFrames(static_cast<unsigned long long>(padding));
            if (trim > last) throw Failure(Error::decodeFailed, "Excessive Opus tail padding");
            last -= trim;
        } else if (padding < 0) {
            if (padding == std::numeric_limits<long long>::min()) throw Failure(Error::decodeFailed, "Invalid Opus padding");
            first = nanosecondFrames(static_cast<unsigned long long>(-padding));
            if (first > last) throw Failure(Error::decodeFailed, "Excessive Opus leading padding");
        }
        const auto preSkip = std::min(skip, last - first);
        first += preSkip;
        skip -= preSkip;
        const auto resultFrames = clip.frames() + last - first;
        if (resultFrames > limits.pcmBytes / (channels * sizeof(float)) || resultFrames > static_cast<std::size_t>(std::numeric_limits<int>::max()))
            throw Failure(Error::limitExceeded, "WebM PCM exceeds budget");
        for (int c = 0; c < channels; ++c) {
            for (std::size_t n = first; n < last; ++n) {
                const auto value = blockPcm[c][n];
                if (!std::isfinite(value)) throw Failure(Error::decodeFailed, "Non-finite Opus PCM");
                clip.channels[c].push_back(value);
            }
        }
        const mkvparser::BlockEntry* next = nullptr;
        if (track->GetNext(entry, next) < 0) throw Failure(Error::decodeFailed, "Cannot read next Opus block");
        entry = next;
    }
    if (clip.frames() == 0 || skip) throw Failure(Error::decodeFailed, "Incomplete Opus audio");
    return clip;
}
}
