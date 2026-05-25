// Instrument profile and tone advisor — pure logic, no DOM or audio.
// Part of Bod 21. Unit-tested in instrument.test.ts.

/** A pickup construction type. */
export type PickupType = "humbucker" | "single" | "p90";

/** Where a pickup sits on the guitar. */
export type PickupPosition = "neck" | "middle" | "bridge";

/** One pickup installed on the instrument. */
export interface Pickup {
  position: PickupPosition;
  type: PickupType;
}

/**
 * The editable "digital passport" of an instrument. Tuning is intentionally
 * not stored here — it lives in settingsStore.tuningId as the single source
 * of truth shared with the tuner; the profile card edits that value directly.
 */
export interface InstrumentProfile {
  /** Display name of the instrument. */
  name: string;
  /** Body shape description, free text. */
  bodyType: string;
  /** Installed pickups, neck to bridge. */
  pickups: Pickup[];
  /** Scale length, free text (e.g. `25,5"`). */
  scaleLength: string;
  /** String gauge, free text (e.g. ".010-.046"). */
  stringGauge: string;
  /** Free-form notes. */
  notes: string;
}

/** A playing style the tone advisor gives advice for. */
export type ToneStyle =
  | "clean"
  | "blues"
  | "rock-rhythm"
  | "rock-lead"
  | "metal-rhythm"
  | "metal-lead"
  | "funk";

/** Gain level the tone advisor recommends. */
export type GainLevel = "low" | "medium" | "high";

/** Czech labels for pickup positions. */
export const POSITION_LABELS: Record<PickupPosition, string> = {
  neck: "Krk",
  middle: "Střed",
  bridge: "Kobylka",
};

/** Czech labels for pickup types. */
export const PICKUP_TYPE_LABELS: Record<PickupType, string> = {
  humbucker: "Humbucker",
  single: "Single",
  p90: "P90",
};

/** Czech labels for gain levels. */
export const GAIN_LABELS: Record<GainLevel, string> = {
  low: "Nízký",
  medium: "Střední",
  high: "Vysoký",
};

/** A selectable style for the tone advisor. */
export interface StyleDef {
  id: ToneStyle;
  /** Czech display name. */
  label: string;
}

/** Styles offered by the tone advisor. */
export const STYLES: StyleDef[] = [
  { id: "clean", label: "Čistý zvuk" },
  { id: "blues", label: "Blues" },
  { id: "rock-rhythm", label: "Rock — rytmika" },
  { id: "rock-lead", label: "Rock — sólo" },
  { id: "metal-rhythm", label: "Metal — rytmika" },
  { id: "metal-lead", label: "Metal — sólo" },
  { id: "funk", label: "Funk" },
];

/** A tone recommendation produced for a given style. */
export interface ToneAdvice {
  /** Recommended pickup position (resolved against the profile). */
  pickup: PickupPosition;
  /** Recommended gain level. */
  gain: GainLevel;
  /** EQ tips, short Czech sentence. */
  eq: string;
  /** Extra note — adapts to the profile's actual pickups. */
  note: string;
}

/** The default profile — editable, the user overwrites whatever they want. */
export const DEFAULT_PROFILE: InstrumentProfile = {
  name: "Solton Double Cut",
  bodyType: "Double cut, masivní tělo",
  pickups: [
    { position: "neck", type: "humbucker" },
    { position: "bridge", type: "humbucker" },
  ],
  scaleLength: '25,5"',
  stringGauge: ".010-.046",
  notes: "",
};

/** Base advice for each style, before adapting to the profile. */
interface BaseAdvice {
  /** The ideal pickup position for this style. */
  pickup: PickupPosition;
  gain: GainLevel;
  eq: string;
  /** Whether the style really wants a humbucker for its sound. */
  wantsHumbucker: boolean;
}

const BASE: Record<ToneStyle, BaseAdvice> = {
  clean: {
    pickup: "neck",
    gain: "low",
    eq: "Basy mírně nahoru, výšky zkrotit — plný a kulatý čistý zvuk.",
    wantsHumbucker: false,
  },
  blues: {
    pickup: "neck",
    gain: "low",
    eq: "Středy nahoru, nech aparát jemně breakovat na silnější úhoz.",
    wantsHumbucker: false,
  },
  "rock-rhythm": {
    pickup: "bridge",
    gain: "medium",
    eq: "Basy a středy vyvážené, výšky lehce stáhnout, ať to neřeže.",
    wantsHumbucker: true,
  },
  "rock-lead": {
    pickup: "neck",
    gain: "medium",
    eq: "Středy nahoru, ať sólo prořízne kapelu; basy ubrat.",
    wantsHumbucker: true,
  },
  "metal-rhythm": {
    pickup: "bridge",
    gain: "high",
    eq: "Středy ubrat (scoop), basy pevné, výšky pro definici riffů.",
    wantsHumbucker: true,
  },
  "metal-lead": {
    pickup: "bridge",
    gain: "high",
    eq: "Středy zpět nahoru proti scoopnuté rytmice, ať je sólo slyšet.",
    wantsHumbucker: true,
  },
  funk: {
    pickup: "bridge",
    gain: "low",
    eq: "Výšky nahoru, basy mírně dolů — ostrý a perkusivní zvuk.",
    wantsHumbucker: false,
  },
};

