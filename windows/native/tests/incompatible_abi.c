/* SPDX-License-Identifier: AGPL-3.0-only */
#include "rocker_audio.h"
/* No other exports: reject the older ABI before trying to create a handle. */
ROCKER_API uint32_t ROCKER_CALL rocker_audio_get_abi_version(void) { return ROCKER_AUDIO_ABI_VERSION - 1u; }
