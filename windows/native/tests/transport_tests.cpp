// SPDX-License-Identifier: AGPL-3.0-only
#include "rocker_audio.h"
#include "render_processor.h"
#include <array>
#include <iostream>
#include <fstream>
#include <limits>
#include <thread>
extern "C" void ROCKER_CALL rocker_test_set_frames(uint32_t frames);
extern "C" uint64_t ROCKER_CALL rocker_test_start_count();

namespace {
int checks = 0;
void check(bool value, const char* message) { ++checks; if (!value) throw std::runtime_error(message); }
// SMF 0, 480 ticks/quarter, 120 BPM, A4 for one quarter note.
const std::vector<uint8_t> midi{
    'M','T','h','d',0,0,0,6,0,0,0,1,1,224,
    'M','T','r','k',0,0,0,20,
    0,255,81,3,7,161,32,0,144,69,100,131,96,128,69,0,0,255,47,0
};
}
int main(int argc, char** argv) {
    try {
        check(argc == 2, "fixture path required");
        auto clip = rocker::media::synthesizeMidi(midi.data(), static_cast<uint32_t>(midi.size()));
        check(clip.sampleRate == 48000 && clip.channels.size() == 1, "owned mono MIDI PCM");
        check(std::abs(clip.frames() / clip.sampleRate - 0.58) < 0.001, "tempo and note-off release timing");
        int crossings = 0;
        bool finite = true;
        for (size_t i = 4800; i < 19200; ++i) {
            finite &= std::isfinite(clip.channels[0][i]) && std::abs(clip.channels[0][i]) < 0.8;
            if (clip.channels[0][i - 1] < 0 && clip.channels[0][i] >= 0) ++crossings;
        }
        check(finite, "bounded finite MIDI PCM");
        check(std::abs(crossings / 0.3 - 440) < 4, "A4 MIDI pitch");
        auto malformed = midi; malformed[9] = 2;
        try { rocker::media::synthesizeMidi(malformed.data(), static_cast<uint32_t>(malformed.size())); check(false, "SMF 2 accepted"); }
        catch (const rocker::media::Failure&) { check(true, "unsupported MIDI rejected"); }
        rocker::RenderProcessor processor(440, 0.02);
        processor.install(std::move(clip), 1); processor.selectClip(); processor.prepare(44100); processor.enable(true);
        std::vector<float> left(44100), right(44100), extra(44100);
        float* outputs[]{left.data(), right.data(), extra.data()};
        processor.render(outputs, 3, 44100);
        check(processor.ended() && std::abs(processor.position() - processor.duration()) < 1e-8, "EOF exact cursor and ended state");
        check(left == right, "mono to both outputs");
        check(left.back() == 0 && right.back() == 0, "silence after EOF");
        crossings = 0;
        for (size_t i = 4410; i < 17640; ++i) if (left[i - 1] < 0 && left[i] >= 0) ++crossings;
        check(std::abs(crossings / 0.3 - 440) < 4, "48 kHz source keeps pitch on 44.1 kHz device");
        processor.seek(0.1); processor.prepare(96000);
        processor.render(outputs, 2, 9600);
        check(std::abs(processor.position() - 0.2) < 1e-6, "seek and 96 kHz clock");
        processor.volume(0); processor.render(outputs, 2, 9600);
        check(left[9599] == 0, "volume reaches silence");
        rocker::media::Clip stereo{48000, {std::vector<float>(4800, 0), std::vector<float>(4800, 0.3f)}};
        processor.install(std::move(stereo), 1); processor.prepare(48000); processor.volume(0.5);
        processor.render(outputs, 3, 4800);
        check(left[1000] == 0 && right[1000] > 0.1f && extra[1000] == 0, "stereo isolation and inactive extra outputs");

        rocker::media::Clip loopClip{48000,{std::vector<float>(1000,0.2f)}};processor.install(std::move(loopClip),1);processor.prepare(44100);processor.enable(true);processor.volume(1);
        check(processor.loop(100.0/48000,900.0/48000,true),"sample-bounded A/B loop");
        std::array<float,511> loopLeft{},loopRight{};float* loopOutput[]{loopLeft.data(),loopRight.data()};
        uint64_t loopFrames=0;for(int i=0;i<4000;++i){int frames=(i%3==0?511:i%3==1?127:64);processor.process(nullptr,0,loopOutput,2,frames);loopFrames+=frames;}
        const double sourcePosition=100+std::fmod(loopFrames*(48000.0/44100),800);
        check(!processor.ended() && processor.loopCycles()>1000 && std::abs(processor.position()*48000-sourcePosition)<1e-5,"resampled loop preserves fractional overshoot without drift");
        check(loopLeft==loopRight,"loop source remains mono in both headphones");
        processor.seek(0);check(std::abs(processor.position()-100.0/48000)<1e-9,"seek outside active loop stays within its selected range");
        check(!processor.loop(0,100,true),"out-of-range loop rejected without replacing valid endpoints");processor.loop(0,0,false);
        check(!processor.loopEnabled(),"loop explicitly disabled");
        processor.loop(0,1000.0/48000,true);processor.seek(0);processor.process(nullptr,0,loopOutput,2,511);
        processor.process(nullptr,0,loopOutput,2,511);processor.process(nullptr,0,loopOutput,2,511);
        check(std::abs(processor.position()*48000-std::fmod(1533*(48000.0/44100),1000))<1e-6,"whole-file loop also retains resampling overshoot at EOF");
        rocker_audio_config config{sizeof(config), ROCKER_AUDIO_ABI_VERSION, 440, 0.02};
        rocker_audio_handle host{};
        check(rocker_audio_create(&config, &host) == ROCKER_OK, "create common host");
        struct Cleanup { rocker_audio_handle host; ~Cleanup() { rocker_audio_destroy(host); } } cleanup{host};
        check(rocker_audio_play(host) == ROCKER_NOT_READY, "cannot play before preparation");
        check(rocker_audio_prepare_midi(host, midi.data(), static_cast<uint32_t>(midi.size()), 1) == ROCKER_OK, "MIDI C ABI preparation");
        rocker_transport_snapshot transport{sizeof(transport), ROCKER_AUDIO_ABI_VERSION};
        check(rocker_audio_get_transport(host, &transport) == ROCKER_OK && transport.ready && !transport.playing, "preparation never starts audio");
        check(rocker_audio_play(host) == ROCKER_OK && rocker_audio_play(host) == ROCKER_OK, "idempotent media start");
        check(rocker_audio_get_transport(host, &transport) == ROCKER_OK && transport.playing && transport.position_seconds > 0, "callback advances common clock");
        check(rocker_audio_stop(host) == ROCKER_OK, "media stop");
        check(rocker_audio_seek(host, 0.2) == ROCKER_OK, "seek stopped transport");
        check(rocker_audio_get_transport(host, &transport) == ROCKER_OK && !transport.playing && std::abs(transport.position_seconds - 0.2) < 1e-6, "seek exact position");
        check(rocker_audio_seek(host, 10) == ROCKER_INVALID_ARGUMENT, "out-of-range seek rejected");
        check(rocker_audio_set_volume(host, std::numeric_limits<double>::quiet_NaN()) == ROCKER_INVALID_ARGUMENT, "NaN volume rejected");
        check(rocker_audio_set_volume(host, 0.4) == ROCKER_OK, "master media volume");
        check(rocker_audio_prepare_file(host, argv[1], 0.5) == ROCKER_OK, "file C ABI with pitch-preserving speed");
        check(rocker_audio_get_transport(host, &transport) == ROCKER_OK && std::abs(transport.duration_seconds - 2) < 0.001 && transport.speed == 0.5, "stretched file duration");
        check(rocker_audio_prepare_file(host, "missing-file.wav", 1) == ROCKER_MEDIA_ERROR, "decode error crosses ABI safely");
        check(rocker_audio_get_transport(host, &transport) == ROCKER_OK && transport.ready && transport.speed == 0.5, "failed preparation retains previous clip");
        check(rocker_audio_seek(host, transport.duration_seconds) == ROCKER_OK, "seek EOF");
        check(rocker_audio_play(host) == ROCKER_OK, "restart EOF begins again");
        check(rocker_audio_get_transport(host, &transport) == ROCKER_OK && transport.position_seconds < 0.1, "restart resets cursor");
        check(rocker_audio_start(host) == ROCKER_OK, "test tone switches same device host");
        check(rocker_audio_get_transport(host, &transport) == ROCKER_OK && !transport.playing && transport.ready, "tone is not reported as media playback");
        check(rocker_audio_stop(host) == ROCKER_OK, "stop tone");
        std::atomic<bool> okay{true};
        std::thread worker([&] { for (int i = 0; i < 4; ++i) if (rocker_audio_prepare_midi(host, midi.data(), static_cast<uint32_t>(midi.size()), 0.5) != ROCKER_OK) okay = false; });
        for (int i = 0; i < 100; ++i) if (rocker_audio_stop(host) != ROCKER_OK) okay = false;
        worker.join(); check(okay, "stop remains safe during worker preparation");
        // Finish during the first callback, then replay before a state poll has
        // cleaned up EOF. Idempotence must not swallow this explicit restart.
        auto shortMidi = midi; shortMidi[33] = 128; shortMidi[34] = 1;
        rocker_test_set_frames(4096);
        check(rocker_audio_prepare_midi(host, shortMidi.data(), static_cast<uint32_t>(shortMidi.size()), 1) == ROCKER_OK, "short MIDI preparation");
        const auto starts = rocker_test_start_count();
        check(rocker_audio_play(host) == ROCKER_OK && rocker_audio_play(host) == ROCKER_OK
            && rocker_test_start_count() == starts + 2, "immediate EOF replay before polling");
        check(rocker_audio_get_transport(host, &transport) == ROCKER_OK && transport.ended && !transport.playing, "EOF poll releases fake device");
        rocker_test_set_frames(1024);
        std::ofstream report("transport-checks.json");
        report << "{\"result\":\"passed\",\"checks\":" << checks << ",\"physicalAudioVerified\":false}";
        std::cout << "PASS: " << checks << " transport/MIDI assertions (fake device and offline rendering)\n";
        return 0;
    } catch (const std::exception& error) { std::cerr << error.what() << '\n'; return 1; }
}
