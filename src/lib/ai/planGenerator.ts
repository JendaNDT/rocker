// AI exercise-plan generator. Calls the Gemini generateContent API in JSON
// mode and parses the reply into a structured practice plan. Part of Bod 14.

import { GEMINI_API_BASE } from "./config";
import { PLAN_SYSTEM_PROMPT } from "./prompts";

/** One block of an AI-generated practice plan. */
export interface GeneratedPlanItem {
  /** Czech display title. */
  title: string;
  /** Czech one-line instruction. */
  detail: string;
  /** Suggested duration in minutes. */
  minutes: number;
  /** Route to the relevant app screen, or null when none fits. */
  href: string | null;
}

/** A full AI-generated practice plan. */
export interface GeneratedPlan {
  items: GeneratedPlanItem[];
  /** Sum of every block's minutes. */
  totalMinutes: number;
}

/**
 * Outcome of a plan request. generatePlan never throws — it always resolves
 * to one of these two shapes.
 */
export type PlanResult =
  | { ok: true; plan: GeneratedPlan }
  | { ok: false; error: string };

/** Arguments for {@link generatePlan}. */
export interface GeneratePlanOptions {
  /** The user's own Gemini API key (BYOK). */
  apiKey: string;
  /** Gemini model id. */
  model: string;
  /** Minutes the user has for today's practice. */
  minutes: number;
  /** Optional free-text focus, e.g. "palm mute". */
  focus?: string;
  /** Number of lessons the user has completed. */
  completedLessons: number;
  /** Total number of lessons available. */
  totalLessons: number;
  /** Current practice streak in days. */
  streakDays: number;
  /** Optional ready-built context block describing the user's guitar. */
  instrumentSummary?: string;
  /** Optional cancellation signal. */
  signal?: AbortSignal;
  /** API base URL override — defaults to GEMINI_API_BASE; set in tests. */
  apiBase?: string;
}

// --- Czech user-facing error messages ------------------------------------

const ERR_NO_KEY =
  "Není zadaný API klíč. Vlož ho v Profilu v sekci AI tutor.";
const ERR_BAD_KEY =
  "API klíč nefunguje. Zkontroluj ho v Profilu — možná je v něm překlep.";
const ERR_RATE_LIMIT =
  "Vyčerpal jsi denní limit dotazů zdarma. Zkus to prosím později.";
const ERR_SERVER = "Server AI měl potíže. Zkus to prosím za chvíli znovu.";
const ERR_NETWORK =
  "Nepodařilo se spojit se serverem. Zkontroluj připojení k internetu.";
const ERR_ABORTED = "Generování bylo zrušeno.";
const ERR_PLAN =
  "Plán se nepodařilo vygenerovat. Zkus to prosím znovu.";

// --- Screen routing ------------------------------------------------------

/** App screens the AI may attach to a plan block, mapped to their routes. */
const SCREEN_HREF: Record<string, string> = {
  tuner: "/tuner",
  metronome: "/metronome",
  practice: "/practice",
  metal: "/metal",
  library: "/library",
  youtube: "/youtube",
  recorder: "/recorder",
};

/** JSON schema constraining Gemini's reply to a plan shape. */
const PLAN_SCHEMA = {
  type: "object",
  properties: {
    items: {
      type: "array",
      items: {
        type: "object",
        properties: {
          title: { type: "string" },
          detail: { type: "string" },
          minutes: { type: "integer" },
          screen: { type: "string", enum: Object.keys(SCREEN_HREF) },
        },
        required: ["title", "detail", "minutes", "screen"],
      },
    },
  },
  required: ["items"],
};

interface GeminiResponse {
  candidates?: Array<{ content?: { parts?: Array<{ text?: unknown }> } }>;
  promptFeedback?: { blockReason?: unknown };
}

/** Builds the user prompt describing today's request and the user's level. */
function buildUserPrompt(options: GeneratePlanOptions): string {
  const lines = [
    "Sestav cvičební plán na dnešek.",
    `Mám dnes ${options.minutes} minut.`,
  ];
  const focus = options.focus?.trim();
  if (focus) lines.push(`Chci se zaměřit na: ${focus}.`);
  lines.push(
    `Z lekcí aplikace mám hotových ${options.completedLessons} z ${options.totalLessons}.`,
  );
  if (options.streakDays > 0) {
    lines.push(`Cvičím v řadě ${options.streakDays} dní.`);
  }
  const instrumentSummary = options.instrumentSummary?.trim();
  if (instrumentSummary) lines.push(`\n${instrumentSummary}`);
  return lines.join("\n");
}

