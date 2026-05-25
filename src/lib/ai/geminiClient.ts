// Gemini chat client. Calls the Gemini generateContent API directly from
// the browser — the native endpoint sends CORS headers, so no proxy or
// backend is needed. Part of Bod 14.
//
// Request — POST to
//   {GEMINI_API_BASE}/models/{model}:generateContent
// with the user's key in the "x-goog-api-key" header and the conversation
// (system instruction + messages) as the JSON body.

import { GEMINI_API_BASE } from "./config";
import { TUTOR_SYSTEM_PROMPT } from "./prompts";
import type { ChatMessage } from "./types";

/**
 * Outcome of a chat request. sendChat never throws — it always resolves to
 * one of these two shapes, so callers handle success and failure uniformly.
 */
export type ChatResult =
  | { ok: true; text: string }
  | { ok: false; error: string };

/** Arguments for {@link sendChat}. */
export interface SendChatOptions {
  /** The user's own Gemini API key (BYOK). */
  apiKey: string;
  /** Gemini model id, e.g. "gemini-2.5-flash". */
  model: string;
  /** Whole conversation so far, oldest first; last message is the user's. */
  history: ChatMessage[];
  /** Optional cancellation signal. */
  signal?: AbortSignal;
  /** System instruction — defaults to the guitar-tutor prompt. */
  systemPrompt?: string;
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
const ERR_BLOCKED =
  "Odpověď zablokoval bezpečnostní filtr. Zkus otázku přeformulovat.";
const ERR_EMPTY =
  "AI nevrátila žádnou odpověď. Zkus otázku přeformulovat.";
const ERR_NETWORK =
  "Nepodařilo se spojit se serverem. Zkontroluj připojení k internetu.";
const ERR_ABORTED = "Dotaz byl zrušen.";
const ERR_NO_MESSAGE =
  "Není žádná zpráva k odeslání. Napiš nejdřív otázku.";

// --- Gemini response shape (only the fields we read) ---------------------

interface GeminiPart {
  text?: unknown;
}
interface GeminiResponse {
  candidates?: Array<{ content?: { parts?: GeminiPart[] } }>;
  promptFeedback?: { blockReason?: unknown };
}

/** Builds the Gemini request body from the conversation history. */
function buildBody(history: ChatMessage[], systemPrompt: string) {
  return {
    systemInstruction: { parts: [{ text: systemPrompt }] },
    contents: history.map((message) => ({
      role: message.role,
      parts: [{ text: message.text }],
    })),
    generationConfig: {
      temperature: 0.7,
      // Generous cap — keeps answers from being truncated while still
      // bounding how much of the free daily quota one reply can use.
      maxOutputTokens: 2048,
    },
  };
}

/** Maps an HTTP error status to a Czech user-facing message. */
function mapHttpError(status: number): string {
  // 400/401/403 from a BYOK request almost always means a bad or
  // unauthorised API key.
  if (status === 400 || status === 401 || status === 403) return ERR_BAD_KEY;
  if (status === 429) return ERR_RATE_LIMIT;
  return ERR_SERVER;
}

/** Joins the text parts of a Gemini response candidate (no trimming). */
function rawText(data: GeminiResponse | null): string {
  const parts = data?.candidates?.[0]?.content?.parts;
  if (!Array.isArray(parts)) return "";
  return parts
    .map((part) => (typeof part.text === "string" ? part.text : ""))
    .join("");
}

/** Extracts the assistant's reply text from a Gemini response. */
function extractText(data: GeminiResponse | null): string {
  return rawText(data).trim();
}

/** Parses one SSE `data:` payload into its text piece, or "" if none. */
function parseSseData(payload: string): string {
  if (!payload || payload === "[DONE]") return "";
  try {
    return rawText(JSON.parse(payload) as GeminiResponse);
  } catch {
    return "";
  }
}

/**
 * Sends the conversation to the AI tutor and resolves with the reply.
 * Never throws — failures come back as { ok: false, error } with a Czech
 * message ready to show the user.
 */
export async function sendChat(
  options: SendChatOptions,
): Promise<ChatResult> {
  const { apiKey, model, history, signal } = options;
  const apiBase = options.apiBase ?? GEMINI_API_BASE;

  const key = apiKey.trim();
  if (!key) return { ok: false, error: ERR_NO_KEY };
  // An empty conversation would make Gemini reject the request with a 400,
  // which mapHttpError would mistranslate into a misleading "bad key" error.
  if (history.length === 0) return { ok: false, error: ERR_NO_MESSAGE };

  const url = `${apiBase}/models/${model}:generateContent`;

  let response: Response;
  try {
    response = await fetch(url, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "x-goog-api-key": key,
      },
      body: JSON.stringify(
        buildBody(history, options.systemPrompt ?? TUTOR_SYSTEM_PROMPT),
      ),
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
    return { ok: false, error: ERR_BLOCKED };
  }

  const text = extractText(data);
  if (!text) return { ok: false, error: ERR_EMPTY };

  return { ok: true, text };
}

/** Arguments for {@link streamChat} — same as sendChat plus a chunk callback. */
export interface StreamChatOptions extends SendChatOptions {
  /** Called with the full reply text so far each time more of it arrives. */
  onChunk: (textSoFar: string) => void;
}

/**
 * Like {@link sendChat}, but streams the reply: `onChunk` is called with the
 * growing text as it arrives from Gemini's SSE endpoint. Resolves with the
 * final ChatResult. Never throws — failures come back as { ok: false, error }.
 */
export async function streamChat(
  options: StreamChatOptions,
): Promise<ChatResult> {
  const { apiKey, model, history, signal, onChunk } = options;
  const apiBase = options.apiBase ?? GEMINI_API_BASE;

  const key = apiKey.trim();
  if (!key) return { ok: false, error: ERR_NO_KEY };
  // An empty conversation would make Gemini reject the request with a 400,
  // which mapHttpError would mistranslate into a misleading "bad key" error.
  if (history.length === 0) return { ok: false, error: ERR_NO_MESSAGE };

  const url = `${apiBase}/models/${model}:streamGenerateContent?alt=sse`;

  let response: Response;
  try {
    response = await fetch(url, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "x-goog-api-key": key,
      },
      body: JSON.stringify(
        buildBody(history, options.systemPrompt ?? TUTOR_SYSTEM_PROMPT),
      ),
      signal,
    });
  } catch {
    if (signal?.aborted) return { ok: false, error: ERR_ABORTED };
    return { ok: false, error: ERR_NETWORK };
  }

  if (!response.ok) {
    return { ok: false, error: mapHttpError(response.status) };
  }
  if (!response.body) {
    return { ok: false, error: ERR_SERVER };
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";
  let fullText = "";

  try {
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });

      // SSE events are newline-delimited; process every complete line and
      // keep the unfinished tail in the buffer for the next read.
      let newlineIndex = buffer.indexOf("\n");
      while (newlineIndex !== -1) {
        const line = buffer.slice(0, newlineIndex).trim();
        buffer = buffer.slice(newlineIndex + 1);
        if (line.startsWith("data:")) {
          const piece = parseSseData(line.slice(5).trim());
          if (piece) {
            fullText += piece;
            onChunk(fullText);
          }
        }
        newlineIndex = buffer.indexOf("\n");
      }
    }
  } catch {
    if (signal?.aborted) return { ok: false, error: ERR_ABORTED };
    return { ok: false, error: ERR_NETWORK };
  }

  // The final SSE event can arrive without a trailing newline, so a last
  // `data:` line may still be sitting in the buffer — process it too.
  const tail = buffer.trim();
  if (tail.startsWith("data:")) {
    const piece = parseSseData(tail.slice(5).trim());
    if (piece) {
      fullText += piece;
      onChunk(fullText);
    }
  }

  const text = fullText.trim();
  if (!text) return { ok: false, error: ERR_EMPTY };

  return { ok: true, text };
}