/** Pickup positions ordered neck to bridge. */
const ORDER: PickupPosition[] = ["neck", "middle", "bridge"];

/**
 * Finds the pickup best matching `ideal` — the exact position if present,
 * otherwise the nearest one. Returns null if the profile has no pickups.
 */
function resolvePickup(
  profile: InstrumentProfile,
  ideal: PickupPosition,
): Pickup | null {
  if (profile.pickups.length === 0) return null;
  const exact = profile.pickups.find((p) => p.position === ideal);
  if (exact) return exact;
  const idealIndex = ORDER.indexOf(ideal);
  let best: Pickup | null = null;
  let bestDistance = Number.POSITIVE_INFINITY;
  for (const position of ORDER) {
    const pickup = profile.pickups.find((p) => p.position === position);
    if (!pickup) continue;
    const distance = Math.abs(ORDER.indexOf(position) - idealIndex);
    if (distance < bestDistance) {
      bestDistance = distance;
      best = pickup;
    }
  }
  return best;
}

/**
 * Produces a tone recommendation for a style, adapted to the instrument's
 * actual pickups. Advisory only — the app never changes anything itself.
 */
export function recommendTone(
  profile: InstrumentProfile,
  style: ToneStyle,
): ToneAdvice {
  const base = BASE[style];
  const resolved = resolvePickup(profile, base.pickup);
  const pickup = resolved ? resolved.position : base.pickup;

  if (!resolved) {
    return {
      pickup,
      gain: base.gain,
      eq: base.eq,
      note: "Profil nemá zadané snímače — doplň je v kartě kytary a poradím přesněji.",
    };
  }

  const parts: string[] = [];
  if (resolved.position !== base.pickup) {
    parts.push(
      `Ideální by byl snímač u ${POSITION_LABELS[base.pickup].toLowerCase()}, ten v profilu nemáš — beru nejbližší (${POSITION_LABELS[pickup].toLowerCase()}).`,
    );
  }

  if (base.wantsHumbucker && resolved.type === "single") {
    parts.push(
      "V téhle pozici máš single — pro nářez bude tenčí a při vysokém gainu šumí. Drž gain o stupeň níž, nebo zvaž humbucker do kobylky.",
    );
  } else if (base.wantsHumbucker && resolved.type === "p90") {
    parts.push(
      "P90 v téhle pozici má víc kousavosti než single, ale míň než humbucker — gain klidně přidej, jen hlídej zpětnou vazbu.",
    );
  } else if (!base.wantsHumbucker && resolved.type === "humbucker") {
    parts.push(
      "Máš tu humbucker — pro jiskřivější zvuk stáhni knoflík tónu nebo použij split coil, pokud ho kytara umí.",
    );
  } else {
    parts.push("Snímač v profilu tomuhle stylu sedí — jedeš naplno.");
  }

  return { pickup, gain: base.gain, eq: base.eq, note: parts.join(" ") };
}

// --- Type guard -----------------------------------------------------------
// Data read back from storage can be corrupt or written by an older app
// version. The loaded value passes through this guard; malformed fields fall
// back to defaults so the app never crashes on bad data (see AGENTS.md).

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

const PICKUP_TYPES: PickupType[] = ["humbucker", "single", "p90"];

function sanitizePickup(value: unknown): Pickup | null {
  if (!isPlainObject(value)) return null;
  const { position, type } = value;
  if (
    typeof position !== "string" ||
    !ORDER.includes(position as PickupPosition)
  ) {
    return null;
  }
  if (typeof type !== "string" || !PICKUP_TYPES.includes(type as PickupType)) {
    return null;
  }
  return {
    position: position as PickupPosition,
    type: type as PickupType,
  };
}

function sanitizeString(value: unknown, fallback: string): string {
  return typeof value === "string" ? value : fallback;
}

/**
 * Coerces an unknown value into an InstrumentProfile. Malformed fields fall
 * back to the default profile; malformed pickups are dropped. Always returns
 * a usable profile.
 */
export function sanitizeProfile(value: unknown): InstrumentProfile {
  if (!isPlainObject(value)) return { ...DEFAULT_PROFILE };
  const pickups = Array.isArray(value.pickups)
    ? value.pickups
        .map(sanitizePickup)
        .filter((pickup): pickup is Pickup => pickup !== null)
    : DEFAULT_PROFILE.pickups;
  return {
    name: sanitizeString(value.name, DEFAULT_PROFILE.name),
    bodyType: sanitizeString(value.bodyType, DEFAULT_PROFILE.bodyType),
    pickups,
    scaleLength: sanitizeString(
      value.scaleLength,
      DEFAULT_PROFILE.scaleLength,
    ),
    stringGauge: sanitizeString(
      value.stringGauge,
      DEFAULT_PROFILE.stringGauge,
    ),
    notes: sanitizeString(value.notes, DEFAULT_PROFILE.notes),
  };
}
