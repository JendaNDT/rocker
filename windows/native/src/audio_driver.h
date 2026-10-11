// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include "render_processor.h"
#include <memory>
#include <string>

namespace rocker {
struct DriverSnapshot {
    double sampleRate = 0;
    uint32_t bufferFrames = 0, outputChannels = 0;
    bool failed = false;
    uint32_t failureReason=0;
};
struct DeviceSelection {
    std::string backend, input, output;
    double rate = 0;
    uint32_t frames = 0, outputFirst = 0;
    int32_t inputChannel = -1;
};
class AudioDriver {
public:
    virtual ~AudioDriver() = default;
    virtual bool start(RenderProcessor&, std::string& error) = 0;
    // Must stop and join callbacks before returning, including after failed start.
    virtual void stop() noexcept = 0;
    virtual DriverSnapshot snapshot() const noexcept = 0;
    virtual std::string catalog() = 0;
    virtual std::string capabilities(const DeviceSelection&) = 0;
    virtual bool configure(const DeviceSelection&, std::string& error) = 0;
    virtual int64_t xrunCount() = 0;
    virtual std::string details() = 0;
    virtual bool controlPanel(std::string& error) = 0;
};
std::unique_ptr<AudioDriver> makeAudioDriver();
}
