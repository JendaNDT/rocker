"use client";

import { useEffect, useState } from "react";
import {
  useSettingsStore,
  type ColorMode,
  type Theme,
} from "@/stores/settingsStore";

interface ThemeOption {
  id: Theme;
  name: string;
  tag: string;
}

const THEMES: ThemeOption[] = [
  { id: "studio", name: "Studio", tag: "Tube amp · serif" },
  { id: "vintage", name: "Vintage", tag: "Staré studio · brass" },
  { id: "glassy", name: "Glassy", tag: "Frosted · gradient" },
];

const MODES: { value: ColorMode; label: string; icon: string }[] = [
  { value: "light", label: "Světlý", icon: "☀" },
  { value: "dark", label: "Tmavý", icon: "☾" },
  { value: "auto", label: "Auto (OS)", icon: "◐" },
];

export function ThemePicker() {
  const theme = useSettingsStore((s) => s.theme);
  const colorMode = useSettingsStore((s) => s.colorMode);
  const setTheme = useSettingsStore((s) => s.setTheme);
  const setColorMode = useSettingsStore((s) => s.setColorMode);

  const [hydrated, setHydrated] = useState(false);
  useEffect(() => setHydrated(true), []);

  // Před hydratací zobrazíme defaults, aby SSR HTML sedělo s prvním renderem.
  const currentTheme = hydrated ? theme : "studio";
  const currentMode = hydrated ? colorMode : "dark";

  return (
    <div>
      <div className="grid gap-3 sm:grid-cols-3">
        {THEMES.map((t) => (
          <button
            key={t.id}
            type="button"
            onClick={() => setTheme(t.id)}
            aria-pressed={currentTheme === t.id}
            className={`relative rounded-lg border-2 p-3 text-left transition-colors ${
              currentTheme === t.id
                ? "border-accent ring-4 ring-accent-soft"
                : "border-line hover:border-line-strong"
            }`}
            style={{ background: "var(--surface)" }}
          >
            <div className="mb-2 h-24 overflow-hidden rounded-md border border-line">
              <ThemePreview theme={t.id} />
            </div>
            <div className="font-semibold text-text">{t.name}</div>
            <div className="mt-0.5 text-xs text-dim uppercase tracking-wider">
              {t.tag}
            </div>
            {currentTheme === t.id && (
              <span className="absolute right-2 top-2 grid h-5 w-5 place-items-center rounded-full bg-accent text-accent-fg">
                <svg
                  width="12"
                  height="12"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="3"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                >
                  <path d="M5 12l5 5L20 7" />
                </svg>
              </span>
            )}
          </button>
        ))}
      </div>

      <div className="mt-4">
        <div className="text-xs uppercase tracking-wider text-dim">Režim</div>
        <div className="mt-2 inline-flex rounded-lg border border-line p-1">
          {MODES.map((m) => (
            <button
              key={m.value}
              type="button"
              onClick={() => setColorMode(m.value)}
              className={`flex items-center gap-2 rounded-md px-3 py-1.5 text-sm transition-colors ${
                currentMode === m.value
                  ? "bg-surface-2 text-accent"
                  : "text-dim hover:text-text"
              }`}
            >
              <span>{m.icon}</span> {m.label}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}

/* ── Static mini-previews — copy-paste from the mockup ───── */

function ThemePreview({ theme }: { theme: Theme }) {
  if (theme === "studio") {
    return (
      <div
        style={{
          position: "relative",
          height: "100%",
          background: "#0e0d0b",
          backgroundImage:
            "radial-gradient(ellipse at top, rgba(200,163,90,0.16), transparent 60%)",
          padding: 8,
          display: "flex",
          flexDirection: "column",
          gap: 4,
        }}
      >
        <div
          style={{
            fontFamily: '"Bodoni Moda", Georgia, serif',
            fontSize: 14,
            color: "#f1e9d8",
          }}
        >
          Studio. <i style={{ color: "#c8a35a" }}>Console.</i>
        </div>
        <div
          style={{
            fontFamily: "monospace",
            fontSize: 7,
            color: "#c8a35a",
            letterSpacing: 1.5,
          }}
        >
          LADIČKA · A4
        </div>
        <div
          style={{
            fontFamily: '"Bodoni Moda", Georgia, serif',
            fontSize: 32,
            color: "#7eb567",
            letterSpacing: -1,
            lineHeight: 0.9,
          }}
        >
          E<span style={{ fontSize: 12, color: "#8a8276" }}>2</span>
        </div>
        <div style={{ display: "flex", gap: 2, marginTop: "auto" }}>
          {["E", "A", "D", "G", "B", "E"].map((n, i) => (
            <span
              key={i}
              style={{
                flex: 1,
                padding: "2px 0",
                textAlign: "center",
                background: i === 0 ? "#c8a35a" : "#1f1c18",
                borderRadius: 2,
                fontFamily: '"Bodoni Moda", Georgia, serif',
                fontSize: 9,
                color: i === 0 ? "#1a1408" : "#f1e9d8",
              }}
            >
              {n}
            </span>
          ))}
        </div>
      </div>
    );
  }
  if (theme === "vintage") {
    return (
      <div
        style={{
          position: "relative",
          height: "100%",
          overflow: "hidden",
          background:
            "radial-gradient(ellipse at top, rgba(194,81,30,0.10), transparent 65%)," +
            " linear-gradient(180deg, #f1e6cc 0%, #d8c89e 100%)",
          padding: 8,
          display: "flex",
          flexDirection: "column",
          gap: 6,
        }}
      >
        {/* paper grain + vignette */}
        <div
          style={{
            position: "absolute",
            inset: 0,
            pointerEvents: "none",
            backgroundImage:
              "repeating-linear-gradient(0deg, transparent 0, transparent 2px, rgba(80,60,30,0.04) 2px, rgba(80,60,30,0.04) 3px)",
            opacity: 0.7,
          }}
        />
        <div
          style={{
            position: "absolute",
            inset: 0,
            pointerEvents: "none",
            background:
              "radial-gradient(ellipse at center, transparent 50%, rgba(60,40,12,0.20) 100%)",
          }}
        />
        {/* brass studs in 4 corners */}
        {[
          { top: 3, left: 3 },
          { top: 3, right: 3 },
          { bottom: 3, left: 3 },
          { bottom: 3, right: 3 },
        ].map((p, i) => (
          <span
            key={i}
            style={{
              position: "absolute",
              width: 6,
              height: 6,
              borderRadius: "50%",
              background:
                "radial-gradient(circle at 32% 28%, #f5e0a6, #8a6f3a 60%, #4a3815)",
              boxShadow:
                "inset 0 -0.5px 1px rgba(60,40,12,0.5), 0 0.5px 1px rgba(60,40,12,0.4)",
              ...p,
            }}
          />
        ))}
        {/* ink-stamp eyebrow */}
        <div
          style={{
            alignSelf: "flex-start",
            position: "relative",
            border: "1.2px solid #c2511e",
            borderRadius: 2,
            padding: "1px 6px",
            transform: "rotate(-1.5deg)",
            fontFamily: '"Special Elite", monospace',
            fontSize: 7,
            color: "#c2511e",
            letterSpacing: 1.5,
          }}
        >
          LADIČKA · A4
        </div>
        {/* cream paper note display */}
        <div
          style={{
            position: "relative",
            background: "#f5ebd3",
            backgroundImage:
              "repeating-linear-gradient(0deg, transparent 0, transparent 2px, rgba(80,60,30,0.035) 2px, rgba(80,60,30,0.035) 3px)",
            border: "1px solid #8c7647",
            borderRadius: 4,
            padding: "3px 8px",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "baseline",
            boxShadow: "inset 0 1px 2px rgba(60,40,12,0.18)",
          }}
        >
          <span
            style={{
              fontFamily: '"Fraunces", Georgia, serif',
              fontSize: 18,
              color: "#2c1f0e",
              letterSpacing: -1,
              fontWeight: 600,
            }}
          >
            E<span style={{ fontSize: 10, color: "#6e5b3d", marginLeft: 1 }}>2</span>
          </span>
          <span
            style={{
              fontFamily: '"Special Elite", monospace',
              fontSize: 8,
              color: "#6e5b3d",
            }}
          >
            +2¢
          </span>
        </div>
        {/* brass plate button + olive LED + IN TUNE */}
        <div
          style={{
            display: "flex",
            gap: 5,
            marginTop: "auto",
            alignItems: "center",
            position: "relative",
          }}
        >
          <span
            style={{
              padding: "3px 8px",
              borderRadius: 3,
              background: "linear-gradient(180deg, #d4b070, #8e6f3e)",
              color: "#2c1f0e",
              fontFamily: '"Fraunces", Georgia, serif',
              fontSize: 8,
              fontWeight: 600,
              border: "1px solid #5a4220",
              boxShadow:
                "inset 0 1px 0 rgba(255,250,200,0.55), inset 0 -1px 1px rgba(60,40,12,0.30)",
              textShadow: "0 0.5px 0 rgba(255,250,200,0.5)",
            }}
          >
            Spustit
          </span>
          <span
            style={{
              width: 5,
              height: 5,
              borderRadius: "50%",
              background: "#6b8e23",
              boxShadow: "0 0 4px #6b8e23",
            }}
          />
          <span
            style={{
              fontFamily: '"Special Elite", monospace',
              fontSize: 7,
              color: "#2c1f0e",
              letterSpacing: 1,
            }}
          >
            IN TUNE
          </span>
        </div>
      </div>
    );
  }
  return (
    <div
      style={{
        position: "relative",
        height: "100%",
        background:
          "radial-gradient(120% 80% at 0% 0%, #4a1f8c 0%, transparent 55%)," +
          " radial-gradient(120% 80% at 100% 10%, #c4196b 0%, transparent 50%)," +
          " radial-gradient(120% 100% at 50% 110%, #1e2a8c 0%, transparent 65%)," +
          " linear-gradient(180deg, #18113a, #0e0820)",
        padding: 8,
        display: "flex",
        flexDirection: "column",
        gap: 5,
      }}
    >
      <div
        style={{
          background: "rgba(255,255,255,0.10)",
          backdropFilter: "blur(20px) saturate(180%)",
          WebkitBackdropFilter: "blur(20px) saturate(180%)",
          border: "0.5px solid rgba(255,255,255,0.2)",
          borderRadius: 8,
          padding: "4px 7px",
        }}
      >
        <div
          style={{
            fontFamily: '"Instrument Serif", Georgia, serif',
            fontSize: 12,
            color: "#fff",
          }}
        >
          Glassy
        </div>
        <div
          style={{
            fontFamily: '"Instrument Serif", Georgia, serif',
            fontSize: 18,
            color: "#fff",
            background: "linear-gradient(135deg, #ff4d8a, #39d4ff)",
            WebkitBackgroundClip: "text",
            backgroundClip: "text",
            WebkitTextFillColor: "transparent",
            letterSpacing: -1,
          }}
        >
          E2
        </div>
      </div>
      <div style={{ marginTop: "auto", display: "flex", gap: 3 }}>
        {["E", "A", "D", "G", "B", "E"].map((n, i) => (
          <span
            key={i}
            style={{
              flex: 1,
              padding: "2px 0",
              textAlign: "center",
              background:
                i === 0
                  ? "linear-gradient(135deg, #ff4d8a, #39d4ff)"
                  : "rgba(255,255,255,0.10)",
              border: "0.5px solid rgba(255,255,255,0.2)",
              borderRadius: 5,
              fontFamily: '"Instrument Serif", Georgia, serif',
              fontSize: 8,
              color: "#fff",
            }}
          >
            {n}
          </span>
        ))}
      </div>
    </div>
  );
}
