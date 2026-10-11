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

/* Version 14 adds the shared multitrack project mixer.
   Reject earlier DLLs before creating a host. */
#define ROCKER_AUDIO_ABI_VERSION 15u
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
typedef struct rocker_clip_info {uint32_t struct_size,abi_version;double sample_rate;uint64_t frames;uint32_t channels,flags;} rocker_clip_info;
typedef struct rocker_project_track {
    uint32_t struct_size,abi_version;const char* path;double gain,pan;
    uint32_t flags,reserved;uint64_t offset_frames,trim_frames;
} rocker_project_track;
typedef struct rocker_loop_snapshot {
    uint32_t struct_size,abi_version,state,failure,layers,bus,sample_rate,flags;
    uint64_t frames,position,cycles,start_frame,acknowledged,capacity;
    uint32_t delay_frames,reserved;
    float gains[8];
} rocker_loop_snapshot;
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
typedef struct rocker_clock_snapshot {
    uint32_t struct_size,abi_version,flags,epoch;
    uint64_t frame,beat;
    double sample_rate,beat_phase,bpm;
    uint32_t dsp_latency_frames,reserved;
    uint64_t loop_cycles;
} rocker_clock_snapshot;
typedef struct rocker_amp_config {
    uint32_t struct_size,abi_version,enabled,distortion,cabinet,reserved;
    double compression,drive,bass,mid,treble,delay,reverb,chorus,level;
} rocker_amp_config;
typedef struct rocker_metronome_config {
    uint32_t struct_size,abi_version,enabled,bpm,beats,subdivision,sound,sub_sound,count_in;
    uint32_t ramp_enabled,ramp_every,ramp_step,ramp_target,gap_enabled,audible_bars,silent_bars,poly_enabled,poly_ticks,poly_sound,reserved;
    double volume,accent_volume,swing,poly_volume;
    uint32_t accents[16],sounds[16];
} rocker_metronome_config;
typedef struct rocker_metronome_snapshot {
    uint32_t struct_size,abi_version,flags;int32_t beat;
    uint64_t frame;int64_t bar;double bpm,phase;
    uint32_t subdivision_tick,poly_tick;
    uint64_t voice_overloads,primary_events,secondary_events;
} rocker_metronome_snapshot;
typedef struct rocker_analysis_snapshot {
    uint32_t struct_size,abi_version,flags,epoch;
    uint64_t frame,sequence,hit_count,onset_frame,calibration_token;
    double sample_rate,rms,peak,frequency,clarity,centroid,hit_centroid,window_seconds,age_seconds,calibration_progress,recommended_gate;
} rocker_analysis_snapshot;
typedef struct rocker_diagnostic_snapshot {
    uint32_t struct_size,abi_version,epoch,flags;
    uint64_t callbacks,frames,total_nanoseconds,last_nanoseconds,max_nanoseconds,over_budget,analysis_dropped,analysis_high_water,device_faults,device_starts,stop_requests,media_errors,invalid_callbacks;
    int64_t driver_xruns;double sample_rate;
    uint32_t min_frames,max_frames,last_failure_reason,source_mode;
    uint64_t configuration_failures;
} rocker_diagnostic_snapshot;
typedef struct rocker_recording_snapshot {
    uint32_t struct_size,abi_version,state,failure,bus,sample_rate,channels,epoch;
    uint64_t token,written_frames,captured_frames,first_frame,dropped_blocks,high_water;
    uint32_t flags,dsp_latency_frames;
} rocker_recording_snapshot;
#pragma pack(pop)

/* Never call from an audio callback. Device operations execute on the native
   control worker; synchronous callers should themselves use a managed worker.
   Cached telemetry, volume and stop requests do not wait for driver I/O. All
   pointers are borrowed for the call. Only create transfers opaque ownership.
   The caller must exclude
   destroy from all concurrent calls and must not reuse a destroyed handle.
   Structures require the exact ABI version and size. No exception crosses ABI. */
