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

#define ROCKER_AUDIO_ABI_VERSION 1u
typedef struct rocker_audio_host* rocker_audio_handle;
typedef uint32_t rocker_audio_result;
enum {
    ROCKER_OK = 0, ROCKER_INVALID_ARGUMENT = 1, ROCKER_ABI_MISMATCH = 2,
    ROCKER_DEVICE_ERROR = 3, ROCKER_BUFFER_TOO_SMALL = 4, ROCKER_INTERNAL_ERROR = 5
};
enum { ROCKER_STOPPED = 0, ROCKER_RUNNING = 1, ROCKER_FAULTED = 2 };

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
#pragma pack(pop)

/* Control-thread API only; never call from an audio callback. All pointers are
   borrowed for the call. Only create transfers ownership of an opaque handle.
   Calls on a live handle are serialized internally. The caller must exclude
   destroy from all concurrent calls and must not reuse a destroyed handle.
   Structures require the exact ABI version and size. No exception crosses ABI. */
ROCKER_API uint32_t ROCKER_CALL rocker_audio_get_abi_version(void);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_create(const rocker_audio_config*, rocker_audio_handle*);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_start(rocker_audio_handle);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_stop(rocker_audio_handle);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_get_snapshot(rocker_audio_handle, rocker_audio_snapshot*);
/* required includes the terminating zero; capacity=0 queries size. UTF-8 is
   copied, never borrowed. A short buffer is terminated and reports TOO_SMALL. */
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_copy_last_error(rocker_audio_handle, char*, uint32_t capacity, uint32_t* required);
ROCKER_API void ROCKER_CALL rocker_audio_destroy(rocker_audio_handle);

#ifdef __cplusplus
}
#endif
#endif
