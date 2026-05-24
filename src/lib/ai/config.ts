// AI configuration — the Gemini API endpoint and the models offered to the
// user. Part of Bod 14.

/**
 * Base URL of the Gemini REST API.
 *
 * The native generateContent endpoint sends CORS headers, so the app calls
 * it straight from the browser — no proxy or backend needed. (The separate
 * OpenAI-compatibility endpoint does NOT allow browser calls; this is not
 * that one.)
 */
export const GEMINI_API_BASE =
  "https://generativelanguage.googleapis.com/v1beta";

/** A Gemini model the user can choose in settings. */
export interface AiModel {
  /** API model id, e.g. "gemini-2.5-flash". */
  id: string;
  /** Czech UI label. */
  label: string;
  /** Short Czech note on the trade-off. */
  note: string;
}

/**
 * Models offered in settings. Only Gemini's free-tier models are listed —
 * paid/Pro models are intentionally left out so the app stays free to use.
 */
export const AI_MODELS: AiModel[] = [
  {
    id: "gemini-2.5-flash",
    label: "Gemini 2.5 Flash",
    note: "Doporučeno — chytré odpovědi, 1500 dotazů denně zdarma.",
  },
  {
    id: "gemini-2.5-flash-lite",
    label: "Gemini 2.5 Flash-Lite",
    note: "Rychlejší a lehčí, také 1500 dotazů denně zdarma.",
  },
];

/** Default model id, used when nothing is stored yet. */
export const DEFAULT_AI_MODEL = "gemini-2.5-flash";

/** True when the given id is one of the offered models. */
export function isKnownModel(id: string): boolean {
  return AI_MODELS.some((model) => model.id === id);
}
