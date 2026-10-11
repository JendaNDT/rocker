// SPDX-License-Identifier: AGPL-3.0-only
#include "rocker_audio.h"
#include "audio_driver.h"
#include <algorithm>
#include <cstring>
#include <mutex>
#if ROCKER_WITH_MEDIA
#include "media_clip.h"
#endif

static_assert(sizeof(rocker_audio_config) == 24);
static_assert(sizeof(rocker_audio_snapshot) == 40);
static_assert(sizeof(rocker_transport_snapshot) == 56);
struct rocker_audio_host {
    explicit rocker_audio_host(const rocker_audio_config& config)
        : tone(config.frequency_hz, config.amplitude), driver(rocker::makeAudioDriver()) {}
    ~rocker_audio_host() { tone.enable(false); driver->stop(); }
    std::mutex control;
    rocker::RenderProcessor tone;
    std::unique_ptr<rocker::AudioDriver> driver;
    std::string error;
    uint32_t state = ROCKER_STOPPED;
};

template <typename Operation>
static rocker_audio_result withHost(rocker_audio_handle host, Operation operation) noexcept {
    if (!host) return ROCKER_INVALID_ARGUMENT;
    try {
        const std::lock_guard lock(host->control);
        return operation(*host);
    } catch (...) { return ROCKER_INTERNAL_ERROR; }
}

