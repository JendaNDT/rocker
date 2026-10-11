/* SPDX-License-Identifier: AGPL-3.0-only */
#ifndef ROCKER_AUDIO_H
#define ROCKER_AUDIO_H

#include <stdint.h>
#if defined(_WIN32)
 #define ROCKER_CALL __cdecl
 #if defined(ROCKER_AUDIO_EXPORTS)
  #define ROCKER_API __declspec(dllexport)
 #else
  #define ROCKER_API __declspec(dllimport)
 #endif
#else
 #define ROCKER_CALL
 #define ROCKER_API __attribute__((visibility("default")))
#endif
#ifdef __cplusplus
extern "C" {
#endif

/* Version 5 adds live input routing, monitor controls and bus meters.
   Reject earlier DLLs before creating a host. */
#define ROCKER_AUDIO_ABI_VERSION 5u
typedef struct rocker_audio_host* rocker_audio_handle;
typedef uint32_t rocker_audio_result;
enum {
    ROCKER_OK = 0, ROCKER_INVALID_ARGUMENT = 1, ROCKER_ABI_MISMATCH = 2,
    ROCKER_DEVICE_ERROR = 3, ROCKER_BUFFER_TOO_SMALL = 4, ROCKER_INTERNAL_ERROR = 5,
    ROCKER_MEDIA_ERROR = 6, ROCKER_NOT_READY = 7
};
enum { ROCKER_STOPPED = 0, ROCKER_RUNNING = 1, ROCKER_FAULTED = 2,
    ROCKER_STARTING = 3, ROCKER_RECONFIGURING = 4, ROCKER_STOPPING = 5, ROCKER_CLOSING = 6 };

#pragma pack(push, 8)
typedef struct rocker_audio_config {
    uint32_t struct_size;
    uint32_t abi_version;
    double frequency_hz;
    double amplitude;
} rocker_audio_config;
typedef struct rocker_audio_snapshot {
    uint32_t struct_size;
    uint32_t abi_version;
    uint32_t state;
    uint32_t reserved;
    double sample_rate;
    uint32_t buffer_frames;
    uint32_t output_channels;
    uint64_t rendered_frames;
} rocker_audio_snapshot;
typedef struct rocker_transport_snapshot {
    uint32_t struct_size, abi_version;
    uint32_t ready, playing, ended, reserved;
    double duration_seconds, position_seconds, speed, volume;
} rocker_transport_snapshot;
typedef struct rocker_device_config {
    uint32_t struct_size, abi_version;
    double sample_rate;
    uint32_t buffer_frames;
    int32_t input_channel; /* -1: output only, otherwise physical mono channel */
    uint32_t output_first, reserved; /* physical stereo pair: first, first+1 */
} rocker_device_config;
typedef struct rocker_meter_snapshot {
    uint32_t struct_size,abi_version,flags,epoch;
    uint64_t frame;
    double sample_rate,input_peak,input_rms,processed_peak,processed_rms,mix_peak,mix_rms;
    uint64_t invalid_samples,input_clips,output_clips,dropped_analysis_blocks;
} rocker_meter_snapshot;
#pragma pack(pop)

/* Never call from an audio callback. Device operations execute on the native
   control worker; synchronous callers should themselves use a managed worker.
   Cached telemetry, volume and stop requests do not wait for driver I/O. All
   pointers are borrowed for the call. Only create transfers opaque ownership.
   The caller must exclude
   destroy from all concurrent calls and must not reuse a destroyed handle.
   Structures require the exact ABI version and size. No exception crosses ABI. */
ROCKER_API uint32_t ROCKER_CALL rocker_audio_get_abi_version(void);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_create(const rocker_audio_config*, rocker_audio_handle*);
/* Reserve on the calling/UI thread, then start on a worker using the same ticket.
   A later stop invalidates it even when that worker has not been scheduled yet. */
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_reserve_start(rocker_audio_handle, uint64_t* ticket);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_start_reserved(rocker_audio_handle, uint64_t ticket, uint32_t mode);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_start(rocker_audio_handle);
/* Live starts input processing without automatically enabling monitoring. */
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_start_live(rocker_audio_handle);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_set_monitoring(rocker_audio_handle,uint32_t enabled,double gain);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_get_meters(rocker_audio_handle,rocker_meter_snapshot*);
/* Immediately silence the graph and invalidate pending starts. Does not wait for vendor I/O. */
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_request_stop(rocker_audio_handle);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_reserve_stop(rocker_audio_handle,uint64_t* ticket);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_stop_reserved(rocker_audio_handle,uint64_t ticket);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_stop(rocker_audio_handle);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_get_snapshot(rocker_audio_handle, rocker_audio_snapshot*);
/* Preparation is synchronous worker work. Decoding does not hold the host's
   control mutex, so Stop remains available. Successful preparation stops the
   driver before replacing PCM and never starts sound. Failed preparation keeps
   the previous clip. UTF-8 paths and MIDI data are borrowed only for the call.
   Callers must retain the handle until the worker call completes. */
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_prepare_file(rocker_audio_handle, const char* utf8_path, double speed);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_prepare_midi(rocker_audio_handle, const uint8_t*, uint32_t count, double speed);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_play(rocker_audio_handle);
/* Seek stops playback and releases the device. Position is in stretched time. */
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_seek(rocker_audio_handle, double seconds);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_set_volume(rocker_audio_handle, double volume);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_get_transport(rocker_audio_handle, rocker_transport_snapshot*);
/* Device queries/configuration are worker operations, never UI/callback calls.
   UTF-8 JSON queries are copied in one call. Empty input means output only.
   Explicit configuration validates by opening silently, then closes. Failure
   preserves the previous selection; neither path automatically resumes sound. */
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_copy_devices(rocker_audio_handle, char*, uint32_t, uint32_t*);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_copy_capabilities(rocker_audio_handle, const char* backend, const char* input, const char* output, char*, uint32_t, uint32_t*);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_configure_device(rocker_audio_handle, const rocker_device_config*, const char* backend, const char* input, const char* output);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_copy_device_details(rocker_audio_handle, char*, uint32_t, uint32_t*);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_show_control_panel(rocker_audio_handle);
/* required includes the terminating zero; capacity=0 queries size. UTF-8 is
   copied, never borrowed. A short buffer is terminated and reports TOO_SMALL. */
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_copy_last_error(rocker_audio_handle, char*, uint32_t capacity, uint32_t* required);
ROCKER_API void ROCKER_CALL rocker_audio_destroy(rocker_audio_handle);

#ifdef __cplusplus
}
#endif
#endif
