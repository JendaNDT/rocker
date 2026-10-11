// SPDX-License-Identifier: AGPL-3.0-only
#include "rocker_audio.h"
#include "tone_processor.h"
#include <array>
#include <chrono>
#include <cmath>
#include <cstring>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <thread>
#include <vector>

extern "C" void ROCKER_CALL rocker_test_set_failure(int enabled);
extern "C" void ROCKER_CALL rocker_test_set_start_delay(uint32_t);
extern "C" double ROCKER_CALL rocker_test_last_peak();

void check(bool condition, const char* message) { if (!condition) throw std::runtime_error(message); }
int main() {
    try {
        rocker_audio_config config{sizeof(config), ROCKER_AUDIO_ABI_VERSION, 440, 0.02};
        rocker_audio_handle host = nullptr;
        check(rocker_audio_create(nullptr, &host) == ROCKER_INVALID_ARGUMENT && !host, "null configuration");
        config.abi_version++;
        check(rocker_audio_create(&config, &host) == ROCKER_ABI_MISMATCH && !host, "ABI mismatch rejected");
        config.abi_version = ROCKER_AUDIO_ABI_VERSION;
        config.struct_size--;
        check(rocker_audio_create(&config, &host) == ROCKER_ABI_MISMATCH, "size mismatch rejected");
        config.struct_size = sizeof(config);
        config.amplitude = std::numeric_limits<double>::quiet_NaN();
        check(rocker_audio_create(&config, &host) == ROCKER_INVALID_ARGUMENT, "NaN rejected");
        config.amplitude = 0.1;
        check(rocker_audio_create(&config, &host) == ROCKER_INVALID_ARGUMENT, "high level rejected");
        config.amplitude = 0.02;
        check(rocker_audio_create(&config, &host) == ROCKER_OK && host, "create");
        rocker_test_set_failure(1);
        check(rocker_audio_start(host) == ROCKER_DEVICE_ERROR, "device failure surfaced");
        uint32_t required = 0;
        check(rocker_audio_copy_last_error(host, nullptr, 0, &required) == ROCKER_OK && required > 3, "error size query");
        std::array<char, 3> shortBuffer{'x', 'x', '!'};
        check(rocker_audio_copy_last_error(host, shortBuffer.data(), 2, &required) == ROCKER_BUFFER_TOO_SMALL
            && shortBuffer[1] == '\0' && shortBuffer[2] == '!', "bounded terminated error copy");
        rocker_test_set_failure(0);
        for (int i = 0; i < 20; ++i) {
            check(rocker_audio_start(host) == ROCKER_OK, "start");
            check(rocker_audio_start(host) == ROCKER_OK, "repeated start");
            rocker_audio_snapshot state{sizeof(state), ROCKER_AUDIO_ABI_VERSION};
            check(rocker_audio_get_snapshot(host, &state) == ROCKER_OK, "snapshot");
            check(state.state == ROCKER_RUNNING && state.output_channels == 2 && state.rendered_frames == 1024,
                "running stereo state after fake render");
            check(rocker_audio_stop(host) == ROCKER_OK && rocker_audio_stop(host) == ROCKER_OK, "idempotent stop");
            check(rocker_audio_get_snapshot(host, &state) == ROCKER_OK && state.state == ROCKER_STOPPED, "stopped");
        }
        rocker_test_set_start_delay(1000);
        std::atomic<bool> slowStarted{false};
        std::thread slow([&]{slowStarted.store(rocker_audio_start(host)==ROCKER_OK);});
        rocker_audio_snapshot phase{sizeof(phase),ROCKER_AUDIO_ABI_VERSION};
        const auto deadline=std::chrono::steady_clock::now()+std::chrono::seconds(3);
        do{rocker_audio_get_snapshot(host,&phase);if(phase.state==ROCKER_STARTING)break;std::this_thread::yield();}while(std::chrono::steady_clock::now()<deadline);
        check(phase.state==ROCKER_STARTING,"starting phase published before vendor I/O");
        const auto observed=std::chrono::steady_clock::now();
        check(rocker_audio_get_snapshot(host,&phase)==ROCKER_OK,"cached state during slow start");
        rocker_transport_snapshot pending{sizeof(pending),ROCKER_AUDIO_ABI_VERSION};
        check(rocker_audio_get_transport(host,&pending)==ROCKER_OK,"cached transport during slow start");
        check(rocker_audio_set_volume(host,0.2)==ROCKER_OK,"volume does not wait for slow device");
        check(rocker_audio_request_stop(host)==ROCKER_OK,"nonblocking panic during slow start");
        check(std::chrono::steady_clock::now()-observed<std::chrono::milliseconds(200),"telemetry and silence do not wait for 1s vendor delay");
        slow.join();rocker_test_set_start_delay(0);
        check(slowStarted.load() && rocker_audio_get_snapshot(host,&phase)==ROCKER_OK && phase.state==ROCKER_STOPPED,"cancelled old start never becomes running");
        check(rocker_test_last_peak()==0,"cancelled start produces no audible samples");
        std::atomic<bool> concurrentOk{true};
        std::vector<std::thread> callers;
        for (int i = 0; i < 4; ++i) callers.emplace_back([&] {
            for (int j = 0; j < 100; ++j) {
                rocker_audio_snapshot state{sizeof(state), ROCKER_AUDIO_ABI_VERSION};
                if (rocker_audio_start(host) != ROCKER_OK || rocker_audio_get_snapshot(host, &state) != ROCKER_OK
                    || rocker_audio_stop(host) != ROCKER_OK) concurrentOk.store(false);
            }
        });
        for (auto& caller : callers) caller.join();
        check(concurrentOk.load(), "concurrent control calls");
        rocker_audio_start(host);
        rocker_audio_stop(host);
        uint32_t jsonSize=0;std::array<char,2048> deviceJson{};
        check(rocker_audio_copy_devices(host,nullptr,0,&jsonSize)==ROCKER_OK && jsonSize>100,"device catalog size");
        check(rocker_audio_copy_devices(host,deviceJson.data(),1,&jsonSize)==ROCKER_BUFFER_TOO_SMALL && deviceJson[0]==0,"small JSON buffer remains terminated");
        check(rocker_audio_copy_devices(host,deviceJson.data(),deviceJson.size(),&jsonSize)==ROCKER_OK && std::strstr(deviceJson.data(),"Test ASIO"),"native catalog contents");
        rocker_device_config chosen{sizeof(chosen),ROCKER_AUDIO_ABI_VERSION,44100,128,1,0,0};
        check(rocker_audio_configure_device(host,&chosen,"Test ASIO","Test interface","Test interface")==ROCKER_OK,"explicit mono channel configuration");
        check(rocker_audio_copy_device_details(host,deviceJson.data(),deviceJson.size(),&jsonSize)==ROCKER_OK && std::strstr(deviceJson.data(),"44100"),"actual configured details");
        chosen.buffer_frames=512;check(rocker_audio_configure_device(host,&chosen,"Test ASIO","Test interface","Test interface")==ROCKER_DEVICE_ERROR,"unsupported format rejected");
        check(rocker_audio_copy_device_details(host,deviceJson.data(),deviceJson.size(),&jsonSize)==ROCKER_OK && std::strstr(deviceJson.data(),"128"),"failed selection preserves last configuration");
        chosen.abi_version=ROCKER_AUDIO_ABI_VERSION-1;check(rocker_audio_configure_device(host,&chosen,"Test ASIO","Test interface","Test interface")==ROCKER_ABI_MISMATCH,"device config ABI version validated");
        rocker_audio_destroy(host);
        rocker_audio_destroy(nullptr);
        check(rocker_audio_stop(nullptr) == ROCKER_INVALID_ARGUMENT, "null handle rejected");

        rocker::ToneProcessor tone(440, 0.02);
        tone.prepare(48000); tone.enable(true);
        std::array<float, 48000> left{}, right{};
        float* output[]{left.data(), right.data()};
        tone.render(output, 2, static_cast<int>(left.size()));
        int crossings = 0;
        double energy = 0;
        for (size_t i = 0; i < left.size(); ++i) {
            check(std::isfinite(left[i]) && std::abs(left[i]) <= 0.020001f, "finite bounded samples");
            check(left[i] == right[i], "identical mono tone in both channels");
            if (i && left[i - 1] < 0 && left[i] >= 0) ++crossings;
            energy += left[i] * left[i];
        }
        check(crossings >= 439 && crossings <= 440 && energy > 1, "440 Hz non-silent signal");
        tone.enable(false); tone.render(output, 2, 48000);
        check(left.back() == 0 && right.back() == 0, "disabled tone reaches silence");
        float* sparse[]{nullptr, right.data()};
        tone.render(sparse, 2, 64);
        std::cout << "PASS: ABI validation, lifecycle, concurrent calls, stereo tone, pitch, level and silence\n";
        return 0;
    } catch (const std::exception& e) { std::cerr << e.what() << '\n'; return 1; }
}
