"use client";

import { useEffect, useRef, useState } from "react";
import { audioEngine } from "@/audio/AudioEngine";
import {
  PALM_MUTE_DRILLS,
  calibrate,
  classifyHit,
  currentHitIndex,
  drillScore,
  isDrillComplete,
  logDrillHit,
  startPalmMuteDrill,
} from "@/lib/palmMute";
import type {
  MuteTechnique,
  PalmMuteCalibration,
  PalmMuteDrill,
  PalmMuteDrillState,
} from "@/lib/palmMute";
import { useMuteDetector } from "@/components/metal/useMuteDetector";
import { useAudioStore } from "@/stores/audioStore";

// Reference hits collected per technique during calibration.
const CALIBRATION_HITS = 4;

type ReadyMode = "detector" | "drill";

type StudioState =
  | { phase: "open"; openSamples: number[]; mutedSamples: number[] }
  | { phase: "muted"; openSamples: number[]; mutedSamples: number[] }
  | { phase: "ready"; calibration: PalmMuteCalibration };

const FRESH_STUDIO: StudioState = {
  phase: "open",
  openSamples: [],
  mutedSamples: [],
};

function clamp01(value: number): number {
  return Math.max(0, Math.min(1, value));
}

/** Short cell label for a technique. */
function techniqueLabel(technique: MuteTechnique): string {
  return technique === "muted" ? "utlum." : "otevř.";
}

/** Instruction-friendly adjective for a technique. */
function techniqueWord(technique: MuteTechnique): string {
  return technique === "muted" ? "utlumený" : "otevřený";
}

/**
 * Palm Mute Studio — calibrates to the player's setup, then offers a live
 * brightness detector and self-paced drills. Detection is one deliberate hit
 * at a time, classified against the calibration.
 */
