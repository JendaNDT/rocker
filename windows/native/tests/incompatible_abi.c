/* SPDX-License-Identifier: AGPL-3.0-only */
#include <stdint.h>
#if defined(_WIN32)
 #define TEST_EXPORT __declspec(dllexport)
 #define TEST_CALL __cdecl
#else
 #define TEST_EXPORT __attribute__((visibility("default")))
 #define TEST_CALL
#endif
/* No other exports: a managed client must reject this before creating a handle. */
TEST_EXPORT uint32_t TEST_CALL rocker_audio_get_abi_version(void) { return 2; }