ROCKER_API uint32_t ROCKER_CALL rocker_audio_get_abi_version(void);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_reserve_recording(rocker_audio_handle,uint64_t*,uint64_t*);
typedef struct rocker_recording_alignment {uint32_t struct_size,abi_version;uint64_t token,media_ticket;double first_project_frame,project_rate,capture_rate;uint32_t flags,failure,dsp_frames,reserved;} rocker_recording_alignment;
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_start_project_recording_reserved(rocker_audio_handle,uint64_t graph,uint64_t take,const char* path,uint32_t bus,uint32_t bars,uint64_t project_ticket);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_get_recording_alignment(rocker_audio_handle,rocker_recording_alignment*);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_start_recording_reserved(rocker_audio_handle,uint64_t,uint64_t,const char*,uint32_t,uint32_t);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_request_recording_stop(rocker_audio_handle);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_finish_recording(rocker_audio_handle);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_get_recording(rocker_audio_handle,rocker_recording_snapshot*);
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
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_get_clock(rocker_audio_handle,rocker_clock_snapshot*);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_set_tempo(rocker_audio_handle,double bpm);
/* A/B endpoints use prepared (stretched) seconds; changing them stops audio. */
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_set_loop(rocker_audio_handle,double start,double end,uint32_t enabled);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_set_amp(rocker_audio_handle,const rocker_amp_config*);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_get_amp(rocker_audio_handle,rocker_amp_config*);
/* Metronome joins an existing graph without restarting its clock or clip.
   Configuration never opens a device. Stop all disarms the metronome. */
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_set_metronome(rocker_audio_handle,const rocker_metronome_config*);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_get_metronome(rocker_audio_handle,rocker_metronome_config*);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_get_metronome_snapshot(rocker_audio_handle,rocker_metronome_snapshot*);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_reserve_metronome_start(rocker_audio_handle,uint64_t*);
/* Cached, age-labelled readings from the separate analysis worker. Bit flags:
   1 pitch, 2 ready window, 4 active input, 8 calibration, 16 complete,
   32 interrupted calibration, 64 stale. No input/stale => frequency zero. */
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_get_analysis(rocker_audio_handle,rocker_analysis_snapshot*);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_set_noise_gate(rocker_audio_handle,double level);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_calibrate_noise(rocker_audio_handle,uint64_t* token);
/* Reference/chime commands use a reserved additive-start ticket. A stale
   ticket cannot arm audio after Stop. enabled=0, confirmation=0 only releases
   the reference tone and does not stop other sources. */
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_reserve_reference(rocker_audio_handle,uint32_t kind,uint64_t* graph_ticket,uint64_t* reference_ticket);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_reference(rocker_audio_handle,uint64_t ticket,uint64_t reference_ticket,uint32_t enabled,double frequency,double volume,uint32_t confirmation);
/* Timings cover Rocker graph/copy work, not vendor callback overhead.
   Over-budget counts are not confirmed hardware underruns. driver_xruns=-1
   means unavailable; refresh queries a vendor only on the control worker. */
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_get_diagnostics(rocker_audio_handle,rocker_diagnostic_snapshot*);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_refresh_diagnostics(rocker_audio_handle);
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
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_pause(rocker_audio_handle);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_play_count_in_reserved(rocker_audio_handle,uint64_t,uint32_t,uint32_t,double);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_cancel_media(rocker_audio_handle);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_clear_media_reserved(rocker_audio_handle,uint64_t ticket);
typedef struct rocker_preview_note {double start_seconds,duration_seconds,frequency_hz,volume;} rocker_preview_note;
typedef struct rocker_rhythm_snapshot {uint32_t struct_size,abi_version,state,epoch,bpm,flags,last_completed_bpm,reserved;uint64_t token,start_frame,rounds;double elapsed_beats,round_beats;} rocker_rhythm_snapshot;
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_reserve_rhythm(rocker_audio_handle,uint64_t* graph,uint64_t* trainer,uint64_t* phrase);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_start_rhythm_reserved(rocker_audio_handle,uint64_t graph,uint64_t trainer,uint64_t phrase,const rocker_preview_note*,uint32_t count,double pattern_beats,double round_beats,uint32_t bpm,uint32_t speed_up,uint32_t count_in_beats);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_reserve_rhythm_stop(rocker_audio_handle,uint64_t* ticket);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_finish_rhythm_stop(rocker_audio_handle,uint64_t ticket);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_get_rhythm(rocker_audio_handle,rocker_rhythm_snapshot*);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_reserve_preview(rocker_audio_handle,uint64_t* graph,uint64_t* phrase);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_preview_reserved(rocker_audio_handle,uint64_t graph,uint64_t phrase,const rocker_preview_note*,uint32_t count,uint32_t repeat,uint32_t count_in_beats);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_stop_preview(rocker_audio_handle);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_reserve_media(rocker_audio_handle,uint64_t*);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_prepare_file_reserved(rocker_audio_handle,uint64_t,const char*,double,uint64_t);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_prepare_midi_reserved(rocker_audio_handle,uint64_t,const uint8_t*,uint32_t,double);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_cancel_ramp(rocker_audio_handle);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_prepare_file_for_loop(rocker_audio_handle,const char*,double,uint64_t);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_copy_waveform(rocker_audio_handle,char*,uint32_t,uint32_t*);
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
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_get_clip_info(rocker_audio_handle,rocker_clip_info*);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_get_project_track_info(rocker_audio_handle,uint64_t,uint32_t,rocker_clip_info*);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_prepare_project_reserved(rocker_audio_handle,uint64_t,uint32_t,const rocker_project_track*,uint32_t);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_project_track(rocker_audio_handle,uint64_t,uint32_t,double,double,uint32_t);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_loop_command(rocker_audio_handle,uint64_t graph_ticket,uint32_t kind,uint32_t bus,uint32_t count_in_bars,uint32_t beats,uint32_t aligned,uint32_t delay_frames,uint32_t layer,double gain);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_get_loop(rocker_audio_handle,rocker_loop_snapshot*);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_set_loop_metadata(rocker_audio_handle,uint32_t bus,uint32_t flags,uint32_t delay);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_load_loop_layer(rocker_audio_handle,const char* path,uint32_t layer,double gain);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_export_loop(rocker_audio_handle,const char* path,int32_t layer);
/* Offline file conversion. Callbacks run on workers, never on the audio thread.
   The destination must be a new file; cancellation leaves a partial temporary
   file for the caller to remove. end_seconds=0 means the source end. */
typedef uint32_t (ROCKER_CALL *rocker_export_cancel)(void*);
typedef void (ROCKER_CALL *rocker_export_progress)(void*,double);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_export_project(const rocker_project_track*,uint32_t,uint32_t,const char*,double,double,rocker_export_cancel,rocker_export_progress,void*);
ROCKER_API rocker_audio_result ROCKER_CALL rocker_audio_export_wav(const char* source,const char* destination,double start_seconds,double end_seconds,rocker_export_cancel cancel,rocker_export_progress progress,void* context);

#ifdef __cplusplus
}
#endif
#endif