export function PalmMuteStudio() {
  const status = useAudioStore((store) => store.status);
  const permission = useAudioStore((store) => store.permission);
  const error = useAudioStore((store) => store.error);
  const running = status === "running";

  const reading = useMuteDetector();
  const readingRef = useRef(reading);
  readingRef.current = reading;

  const [studio, setStudio] = useState<StudioState>(FRESH_STUDIO);
  const studioRef = useRef(studio);
  studioRef.current = studio;
  const [calibrationFailed, setCalibrationFailed] = useState(false);

  const [readyMode, setReadyMode] = useState<ReadyMode>("detector");
  const readyModeRef = useRef(readyMode);
  readyModeRef.current = readyMode;
  const [drillState, setDrillState] = useState<PalmMuteDrillState | null>(null);
  const drillStateRef = useRef(drillState);
  drillStateRef.current = drillState;

  const prevHitCountRef = useRef(0);

  function resetStudio() {
    setStudio(FRESH_STUDIO);
    setCalibrationFailed(false);
    setReadyMode("detector");
    setDrillState(null);
  }

  // Reset to a fresh calibration whenever the input (re)starts.
  useEffect(() => {
    if (running) {
      prevHitCountRef.current = 0;
      resetStudio();
    }
  }, [running]);

  // Release the microphone when leaving the studio.
  useEffect(() => {
    return () => {
      void audioEngine.stop();
    };
  }, []);

  // Route each detected hit by the current phase.
  useEffect(() => {
    if (reading.hitCount === prevHitCountRef.current) return;
    prevHitCountRef.current = reading.hitCount;
    if (reading.hitCount === 0) return;

    const centroid = readingRef.current.lastHitCentroid;
    const current = studioRef.current;

    if (current.phase === "open") {
      const openSamples = [...current.openSamples, centroid];
      if (openSamples.length < CALIBRATION_HITS) {
        setStudio({ ...current, openSamples });
      } else {
        setStudio({ phase: "muted", openSamples, mutedSamples: [] });
      }
      return;
    }

    if (current.phase === "muted") {
      const mutedSamples = [...current.mutedSamples, centroid];
      if (mutedSamples.length < CALIBRATION_HITS) {
        setStudio({ ...current, mutedSamples });
        return;
      }
      const calibration = calibrate(current.openSamples, mutedSamples);
      if (calibration) {
        setCalibrationFailed(false);
        setStudio({ phase: "ready", calibration });
      } else {
        setCalibrationFailed(true);
        setStudio(FRESH_STUDIO);
      }
      return;
    }

    // phase "ready": in drill mode, log the hit; otherwise the live verdict
    // is rendered directly from the reading.
    if (readyModeRef.current === "drill") {
      const drill = drillStateRef.current;
      if (drill && !isDrillComplete(drill)) {
        const detected = classifyHit(centroid, current.calibration);
        setDrillState(logDrillHit(drill, detected));
      }
    }
  }, [reading.hitCount]);

  function startInput() {
    void (async () => {
      if (permission !== "granted") {
        const result = await audioEngine.requestPermission();
        if (result !== "granted") return;
      }
      await audioEngine.start();
    })();
  }

  // --- Not running: start screen ------------------------------------------
  if (!running) {
    return (
      <div className="rounded-lg border border-line bg-surface p-6">
        <p className="text-sm text-text">
          Palm Mute Studio poslouchá tvou kytaru a pozná, jestli hraješ
          utlumeně, nebo otevřeně. Nejdřív se krátce zkalibruje. Vstupní
          zařízení vybereš v nastavení (ozubené kolo).
        </p>
        {permission === "denied" && (
          <p className="mt-2 text-sm text-danger">
            Přístup k mikrofonu je zamítnutý — povol ho v prohlížeči a zkus to
            znovu.
          </p>
        )}
        <button
          type="button"
          onClick={startInput}
          className="mt-4 rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90"
        >
          {status === "starting" ? "Spouštím…" : "Spustit detektor"}
        </button>
        {error && <p className="mt-3 text-sm text-danger">{error}</p>}
      </div>
    );
  }

  // --- Running: calibration ------------------------------------------------
  if (studio.phase === "open" || studio.phase === "muted") {
    const isOpenStep = studio.phase === "open";
    const done = isOpenStep
      ? studio.openSamples.length
      : studio.mutedSamples.length;

    return (
      <div className="rounded-xl border border-line bg-surface p-5">
        <div className="text-xs text-muted">
          Kalibrace — krok {isOpenStep ? "1" : "2"} / 2
        </div>
        <h2 className="mt-1 text-xl font-semibold">
          {isOpenStep ? "Zahraj 4 otevřené údery" : "Zahraj 4 utlumené údery"}
        </h2>
        <p className="mt-2 text-sm text-dim">
          {isOpenStep
            ? "Zahraj power chord E5 (na nízké E struně) a nech struny volně znít. Čtyřikrát."
            : "Zahraj stejný power chord E5, ale s palm mute — dlaní pravé ruky u kobylky. Čtyřikrát."}
        </p>

        <div className="mt-4 flex gap-2">
          {Array.from({ length: CALIBRATION_HITS }, (_, index) => (
            <div
              key={index}
              className={`h-3 flex-1 rounded-full ${
                index < done ? "bg-accent" : "bg-surface-2"
              }`}
            />
          ))}
        </div>
        <div className="mt-2 text-sm text-muted">
          Zachyceno {done} / {CALIBRATION_HITS} úderů
        </div>

        {calibrationFailed && (
          <p className="mt-4 text-sm text-danger">
            Kalibrace se nepovedla — utlumené údery nevyšly tmavší než
            otevřené. Zkus to znovu, palm mute pořádně přitlum.
          </p>
        )}

        <button
          type="button"
          onClick={() => void audioEngine.stop()}
          className="mt-5 rounded-lg border border-line bg-bg px-4 py-2 text-sm transition-colors hover:border-line-strong"
        >
          Zastavit
        </button>
      </div>
    );
  }

  // --- Running: ready (detector + drill) ----------------------------------
  const { calibration } = studio;

  return (
    <div className="rounded-xl border border-line bg-surface p-5">
      {/* Mode toggle */}
      <div className="inline-flex rounded-lg border border-line p-1">
        {(["detector", "drill"] as ReadyMode[]).map((mode) => (
          <button
            key={mode}
            type="button"
            onClick={() => setReadyMode(mode)}
            className={
              readyMode === mode
                ? "rounded-md bg-surface-2 px-3 py-1.5 text-sm text-accent"
                : "rounded-md px-3 py-1.5 text-sm text-dim transition-colors hover:text-text"
            }
          >
            {mode === "detector" ? "Detektor" : "Drill"}
          </button>
        ))}
      </div>

      {readyMode === "detector" && (
        <DetectorView calibration={calibration} reading={reading} />
      )}
      {readyMode === "drill" && (
        <DrillView
          drillState={drillState}
          onPick={(drill) => setDrillState(startPalmMuteDrill(drill))}
          onRestart={(drill) => setDrillState(startPalmMuteDrill(drill))}
          onBack={() => setDrillState(null)}
        />
      )}

      {/* Recalibrate / stop */}
      <div className="mt-6 flex gap-3 border-t border-line pt-4">
        <button
          type="button"
          onClick={resetStudio}
          className="rounded-lg border border-line bg-bg px-4 py-2 text-sm transition-colors hover:border-line-strong"
        >
          Překalibrovat
        </button>
        <button
          type="button"
          onClick={() => void audioEngine.stop()}
          className="rounded-lg border border-line bg-bg px-4 py-2 text-sm transition-colors hover:border-line-strong"
        >
          Zastavit
        </button>
      </div>
    </div>
  );
}

