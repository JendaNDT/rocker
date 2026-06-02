"use client";

import { type ReactNode, useEffect, useRef, useState } from "react";
import type { AlphaTabApi } from "@coderline/alphatab";
import { useActiveTheme } from "@/lib/useActiveTheme";
import type { Theme } from "@/stores/settingsStore";

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
  "rounded-lg border border-line bg-surface px-4 py-2 text-sm transition-colors hover:border-line-strong disabled:cursor-not-allowed disabled:opacity-50";
const SEGMENT_ACTIVE =
  "rounded-md bg-accent px-3 py-1.5 text-sm font-semibold text-accent-fg";
const SEGMENT_IDLE =
  "rounded-md px-3 py-1.5 text-sm text-dim transition-colors hover:text-text";

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
  const theme = useActiveTheme();

  return (
    <div>
      <label className="inline-block cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90">
        {status === "loaded" ? "Nahrát jiný soubor" : "Nahrát soubor"}
        <input
          type="file"
          accept={ACCEPT}
          onChange={handleFile}
          className="hidden"
        />
      </label>

      {status === "empty" && (
        <p className="mt-3 text-sm text-muted">
          Nahraj soubor Guitar Pro (.gp, .gpx) nebo MusicXML (.xml,
          .musicxml) — tabulatura i notový zápis se zobrazí níže.
          Soubor zůstane ve tvém prohlížeči, nikam se neposílá.
        </p>
      )}
      {status === "loading" && (
        <p className="mt-3 text-sm text-muted">Načítám soubor…</p>
      )}
      {status === "error" && (
        <p className="mt-3 text-sm text-danger">{errorMessage}</p>
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
            <span className="text-sm text-dim">Tempo:</span>
            <div className="inline-flex rounded-lg border border-line p-1">
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
                  ? "rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90"
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
            <p className="text-xs text-muted">
              Tip: tažením po notách vyber úsek k opakování. Bez výběru se
              opakuje celá skladba.
            </p>
          )}
        </div>
      )}

      <SheetFrame visible={showSheet} theme={theme}>
        <div ref={containerRef} />
      </SheetFrame>
    </div>
  );
}

/**
 * Wrapper around the alphaTab render target. The inner surface always stays
 * white — alphaTab paints the score in black ink and would be unreadable on
 * a dark panel. Only the frame around the paper changes per theme.
 *
 * Pre-hydration (`theme === null`) and the unloaded state both fall back to
 * the original neutral border so the first render matches the prerendered
 * HTML.
 */
function SheetFrame({
  visible,
  theme,
  children,
}: {
  visible: boolean;
  theme: Theme | null;
  children: ReactNode;
}) {
  if (!visible) return <div>{children}</div>;

  if (theme === "studio") {
    // LCD-style backlit panel: brass bezel hugs a clean white sheet with a
    // warm glow halo. Reads as an instrument display.
    return (
      <div
        className="mt-4 rounded-lg p-[2px]"
        style={{
          background:
            "linear-gradient(180deg, #f0d68e 0%, #d4a83e 35%, #b88a2a 70%, #6e5023 100%)",
          boxShadow:
            "0 0 22px rgba(212,168,62,0.25), 0 4px 14px rgba(0,0,0,0.45)",
        }}
      >
        <div
          className="overflow-x-auto rounded-md bg-white p-3"
          style={{
            boxShadow: "inset 0 0 18px rgba(212,168,62,0.18)",
          }}
        >
          {children}
        </div>
      </div>
    );
  }

  if (theme === "vintage") {
    // Old printed tablature: brass-edged paper sheet with a soft grain
    // overlay and a darker vignette around the page.
    return (
      <div
        className="mt-4 rounded-sm p-[3px]"
        style={{
          background:
            "linear-gradient(180deg, #d4b070 0%, #8e6f3e 50%, #4a3220 100%)",
          boxShadow:
            "0 1px 0 rgba(255,250,232,0.4), 0 6px 18px rgba(40,20,8,0.55)",
        }}
      >
        <div
          className="relative overflow-x-auto rounded-[2px] bg-[#f5ebd3] p-3"
          style={{
            backgroundImage:
              "radial-gradient(circle at 20% 30%, rgba(140,118,71,0.12) 0, transparent 14px), radial-gradient(circle at 75% 65%, rgba(140,118,71,0.10) 0, transparent 12px), repeating-linear-gradient(0deg, transparent 0, transparent 3px, rgba(80,60,30,0.025) 3px, rgba(80,60,30,0.025) 4px)",
            boxShadow:
              "inset 0 1px 0 rgba(255,250,232,0.6), inset 0 0 24px rgba(60,40,12,0.25)",
          }}
        >
          {children}
        </div>
      </div>
    );
  }

  if (theme === "glassy") {
    // Frosted glass slab with a neon halo around the page.
    return (
      <div
        className="mt-4 rounded-2xl p-[2px]"
        style={{
          background:
            "conic-gradient(from 180deg, #39d4ff, #c84db8, #ff4d8a, #c84db8, #39d4ff)",
          boxShadow:
            "0 0 22px rgba(255,77,138,0.45), 0 0 10px rgba(57,212,255,0.35)",
        }}
      >
        <div
          className="overflow-x-auto rounded-2xl bg-white p-3"
          style={{
            boxShadow: "inset 0 0 22px rgba(255,255,255,0.6)",
          }}
        >
          {children}
        </div>
      </div>
    );
  }

  // Default fallback — also the SSR-matching pre-hydration render.
  return (
    <div className="mt-4 overflow-x-auto rounded-lg border border-line bg-white p-2">
      {children}
    </div>
  );
}
