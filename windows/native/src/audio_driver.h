// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include "tone_processor.h"
#include <memory>
#include <string>

namespace rocker {
struct DriverSnapshot {
    double sampleRate = 0;
    uint32_t bufferFrames = 0, outputChannels = 0;
    bool failed = false;
};
class AudioDriver {
public:
    virtual ~AudioDriver() = default;
    virtual bool start(ToneProcessor&, std::string& error) = 0;
    // Must stop and join callbacks before returning, including after failed start.
    virtual void stop() noexcept = 0;
    virtual DriverSnapshot snapshot() const noexcept = 0;
};
std::unique_ptr<AudioDriver> makeAudioDriver();
}