/** Live brightness meter and the verdict of the most recent hit. */
function DetectorView({
  calibration,
  reading,
}: {
  calibration: PalmMuteCalibration;
  reading: { centroid: number; hitCount: number; lastHitCentroid: number };
}) {
  const span = calibration.openMean - calibration.mutedMean;
  const markerPosition = clamp01(
    (reading.centroid - calibration.mutedMean) / span,
  );
  const verdict =
    reading.hitCount > 0
      ? classifyHit(reading.lastHitCentroid, calibration)
      : null;

  return (
    <div className="mt-4">
      <div className="text-center">
        {verdict === null && (
          <div className="text-2xl font-bold text-muted">Zahraj úder</div>
        )}
        {verdict === "muted" && (
          <div className="text-3xl font-bold text-accent">UTLUMENO</div>
        )}
        {verdict === "open" && (
          <div className="text-3xl font-bold text-sky-400">OTEVŘENO</div>
        )}
      </div>

      <div className="mt-5">
        <div className="relative h-4 rounded-full bg-gradient-to-r from-accent via-line-strong to-sky-900">
          <div className="absolute inset-y-0 left-1/2 w-0.5 -translate-x-1/2 bg-surface-2" />
          <div
            className="absolute top-1/2 h-5 w-5 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-line bg-white transition-all duration-75"
            style={{ left: `${markerPosition * 100}%` }}
          />
        </div>
        <div className="mt-1 flex justify-between text-xs text-muted">
          <span>tmavé (utlumeno)</span>
          <span>světlé (otevřeno)</span>
        </div>
      </div>

      <p className="mt-4 text-sm text-dim">
        Hraj power chord E5 a sleduj značku — vlevo od středu je utlumený zvuk,
        vpravo otevřený.
      </p>
    </div>
  );
}

/** Drill picker and the active drill run. */
function DrillView({
  drillState,
  onPick,
  onRestart,
  onBack,
}: {
  drillState: PalmMuteDrillState | null;
  onPick: (drill: PalmMuteDrill) => void;
  onRestart: (drill: PalmMuteDrill) => void;
  onBack: () => void;
}) {
  if (!drillState) {
    return (
      <div className="mt-4">
        <p className="text-sm text-dim">Vyber drill:</p>
        <div className="mt-3 space-y-2">
          {PALM_MUTE_DRILLS.map((drill) => (
            <button
              key={drill.id}
              type="button"
              onClick={() => onPick(drill)}
              className="block w-full rounded-lg border border-line bg-bg px-4 py-3 text-left transition-colors hover:border-accent"
            >
              <div className="font-medium">{drill.name}</div>
              <div className="mt-0.5 text-sm text-dim">
                {drill.description}
              </div>
            </button>
          ))}
        </div>
      </div>
    );
  }

  const { drill, hits } = drillState;
  const complete = isDrillComplete(drillState);
  const currentIndex = currentHitIndex(drillState);
  const nextTechnique = currentIndex < 0 ? null : drill.pattern[currentIndex];

  return (
    <div className="mt-4">
      <div className="text-sm font-medium text-text">{drill.name}</div>

      {/* Pattern cells */}
      <div className="mt-3 flex gap-1.5">
        {drill.pattern.map((expected, index) => {
          const logged = index < hits.length;
          const detected = logged ? hits[index] : null;
          const correct = logged && detected === expected;
          const isCurrent = index === currentIndex;

          let cellClass =
            "border-line bg-bg text-muted";
          if (logged) {
            cellClass = correct
              ? "border-success bg-success/15 text-success"
              : "border-danger bg-danger/15 text-danger";
          } else if (isCurrent) {
            cellClass = "border-accent bg-accent/10 text-accent";
          }

          return (
            <div
              key={index}
              className={`flex h-14 flex-1 items-center justify-center rounded-lg border text-xs font-medium ${cellClass}`}
            >
              {techniqueLabel(detected ?? expected)}
            </div>
          );
        })}
      </div>

      {!complete && nextTechnique && (
        <p className="mt-3 text-sm text-text">
          Zahraj{" "}
          <span className="font-semibold text-accent">
            {techniqueWord(nextTechnique)}
          </span>{" "}
          úder.
        </p>
      )}

      {complete && (
        <div className="mt-4">
          <div className="text-lg font-semibold">
            Skóre: {drillScore(drillState)} / {drill.pattern.length} správně
          </div>
          <div className="mt-3 flex gap-3">
            <button
              type="button"
              onClick={() => onRestart(drill)}
              className="rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90"
            >
              Zkusit znovu
            </button>
            <button
              type="button"
              onClick={onBack}
              className="rounded-lg border border-line bg-bg px-4 py-2 text-sm transition-colors hover:border-line-strong"
            >
              Zpět na výběr
            </button>
          </div>
        </div>
      )}

      {!complete && (
        <button
          type="button"
          onClick={onBack}
          className="mt-4 text-sm text-muted transition-colors hover:text-text"
        >
          Zpět na výběr drillu
        </button>
      )}
    </div>
  );
}
