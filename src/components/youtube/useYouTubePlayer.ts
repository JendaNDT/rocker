"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { loadYouTubeApi } from "@/lib/youtubeApi";

export interface YouTubePlayerControls {
  ready: boolean;
  load: (videoId: string) => void;
  setRate: (rate: number) => void;
  seekTo: (seconds: number) => void;
  getCurrentTime: () => number;
  getDuration: () => number;
}

/**
 * Creates and manages a YouTube IFrame player attached to the element with
 * the given id. The element is replaced by the player iframe.
 */
export function useYouTubePlayer(
  containerId: string,
  onError?: () => void,
): YouTubePlayerControls {
  const playerRef = useRef<YT.Player | null>(null);
  const [ready, setReady] = useState(false);
  // Keep the latest onError without re-creating the player on every render.
  const onErrorRef = useRef(onError);
  onErrorRef.current = onError;

  useEffect(() => {
    let cancelled = false;

    // Keep the build promise so cleanup can wait for it. If unmount happens
    // between loadYouTubeApi() and its .then(), the player created (if any)
    // is destroyed by the cleanup below — without this, a player born after
    // cleanup would orphan its iframe (a "ghost iframe" in React Strict mode
    // where every effect runs twice).
    const playerBuild = loadYouTubeApi().then<YT.Player | null>((yt) => {
      if (cancelled) return null;
      const player = new yt.Player(containerId, {
        width: "100%",
        height: "100%",
        playerVars: { rel: 0, modestbranding: 1 },
        events: {
          onReady: () => {
            if (!cancelled) setReady(true);
          },
          onError: () => {
            if (!cancelled) onErrorRef.current?.();
          },
        },
      });
      playerRef.current = player;
      return player;
    });

    return () => {
      cancelled = true;
      playerRef.current = null;
      void playerBuild.then((player) => {
        player?.destroy();
      });
    };
  }, [containerId]);

  const load = useCallback((videoId: string) => {
    playerRef.current?.loadVideoById(videoId);
  }, []);

  const setRate = useCallback((rate: number) => {
    playerRef.current?.setPlaybackRate(rate);
  }, []);

  const seekTo = useCallback((seconds: number) => {
    playerRef.current?.seekTo(seconds, true);
  }, []);

  const getCurrentTime = useCallback(() => {
    return playerRef.current?.getCurrentTime() ?? 0;
  }, []);

  const getDuration = useCallback(() => {
    return playerRef.current?.getDuration() ?? 0;
  }, []);

  return { ready, load, setRate, seekTo, getCurrentTime, getDuration };
}