uint32_t ROCKER_CALL rocker_audio_get_abi_version() { return ROCKER_AUDIO_ABI_VERSION; }
rocker_audio_result ROCKER_CALL rocker_audio_create(const rocker_audio_config* config, rocker_audio_handle* output) {
    if (!output) return ROCKER_INVALID_ARGUMENT;
    *output = nullptr;
    if (!config) return ROCKER_INVALID_ARGUMENT;
    if (config->struct_size != sizeof(*config) || config->abi_version != ROCKER_AUDIO_ABI_VERSION)
        return ROCKER_ABI_MISMATCH;
    if (!std::isfinite(config->frequency_hz) || !std::isfinite(config->amplitude)
        || config->frequency_hz < 20 || config->frequency_hz > 2000
        || config->amplitude <= 0 || config->amplitude > 0.05) return ROCKER_INVALID_ARGUMENT;
    try { *output = new rocker_audio_host(*config); return ROCKER_OK; }
    catch (...) { return ROCKER_INTERNAL_ERROR; }
}
static rocker_audio_result startHost(rocker_audio_host& h, bool media) {
    if (media && !h.tone.hasClip()) { h.error = "No prepared media is available."; return ROCKER_NOT_READY; }
    if (h.state == ROCKER_RUNNING && !h.driver->snapshot().failed && h.tone.clipMode() == media && (!media || !h.tone.ended())) return ROCKER_OK;
    h.driver->stop();
    h.error.clear();
    if (media) { h.tone.selectClip(); if (h.tone.ended()) h.tone.seek(0); }
    else h.tone.selectTone();
    h.tone.enable(true);
    try {
        if (h.driver->start(h.tone, h.error)) { h.state = ROCKER_RUNNING; return ROCKER_OK; }
    } catch (...) {
        h.error = "The audio driver could not start.";
        h.tone.enable(false); h.driver->stop(); h.state = ROCKER_FAULTED;
        return ROCKER_INTERNAL_ERROR;
    }
    h.tone.enable(false); h.driver->stop(); h.state = ROCKER_FAULTED;
    return ROCKER_DEVICE_ERROR;
}
rocker_audio_result ROCKER_CALL rocker_audio_start(rocker_audio_handle host) {
    return withHost(host, [](auto& h) { return startHost(h, false); });
}
rocker_audio_result ROCKER_CALL rocker_audio_play(rocker_audio_handle host) {
    return withHost(host, [](auto& h) { return startHost(h, true); });
}
static void refreshHost(rocker_audio_host& h) {
    if (h.state != ROCKER_RUNNING) return;
    if (h.driver->snapshot().failed) {
        h.tone.enable(false); h.driver->stop(); h.state = ROCKER_FAULTED;
        h.error = "Audio device stopped unexpectedly. Stop and start playback again.";
    } else if (h.tone.clipMode() && h.tone.ended()) {
        h.tone.enable(false); h.driver->stop(); h.state = ROCKER_STOPPED;
    }
}
rocker_audio_result ROCKER_CALL rocker_audio_stop(rocker_audio_handle host) {
    return withHost(host, [](auto& h) -> rocker_audio_result {
        h.tone.enable(false); h.driver->stop(); h.state = ROCKER_STOPPED;
        return ROCKER_OK;
    });
}
rocker_audio_result ROCKER_CALL rocker_audio_get_snapshot(rocker_audio_handle host, rocker_audio_snapshot* out) {
    if (!out) return ROCKER_INVALID_ARGUMENT;
    if (out->struct_size != sizeof(*out) || out->abi_version != ROCKER_AUDIO_ABI_VERSION)
        return ROCKER_ABI_MISMATCH;
    return withHost(host, [out](auto& h) -> rocker_audio_result {
        refreshHost(h);
        const auto device = h.driver->snapshot();
        *out = {sizeof(*out), ROCKER_AUDIO_ABI_VERSION, h.state, 0,
            device.sampleRate, device.bufferFrames, device.outputChannels, h.tone.frames()};
        return ROCKER_OK;
    });
}
rocker_audio_result ROCKER_CALL rocker_audio_seek(rocker_audio_handle host, double seconds) {
    if (!std::isfinite(seconds) || seconds < 0) return ROCKER_INVALID_ARGUMENT;
    return withHost(host, [seconds](auto& h) -> rocker_audio_result {
        if (!h.tone.hasClip()) return ROCKER_NOT_READY;
        if (seconds > h.tone.duration()) return ROCKER_INVALID_ARGUMENT;
        h.tone.enable(false); h.driver->stop(); h.state = ROCKER_STOPPED;
        h.tone.selectClip(); h.tone.seek(seconds); return ROCKER_OK;
    });
}
rocker_audio_result ROCKER_CALL rocker_audio_set_volume(rocker_audio_handle host, double volume) {
    if (!std::isfinite(volume) || volume < 0 || volume > 1) return ROCKER_INVALID_ARGUMENT;
    return withHost(host, [volume](auto& h) -> rocker_audio_result { h.tone.volume(volume); return ROCKER_OK; });
}
rocker_audio_result ROCKER_CALL rocker_audio_get_transport(rocker_audio_handle host, rocker_transport_snapshot* out) {
    if (!out) return ROCKER_INVALID_ARGUMENT;
    if (out->struct_size != sizeof(*out) || out->abi_version != ROCKER_AUDIO_ABI_VERSION) return ROCKER_ABI_MISMATCH;
    return withHost(host, [out](auto& h) -> rocker_audio_result {
        refreshHost(h);
        *out = {sizeof(*out), ROCKER_AUDIO_ABI_VERSION, h.tone.hasClip() ? 1u : 0u,
            h.state == ROCKER_RUNNING && h.tone.clipMode() ? 1u : 0u, h.tone.ended() ? 1u : 0u, 0,
            h.tone.duration(), h.tone.position(), h.tone.speed(), h.tone.volume()};
        return ROCKER_OK;
    });
}
#if ROCKER_WITH_MEDIA
template <typename Decode> static rocker_audio_result prepareMedia(rocker_audio_handle host, double speed, Decode decode) noexcept {
    if (!host || !std::isfinite(speed) || speed < 0.25 || speed > 2) return ROCKER_INVALID_ARGUMENT;
    try {
        auto original = decode();
        auto clip = speed == 1 ? std::move(original) : rocker::media::stretch(original, speed);
        return withHost(host, [&](auto& h) -> rocker_audio_result {
            h.tone.enable(false); h.driver->stop(); h.state = ROCKER_STOPPED;
            h.tone.install(std::move(clip), speed); h.error.clear(); return ROCKER_OK;
        });
    } catch (const std::exception& error) {
        return withHost(host, [&](auto& h) -> rocker_audio_result { h.error = error.what(); return ROCKER_MEDIA_ERROR; });
    } catch (...) { return ROCKER_INTERNAL_ERROR; }
}
#endif
rocker_audio_result ROCKER_CALL rocker_audio_prepare_file(rocker_audio_handle host, const char* path, double speed) {
    if (!host || !path || !*path) return ROCKER_INVALID_ARGUMENT;
#if ROCKER_WITH_MEDIA
    // Bound the borrowed path before constructing any path/string allocation.
    size_t length = 0;
    while (length < 32768 && path[length]) ++length;
    if (length == 32768) return ROCKER_INVALID_ARGUMENT;
    return prepareMedia(host, speed, [&] { return rocker::media::decode(std::filesystem::path(std::u8string(reinterpret_cast<const char8_t*>(path), length))); });
#else
    (void)speed;
    return ROCKER_NOT_READY;
#endif
}
rocker_audio_result ROCKER_CALL rocker_audio_prepare_midi(rocker_audio_handle host, const uint8_t* data, uint32_t count, double speed) {
    if (!host || !data || !count) return ROCKER_INVALID_ARGUMENT;
#if ROCKER_WITH_MEDIA
    return prepareMedia(host, speed, [&] { return rocker::media::synthesizeMidi(data, count); });
#else
    (void)speed;
    return ROCKER_NOT_READY;
#endif
}
rocker_audio_result ROCKER_CALL rocker_audio_copy_last_error(rocker_audio_handle host, char* buffer, uint32_t capacity, uint32_t* required) {
    if (!required || (capacity > 0 && !buffer)) return ROCKER_INVALID_ARGUMENT;
    return withHost(host, [=](auto& h) -> rocker_audio_result {
        *required = static_cast<uint32_t>(h.error.size() + 1);
        if (capacity == 0) return ROCKER_OK;
        const auto count = std::min(static_cast<size_t>(capacity - 1), h.error.size());
        std::memcpy(buffer, h.error.data(), count); buffer[count] = '\0';
        return capacity < *required ? ROCKER_BUFFER_TOO_SMALL : ROCKER_OK;
    });
}
void ROCKER_CALL rocker_audio_destroy(rocker_audio_handle host) {
    try { delete host; } catch (...) { /* Never unwind through the C ABI. */ }
}