/** Maps an HTTP error status to a Czech user-facing message. */
function mapHttpError(status: number): string {
  if (status === 400 || status === 401 || status === 403) return ERR_BAD_KEY;
  if (status === 429) return ERR_RATE_LIMIT;
  return ERR_SERVER;
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/** Upper bound for a single plan block — clamps absurd AI-returned values. */
const MAX_ITEM_MINUTES = 180;

/** Coerces an unknown value into a GeneratedPlanItem, or null if malformed. */
function sanitizePlanItem(value: unknown): GeneratedPlanItem | null {
  if (!isPlainObject(value)) return null;
  const { title, detail, minutes, screen } = value;
  if (typeof title !== "string" || title.trim().length === 0) return null;
  if (typeof detail !== "string" || detail.trim().length === 0) return null;
  if (typeof minutes !== "number" || !Number.isFinite(minutes)) return null;
  const rounded = Math.round(minutes);
  if (rounded <= 0) return null;
  // The AI can return an absurd duration; cap it so one block can't blow
  // past the time the user actually asked for.
  const safeMinutes = Math.min(rounded, MAX_ITEM_MINUTES);
  const href =
    typeof screen === "string" && screen in SCREEN_HREF
      ? SCREEN_HREF[screen]
      : null;
  return { title, detail, minutes: safeMinutes, href };
}

/**
 * Coerces an unknown value into a GeneratedPlan, or null if it has no usable
 * items. Broken items are dropped so a partly-bad reply still yields a plan.
 */
export function sanitizeGeneratedPlan(value: unknown): GeneratedPlan | null {
  if (!isPlainObject(value)) return null;
  const rawItems = value.items;
  if (!Array.isArray(rawItems)) return null;
  const items = rawItems
    .map(sanitizePlanItem)
    .filter((item): item is GeneratedPlanItem => item !== null);
  if (items.length === 0) return null;
  const totalMinutes = items.reduce((sum, item) => sum + item.minutes, 0);
  return { items, totalMinutes };
}

/** Reads the reply text (the JSON string) out of a Gemini response. */
function extractText(data: GeminiResponse | null): string {
  const parts = data?.candidates?.[0]?.content?.parts;
  if (!Array.isArray(parts)) return "";
  return parts
    .map((part) => (typeof part.text === "string" ? part.text : ""))
    .join("");
}

/**
 * Asks the AI to generate a practice plan and resolves with the structured
 * result. Never throws — failures come back as { ok: false, error } with a
 * Czech message ready to show the user.
 */
export async function generatePlan(
  options: GeneratePlanOptions,
): Promise<PlanResult> {
  const { apiKey, model, signal } = options;
  const apiBase = options.apiBase ?? GEMINI_API_BASE;

  const key = apiKey.trim();
  if (!key) return { ok: false, error: ERR_NO_KEY };

  const url = `${apiBase}/models/${model}:generateContent`;
  const body = {
    systemInstruction: { parts: [{ text: PLAN_SYSTEM_PROMPT }] },
    contents: [{ role: "user", parts: [{ text: buildUserPrompt(options) }] }],
    generationConfig: {
      temperature: 0.7,
      maxOutputTokens: 2048,
      responseMimeType: "application/json",
      responseSchema: PLAN_SCHEMA,
    },
  };

  let response: Response;
  try {
    response = await fetch(url, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "x-goog-api-key": key,
      },
      body: JSON.stringify(body),
      signal,
    });
  } catch {
    if (signal?.aborted) return { ok: false, error: ERR_ABORTED };
    return { ok: false, error: ERR_NETWORK };
  }

  let data: GeminiResponse | null = null;
  try {
    data = (await response.json()) as GeminiResponse;
  } catch {
    data = null;
  }

  if (!response.ok) {
    return { ok: false, error: mapHttpError(response.status) };
  }
  if (data?.promptFeedback?.blockReason) {
    return { ok: false, error: ERR_PLAN };
  }

  const text = extractText(data);
  if (!text) return { ok: false, error: ERR_PLAN };

  let parsed: unknown = null;
  try {
    parsed = JSON.parse(text);
  } catch {
    return { ok: false, error: ERR_PLAN };
  }

  const plan = sanitizeGeneratedPlan(parsed);
  if (!plan) return { ok: false, error: ERR_PLAN };

  return { ok: true, plan };
}
