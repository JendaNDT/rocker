import type { GapConfig, RampConfig } from "@/lib/metronomeConfig";
import { swingSupported } from "@/lib/metronomeConfig";
import { MAX_BPM, MIN_BPM } from "@/lib/tempo";

/** Maps a swing ratio to a short Czech label shown next to the slider. */
function swingLabel(swing: number): string {
  if (swing <= 0.52) return "rovné";
  if (swing < 0.62) return "lehký swing";
  if (swing < 0.71) return "triolový";
  return "shuffle";
}

function SliderRow({
  label,
  value,
  min,
  max,
  unit,
  onChange,
}: {
  label: string;
  value: number;
  min: number;
  max: number;
  unit: string;
  onChange: (value: number) => void;
}) {
  return (
    <div>
      <div className="flex justify-between text-sm text-text">
        <span>{label}</span>
        <span className="text-dim tabular-nums">
          {value} {unit}
        </span>
      </div>
      <input
        type="range"
        min={min}
        max={max}
        value={value}
        onChange={(event) => onChange(Number(event.target.value))}
        aria-label={label}
        className="mt-1 w-full accent-accent"
      />
    </div>
  );
}

/**
 * Tempo trainer controls: swing / shuffle feel, progressive speed-up and
 * the gap trainer. Purely presentational — changes are reported through
 * the callbacks.
 */
export function TrainerPanel({
  ramp,
  gap,
  swing,
  subdivision,
  onChangeRamp,
  onChangeGap,
  onChangeSwing,
}: {
  ramp: RampConfig;
  gap: GapConfig;
  swing: number;
  /** Current subdivision — drives the swing slider's disabled state. */
  subdivision: number;
  onChangeRamp: (patch: Partial<RampConfig>) => void;
  onChangeGap: (patch: Partial<GapConfig>) => void;
  onChangeSwing: (swing: number) => void;
}) {
  const swingActive = swingSupported(subdivision);
  // Slider talks in percent (50..75); engine talks in ratio (0.5..0.75).
  const swingPercent = Math.round(swing * 100);
  return (
    <div className="space-y-4">
      <div className="rounded-lg border border-line bg-surface p-4">
        <div className="flex items-center justify-between gap-3">
          <span className="text-sm font-medium text-text">Swing / Shuffle</span>
          <span className="text-xs text-dim tabular-nums">
            {swingPercent} %{" "}
            <span className="text-muted">({swingLabel(swing)})</span>
          </span>
        </div>
        <input
          type="range"
          min={50}
          max={75}
          value={swingPercent}
          disabled={!swingActive}
          aria-label="Swing / Shuffle"
          onChange={(event) => onChangeSwing(Number(event.target.value) / 100)}
          className="mt-2 w-full accent-accent disabled:opacity-40"
        />
        <p className="mt-1 text-xs text-muted">
          {swingActive
            ? "Posune každou druhou osminu / šestnáctinu pro shuffle feel. 50 % = rovné, 67 % = jazz, 75 % = shuffle."
            : "Swing funguje jen na osminy nebo šestnáctiny — přepni doby v nastavení."}
        </p>
      </div>

      <div className="rounded-lg border border-line bg-surface p-4">
        <label className="flex items-center gap-2 text-sm font-medium text-text">
          <input
            type="checkbox"
            checked={ramp.enabled}
            onChange={(event) =>
              onChangeRamp({ enabled: event.target.checked })
            }
            className="h-4 w-4 accent-accent"
          />
          Progresivní zrychlování
        </label>
        {ramp.enabled && (
          <div className="mt-3 space-y-3">
            <SliderRow
              label="Zrychlit každých … taktů"
              value={ramp.everyBars}
              min={1}
              max={16}
              unit="taktů"
              onChange={(value) => onChangeRamp({ everyBars: value })}
            />
            <SliderRow
              label="Přidat tempo"
              value={ramp.stepBpm}
              min={1}
              max={20}
              unit="BPM"
              onChange={(value) => onChangeRamp({ stepBpm: value })}
            />
            <SliderRow
              label="Cílové tempo"
              value={ramp.targetBpm}
              min={MIN_BPM}
              max={MAX_BPM}
              unit="BPM"
              onChange={(value) => onChangeRamp({ targetBpm: value })}
            />
            <p className="text-xs text-muted">
              Metronom začne tvým aktuálním tempem a sám zrychluje až k cíli.
            </p>
          </div>
        )}
      </div>

      <div className="rounded-lg border border-line bg-surface p-4">
        <label className="flex items-center gap-2 text-sm font-medium text-text">
          <input
            type="checkbox"
            checked={gap.enabled}
            onChange={(event) => onChangeGap({ enabled: event.target.checked })}
            className="h-4 w-4 accent-accent"
          />
          Cvičné ticho
        </label>
        {gap.enabled && (
          <div className="mt-3 space-y-3">
            <SliderRow
              label="Hraje … taktů"
              value={gap.audibleBars}
              min={1}
              max={16}
              unit="taktů"
              onChange={(value) => onChangeGap({ audibleBars: value })}
            />
            <SliderRow
              label="Ticho … taktů"
              value={gap.silentBars}
              min={1}
              max={16}
              unit="taktů"
              onChange={(value) => onChangeGap({ silentBars: value })}
            />
            <p className="text-xs text-muted">
              Metronom pravidelně ztichne — udržíš tempo i bez něj? Na konci
              ticha se rozsvítí první doba.
            </p>
          </div>
        )}
      </div>
    </div>
  );
}
