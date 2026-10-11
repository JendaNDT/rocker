// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include "media_clip.h"
namespace rocker::media::detail {
Clip decodeWebm(const std::filesystem::path&, Limits);
Clip decodeMp4(const std::filesystem::path&, Limits);
}
