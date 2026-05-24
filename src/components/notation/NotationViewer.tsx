"use client";

import { useEffect, useRef, useState } from "react";
import type { AlphaTabApi } from "@coderline/alphatab";

/** Loading state of the notation viewer. */
type Status = "empty" | "loading" | "loaded" | "error";

// Guitar Pro and (uncompressed) MusicXML file extensions.
const ACCEPT = ".gp,.gpx,.gp3,.gp4,.gp5,.gp7,.xml,.musicxml";

const ERR_LOAD =
  "Soubor se nepodařilo načíst. Zkontroluj, že je to platný Guitar Pro nebo MusicXML soubor.";
const ERR_READ = "Soubor se nepodařilo přečíst z disku.";

/** Playback speeds offered for slow practice. */
const SPEEDS = [0.5, 0.75, 1];

const CONTROL_BUTTON =
  "rounded-lg border border-neutral-800 bg-neutral-900 px-4 py-2 text-sm transition-colors hover:border-neutral-700 disabled:cursor-not-allowed disabled:opacity-50";
const SEGMENT_ACTIVE =
  "rounded-md bg-amber-500 px-3 py-1.5 text-sm font-semibold text-neutral-950";
const SEGMENT_IDLE =
  "rounded-md px-3 py-1.5 text-sm text-neutral-400 transition-colors hover:text-neutral-100";

/**
 * Notation viewer for Bod 15: the user picks a Guitar Pro or MusicXML file
 * and alphaTab renders it as tablature and notation, with synth playback,
 * slow-down and an A/B practice loop.
 *
 * alphaTab is browser-only, so it is imported lazily inside an effect (it must
 * not run during the static prerender). useWorkers is off (main-thread layout)
 * and the player uses the ScriptProcessor output — both avoid bundling
 * alphaTab's Web Worker / Audio Worklet, and are fine for one sheet at a time.
 * The Bravura font and the SONiVOX soundfont are served from /alphatab/.
 */
export function NotationViewer() {
  const containerRef = useRef<HTMLDivElement>(null);
  const apiRef = useRef<AlphaTabApi | null>(null);
  const [status, setStatus] = useState<Status>("empty");
  const [errorMessage, setErrorMessage] = useState("");
  const [playerReady, setPlayerReady] = useState(false);
  const [playing, setPlaying] = useState(false);
  const [speed, setSpeed] = useState(1);
  const [looping, setLooping] = useState(false);

  useEffect(() => {
    if (!containerRef.current) return;

    let destroy: (() => void) | null = null;
    let cancelled = false;

    void import("@coderline/alphatab").then((alphaTab) => {
      if (cancelled || !containerRef.current) return;
      const api = new alphaTab.AlphaTabApi(containerRef.current, {
        core: {
          fontDirectory: "/alphatab/font/",
          useWorkers: false,
        },
        player: {
          playerMode: alphaTab.PlayerMode.EnabledSynthesizer,
          soundFont: "/alphatab/soundfont/sonivox.sf3",
          outputMode: alphaTab.PlayerOutputMode.WebAudioScriptProcessor,
        },
      });
      apiRef.current = api;
      api.scoreLoaded.on(() => setStatus("loaded"));
      api.error.on(() => {
        setStatus("error");
        setErrorMessage(ERR_LOAD);
      });
      api.playerReady.on(() => setPlayerReady(true));
      api.playerStateChanged.on((args) => setPlaying(Boolean(args.state)));
      destroy = () => api.destroy();
    });

    return () => {
      cancelled = true;
      destroy?.();
      apiRef.current = null;
    };
  }, []);

  async function handleFile(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    // Reset the input so picking the same file again still fires onChange.
    event.target.value = "";
    if (!file) return;

    const api = apiRef.current;
    if (!api) return;

    setStatus("loading");
    setErrorMessage("");
    setPlaying(false);
    try {
      const buffer = await file.arrayBuffer();
      api.load(new Uint8Array(buffer));
    } catch {
      setStatus("error");
      setErrorMessage(ERR_READ);
    }
  }

  function changeSpeed(value: number) {
    setSpeed(value);
    if (apiRef.current) apiRef.current.playbackSpeed = value;
  }

  function toggleLoop() {
    const next = !looping;
    setLooping(next);
    if (apiRef.current) apiRef.current.isLooping = next;
  }

  function clearRange() {
    if (apiRef.current) apiRef.current.playbackRange = null;
  }

  const showSheet = status === "loading" || status === "loaded";

  return (
    <div>
      <label className="inline-block cursor-pointer rounded-lg bg-amber-500 px-4 py-2 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400">
        {status === "loaded" ? "Nahrát jiný soubor" : "Nahrát soubor"}
        <input
          type="file"
          accept={ACCEPT}
          onChange={handleFile}
          className="hidden"
        />
      </label>

      {status === "empty" && (
        <p className="mt-3 text-sm text-neutral-500">
          Nahraj soubor Guitar Pro (.gp, .gpx) nebo MusicXML (.xml,
          .musicxml) — tabulatura i notový zápis se zobrazí níže.
        </p>
      )}
      {status === "loading" && (
        <p className="mt-3 text-sm text-neutral-500">Načítám soubor…</p>
      )}
      {status === "error" && (
        <p className="mt-3 text-sm text-red-400">{errorMessage}</p>
      )}

      {status === "loaded" && (
        <div className="mt-3 space-y-3">
          <div className="flex gap-2">
            <button
              type="button"
              onClick={() => apiRef.current?.playPause()}
              disabled={!playerReady}
              className={CONTROL_BUTTON}
            >
              {!playerReady
                ? "Načítám zvuk…"
                : playing
                  ? "Pauza"
                  : "Přehrát"}
            </button>
            <button
              type="button"
              onClick={() => apiRef.current?.stop()}
              disabled={!playerReady}
              className={CONTROL_BUTTON}
            >
              Stop
            </button>
          </div>

          <div className="flex items-center gap-2">
            <span className="text-sm text-neutral-400">Tempo:</span>
            <div className="inline-flex rounded-lg border border-neutral-800 p-1">
              {SPEEDS.map((value) => (
                <button
                  key={value}
                  type="button"
                  onClick={() => changeSpeed(value)}
                  className={speed === value ? SEGMENT_ACTIVE : SEGMENT_IDLE}
                >
                  {value}×
                </button>
              ))}
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={toggleLoop}
              className={
                looping
                  ? "rounded-lg bg-amber-500 px-4 py-2 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400"
                  : CONTROL_BUTTON
              }
            >
              {looping ? "Smyčka zapnutá" : "Smyčka"}
            </button>
            {looping && (
              <button
                type="button"
                onClick={clearRange}
                className={CONTROL_BUTTON}
              >
                Zrušit úsek
              </button>
            )}
          </div>
          {looping && (
            <p className="text-xs text-neutral-500">
              Tip: tažením po notách vyber úsek k opakování. Bez výběru se
              opakuje celá skladba.
            </p>
          )}
        </div>
      )}

      <div
        className={
          showSheet
            ? "mt-4 overflow-x-auto rounded-lg border border-neutral-800 bg-white p-2"
            : ""
        }
      >
        <div ref={containerRef} />
      </div>
    </div>
  );
}
