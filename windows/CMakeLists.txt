cmake_minimum_required(VERSION 3.31)
file(STRINGS "${CMAKE_CURRENT_LIST_DIR}/Directory.Build.props" rocker_version_line REGEX "<VersionPrefix>[0-9.]+</VersionPrefix>")
string(REGEX MATCH "<VersionPrefix>([0-9.]+)</VersionPrefix>" rocker_version_match "${rocker_version_line}")
if(NOT rocker_version_match)
    message(FATAL_ERROR "A product version is required in Directory.Build.props")
endif()
project(RockerAudioProbe VERSION ${CMAKE_MATCH_1} LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
if(MSVC)
    add_compile_options(/Z7)
    set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
endif()
include(CTest)
option(ROCKER_WITH_JUCE "Build the real device DLL (requires JUCE dependencies)" ON)
option(ROCKER_WITH_MEDIA "Build offline decoding and pitch-preserving stretching" ON)

function(rocker_audio_target target driver)
    add_library(${target} SHARED native/src/audio_host.cpp ${driver})
    target_include_directories(${target} PUBLIC native/include PRIVATE native/src)
    target_compile_definitions(${target} PRIVATE ROCKER_AUDIO_EXPORTS)
    if(ROCKER_WITH_MEDIA)
        target_compile_definitions(${target} PRIVATE ROCKER_WITH_MEDIA=1)
        target_link_libraries(${target} PRIVATE rocker_media)
    endif()
    set_target_properties(${target} PROPERTIES CXX_VISIBILITY_PRESET hidden VISIBILITY_INLINES_HIDDEN YES)
    if(MSVC)
        target_compile_options(${target} PRIVATE /W4 /permissive- /utf-8)
        target_link_options(${target} PRIVATE /DEBUG:FULL /OPT:REF /OPT:ICF)
    else()
        target_compile_options(${target} PRIVATE -Wall -Wextra -Wpedantic)
    endif()
endfunction()

if(ROCKER_WITH_JUCE OR ROCKER_WITH_MEDIA)
    include(FetchContent)
    # WinUI owns the UI and packaging; no JUCE app/plugin helper executable is needed.
    set(JUCE_MODULES_ONLY ON CACHE BOOL "Configure JUCE modules for the standalone DLL" FORCE)
    FetchContent_Declare(JUCE
        GIT_REPOSITORY https://github.com/juce-framework/JUCE.git
        GIT_TAG be29c81492b6151c8ea8d14c840e1311963b3a83)
    FetchContent_MakeAvailable(JUCE)
endif()

if(ROCKER_WITH_MEDIA)
    # Declare Linear first so Stretch cannot fetch a moving version tag itself.
    FetchContent_Declare(signalsmith-linear
        GIT_REPOSITORY https://github.com/Signalsmith-Audio/linear.git
        GIT_TAG de55e6a50ffcf6f8f43f649692d94691c7025151)
    FetchContent_Declare(signalsmith-stretch
        GIT_REPOSITORY https://github.com/Signalsmith-Audio/signalsmith-stretch.git
        GIT_TAG a670068d9aeb64913331d5cc29337b19a457a7df)
    FetchContent_MakeAvailable(signalsmith-linear signalsmith-stretch)
    FetchContent_Declare(libwebm
        GIT_REPOSITORY https://github.com/webmproject/libwebm.git
        GIT_TAG 6745fd29e0245fc584b0bb9f65018ea2366fe7fb
        SOURCE_SUBDIR rocker-parser-only)
    FetchContent_MakeAvailable(libwebm)
    # Build only the reviewed parser; upstream project flags/sample tools are
    # unnecessary and would alter the parent project's compiler configuration.
    add_library(rocker_webm_parser STATIC ${libwebm_SOURCE_DIR}/mkvparser/mkvparser.cc)
    set_target_properties(rocker_webm_parser PROPERTIES POSITION_INDEPENDENT_CODE ON)
    target_include_directories(rocker_webm_parser PUBLIC ${libwebm_SOURCE_DIR})
    add_library(rocker_media STATIC native/src/media_clip.cpp native/src/webm_decoder.cpp native/src/mf_decoder.cpp native/src/midi_synth.cpp)
    set_target_properties(rocker_media PROPERTIES POSITION_INDEPENDENT_CODE ON)
    target_include_directories(rocker_media PUBLIC native/include)
    target_compile_definitions(rocker_media PUBLIC
        JUCE_GLOBAL_MODULE_SETTINGS_INCLUDED=1 JUCE_USE_CURL=0
        JUCE_USE_WINDOWS_MEDIA_FORMAT=0 JUCE_USE_FLAC=0 JUCE_USE_OGGVORBIS=0)
    target_link_libraries(rocker_media PRIVATE juce::juce_audio_formats signalsmith-stretch rocker_webm_parser)
    if(WIN32)
        # GUIDs and COM only: no eager imports from optional Media Foundation DLLs.
        target_link_libraries(rocker_media PRIVATE mfuuid ole32)
    endif()
endif()

if(ROCKER_WITH_JUCE)
    rocker_audio_target(rocker_audio native/src/juce_driver.cpp)
    target_compile_definitions(rocker_audio PRIVATE
        JUCE_GLOBAL_MODULE_SETTINGS_INCLUDED=1 JUCE_STANDALONE_APPLICATION=1
        JUCE_WEB_BROWSER=0 JUCE_USE_CURL=0 JUCE_ASIO_USE_EXTERNAL_SDK=0
        JUCE_ASIO=$<BOOL:${WIN32}> JUCE_WASAPI=$<BOOL:${WIN32}>)
    target_link_libraries(rocker_audio PRIVATE juce::juce_audio_devices)
endif()

if(BUILD_TESTING)
    if(ROCKER_WITH_MEDIA)
        add_executable(rocker_media_tests native/tests/media_tests.cpp)
        target_link_libraries(rocker_media_tests PRIVATE rocker_media)
        add_test(NAME media_processing COMMAND rocker_media_tests
            "${CMAKE_CURRENT_SOURCE_DIR}/../docs/windows/baseline/fixtures/audio"
            "${CMAKE_CURRENT_SOURCE_DIR}/fixtures/media")
    endif()
    add_executable(rocker_analysis_worker_tests native/tests/analysis_worker_tests.cpp)
    target_include_directories(rocker_analysis_worker_tests PRIVATE native/src)
    add_test(NAME analysis_worker COMMAND rocker_analysis_worker_tests)
    set_tests_properties(analysis_worker PROPERTIES TIMEOUT 60)
    add_executable(rocker_analysis_tests native/tests/analysis_tests.cpp)
    target_include_directories(rocker_analysis_tests PRIVATE native/src)
    add_test(NAME pitch_analysis COMMAND rocker_analysis_tests)
    set_tests_properties(pitch_analysis PROPERTIES TIMEOUT 120)
    add_executable(rocker_metronome_tests native/tests/metronome_tests.cpp)
    target_include_directories(rocker_metronome_tests PRIVATE native/src)
    add_test(NAME metronome_dsp COMMAND rocker_metronome_tests)
    set_tests_properties(metronome_dsp PROPERTIES TIMEOUT 60)
    add_executable(rocker_amp_tests native/tests/amp_tests.cpp)
    target_include_directories(rocker_amp_tests PRIVATE native/src)
    add_test(NAME amplifier_dsp COMMAND rocker_amp_tests)
    set_tests_properties(amplifier_dsp PROPERTIES TIMEOUT 120)
    add_executable(rocker_clock_tests native/tests/clock_tests.cpp)
    target_include_directories(rocker_clock_tests PRIVATE native/src)
    add_test(NAME sample_clock COMMAND rocker_clock_tests)
    add_executable(rocker_routing_tests native/tests/routing_tests.cpp)
    target_include_directories(rocker_routing_tests PRIVATE native/include native/src)
    add_test(NAME input_routing COMMAND rocker_routing_tests)
    add_executable(rocker_realtime_tests native/tests/realtime_tests.cpp)
    target_include_directories(rocker_realtime_tests PRIVATE native/include native/src)
    add_test(NAME realtime_contract COMMAND rocker_realtime_tests)
    # This different library name prevents a fake backend from shipping as the device DLL.
    rocker_audio_target(rocker_audio_test native/tests/fake_driver.cpp)
    if(ROCKER_WITH_MEDIA)
        add_executable(rocker_transport_tests native/tests/transport_tests.cpp)
        target_include_directories(rocker_transport_tests PRIVATE native/src)
        target_link_libraries(rocker_transport_tests PRIVATE rocker_audio_test rocker_media)
        add_test(NAME common_transport COMMAND rocker_transport_tests
            "${CMAKE_CURRENT_SOURCE_DIR}/../docs/windows/baseline/fixtures/audio/a4-440.wav")
    endif()
    add_executable(rocker_audio_contract_tests native/tests/audio_contract_tests.cpp)
    target_include_directories(rocker_audio_contract_tests PRIVATE native/src)
    target_link_libraries(rocker_audio_contract_tests PRIVATE rocker_audio_test)
    add_test(NAME audio_contract COMMAND rocker_audio_contract_tests)
    set_tests_properties(audio_contract realtime_contract input_routing sample_clock PROPERTIES TIMEOUT 30)
    add_library(rocker_audio_test_incompatible SHARED native/tests/incompatible_abi.c)
    target_include_directories(rocker_audio_test_incompatible PRIVATE native/include)
    target_compile_definitions(rocker_audio_test_incompatible PRIVATE ROCKER_AUDIO_EXPORTS)
endif()
