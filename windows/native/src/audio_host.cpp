// SPDX-License-Identifier: AGPL-3.0-only
#include "rocker_audio.h"
#include "audio_driver.h"
#include <algorithm>
#include <cstring>
#include <mutex>

static_assert(sizeof(rocker_audio_config) == 24);
static_assert(sizeof(rocker_audio_snapshot) == 40);
struct rocker_audio_host {
    explicit rocker_audio_host(const rocker_audio_config& config)
        : tone(config.frequency_hz, config.amplitude), driver(rocker::makeAudioDriver()) {}
    ~rocker_audio_host() { tone.enable(false); driver->stop(); }
    std::mutex control;
    rocker::ToneProcessor tone;
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
rocker_audio_result ROCKER_CALL rocker_audio_start(rocker_audio_handle host) {
    return withHost(host, [](auto& h) -> rocker_audio_result {
        if (h.state == ROCKER_RUNNING && !h.driver->snapshot().failed) return ROCKER_OK;
        h.driver->stop();
        h.error.clear();
        h.tone.enable(true);
        try {
            if (h.driver->start(h.tone, h.error)) { h.state = ROCKER_RUNNING; return ROCKER_OK; }
        } catch (...) {
            h.tone.enable(false); h.driver->stop(); h.state = ROCKER_FAULTED;
            return ROCKER_INTERNAL_ERROR;
        }
        h.tone.enable(false); h.driver->stop(); h.state = ROCKER_FAULTED;
        return ROCKER_DEVICE_ERROR;
    });
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
        const auto device = h.driver->snapshot();
        if (h.state == ROCKER_RUNNING && device.failed) {
            h.tone.enable(false); h.state = ROCKER_FAULTED;
            h.error = "Audio device stopped unexpectedly. Stop and start the test again.";
        }
        *out = {sizeof(*out), ROCKER_AUDIO_ABI_VERSION, h.state, 0,
            device.sampleRate, device.bufferFrames, device.outputChannels, h.tone.frames()};
        return ROCKER_OK;
    });
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
