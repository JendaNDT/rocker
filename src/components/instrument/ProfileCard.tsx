"use client";

import { TUNINGS } from "@/audio/tunings";
import {
  PICKUP_TYPE_LABELS,
  POSITION_LABELS,
  type Pickup,
  type PickupPosition,
  type PickupType,
} from "@/lib/instrument";
import { useInstrumentStore } from "@/stores/instrumentStore";
import { useSettingsStore } from "@/stores/settingsStore";

const INPUT =
  "w-full rounded-md border border-line bg-bg px-3 py-2 text-sm text-text transition-colors focus:border-accent focus:outline-none";
const SELECT =
  "rounded-md border border-line bg-bg px-2 py-1.5 text-sm text-text focus:border-accent focus:outline-none";
const LABEL = "text-xs font-medium text-dim";

const POSITIONS: PickupPosition[] = ["neck", "middle", "bridge"];
const TYPES: PickupType[] = ["humbucker", "single", "p90"];

/** One labelled text input bound to a profile field. */
function TextField({
  label,
  value,
  placeholder,
  onChange,
}: {
  label: string;
  value: string;
  placeholder?: string;
  onChange: (value: string) => void;
}) {
  return (
    <label className="block">
      <span className={LABEL}>{label}</span>
      <input
        type="text"
        value={value}
        placeholder={placeholder}
        onChange={(event) => onChange(event.target.value)}
        className={`mt-1 ${INPUT}`}
      />
    </label>
  );
}

/**
 * Editable "digital passport" of the instrument (Bod 21). Most fields are
 * free-form; tuning is a dropdown bound to settingsStore.tuningId, the value
 * shared with the tuner, so it is set in one place across the app.
 */
export function ProfileCard() {
  const profile = useInstrumentStore((state) => state.profile);
  const updateProfile = useInstrumentStore((state) => state.updateProfile);
  const reset = useInstrumentStore((state) => state.reset);
  const tuningId = useSettingsStore((state) => state.tuningId);
  const setTuningId = useSettingsStore((state) => state.setTuningId);

  function setPickup(index: number, patch: Partial<Pickup>) {
    updateProfile({
      pickups: profile.pickups.map((pickup, i) =>
        i === index ? { ...pickup, ...patch } : pickup,
      ),
    });
  }

  function addPickup() {
    updateProfile({
      pickups: [
        ...profile.pickups,
        { position: "bridge", type: "humbucker" },
      ],
    });
  }

  function removePickup(index: number) {
    updateProfile({
      pickups: profile.pickups.filter((_, i) => i !== index),
    });
  }

  return (
    <div className="rounded-lg border border-line bg-surface p-4">
      <div className="flex items-baseline justify-between gap-4">
        <h2 className="text-lg font-semibold">Profil nástroje</h2>
        <button
          type="button"
          onClick={reset}
          className="rounded-md border border-line bg-bg px-2.5 py-1 text-xs text-dim transition-colors hover:border-line-strong"
        >
          Obnovit výchozí
        </button>
      </div>
      <p className="mt-1 text-sm text-dim">
        Vyplň, co o své kytaře víš — co nevíš, nech prázdné. Ukládá se to
        v prohlížeči.
      </p>

      <div className="mt-4 grid gap-3 sm:grid-cols-2">
        <TextField
          label="Název nástroje"
          value={profile.name}
          onChange={(name) => updateProfile({ name })}
        />
        <TextField
          label="Typ těla"
          value={profile.bodyType}
          placeholder="Např. Double cut"
          onChange={(bodyType) => updateProfile({ bodyType })}
        />
        <TextField
          label="Menzura"
          value={profile.scaleLength}
          placeholder='Např. 25,5"'
          onChange={(scaleLength) => updateProfile({ scaleLength })}
        />
        <label className="block">
          <span className={LABEL}>Ladění</span>
          <select
            value={tuningId}
            onChange={(event) => setTuningId(event.target.value)}
            aria-label="Ladění"
            className={`mt-1 ${INPUT}`}
          >
            {TUNINGS.map((tuning) => (
              <option key={tuning.id} value={tuning.id}>
                {tuning.label}
              </option>
            ))}
          </select>
        </label>
        <TextField
          label="Síla strun"
          value={profile.stringGauge}
          placeholder="Např. .010-.046"
          onChange={(stringGauge) => updateProfile({ stringGauge })}
        />
      </div>

      <div className="mt-4">
        <div className="flex items-baseline justify-between gap-4">
          <span className={LABEL}>Snímače</span>
          <button
            type="button"
            onClick={addPickup}
            className="rounded-md border border-line bg-bg px-2.5 py-1 text-xs text-dim transition-colors hover:border-line-strong"
          >
            Přidat snímač
          </button>
        </div>

        {profile.pickups.length === 0 ? (
          <p className="mt-2 text-sm text-muted">
            Žádné snímače — bez nich tónový poradce neporadí přesně.
          </p>
        ) : (
          <ul className="mt-2 space-y-2">
            {profile.pickups.map((pickup, index) => (
              <li
                key={index}
                className="flex flex-wrap items-center gap-2 rounded-md border border-line bg-bg p-2"
              >
                <select
                  value={pickup.position}
                  onChange={(event) =>
                    setPickup(index, {
                      position: event.target.value as PickupPosition,
                    })
                  }
                  className={SELECT}
                  aria-label="Pozice snímače"
                >
                  {POSITIONS.map((position) => (
                    <option key={position} value={position}>
                      {POSITION_LABELS[position]}
                    </option>
                  ))}
                </select>
                <select
                  value={pickup.type}
                  onChange={(event) =>
                    setPickup(index, {
                      type: event.target.value as PickupType,
                    })
                  }
                  className={SELECT}
                  aria-label="Typ snímače"
                >
                  {TYPES.map((type) => (
                    <option key={type} value={type}>
                      {PICKUP_TYPE_LABELS[type]}
                    </option>
                  ))}
                </select>
                <button
                  type="button"
                  onClick={() => removePickup(index)}
                  className="ml-auto rounded-md px-2 py-1 text-xs text-muted transition-colors hover:text-danger"
                >
                  Odebrat
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>

      <label className="mt-4 block">
        <span className={LABEL}>Poznámky</span>
        <textarea
          value={profile.notes}
          onChange={(event) => updateProfile({ notes: event.target.value })}
          rows={3}
          placeholder="Cokoliv dalšího — úpravy, nastavení, postřehy."
          className={`mt-1 ${INPUT} resize-y`}
        />
      </label>
    </div>
  );
}
