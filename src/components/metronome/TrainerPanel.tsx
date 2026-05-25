import type { GapConfig, RampConfig } from "@/lib/metronomeConfig";
import { MAX_BPM, MIN_BPM } from "@/lib/tempo";

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
 * Tempo trainer controls: progressive speed-up and the gap trainer.
 * Purely presentational — changes are reported through the callbacks.
 */
export function TrainerPanel({
  ramp,
  gap,
  onChangeRamp,
  onChangeGap,
}: {
  ramp: RampConfig;
  gap: GapConfig;
  onChangeRamp: (patch: Partial<RampConfig>) => void;
  onChangeGap: (patch: Partial<GapConfig>) => void;
}) {
  return (
    <div className="space-y-4">
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
