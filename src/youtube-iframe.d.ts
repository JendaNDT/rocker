// Minimal ambient types for the YouTube IFrame Player API — only the surface
// Rocker actually uses. See https://developers.google.com/youtube/iframe_api_reference

interface YouTubePlayerEvents {
  onReady?: () => void;
  onStateChange?: (event: { data: number }) => void;
}

interface YouTubePlayerOptions {
  width?: string | number;
  height?: string | number;
  videoId?: string;
  playerVars?: Record<string, string | number>;
  events?: YouTubePlayerEvents;
}

declare class YouTubeIframePlayer {
  constructor(element: string | HTMLElement, options: YouTubePlayerOptions);
  loadVideoById(videoId: string): void;
  playVideo(): void;
  pauseVideo(): void;
  seekTo(seconds: number, allowSeekAhead: boolean): void;
  getCurrentTime(): number;
  getDuration(): number;
  getPlayerState(): number;
  setPlaybackRate(rate: number): void;
  destroy(): void;
}

declare namespace YT {
  type Player = YouTubeIframePlayer;
  const Player: typeof YouTubeIframePlayer;

  enum PlayerState {
    UNSTARTED = -1,
    ENDED = 0,
    PLAYING = 1,
    PAUSED = 2,
    BUFFERING = 3,
    CUED = 5,
  }
}

interface Window {
  YT: typeof YT;
  onYouTubeIframeAPIReady?: () => void;
}
