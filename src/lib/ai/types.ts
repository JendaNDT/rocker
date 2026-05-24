// AI chat — pure types and type guards, no DOM or network. Part of Bod 14
// (AI tutor "zeptej se"). Unit-tested in types.test.ts.

/**
 * Author of a chat message.
 * - "user"  — written by the person using the app
 * - "model" — written by the AI (Gemini's own term for the assistant role,
 *   reused here so messages map straight onto the API request)
 */
export type ChatRole = "user" | "model";

/** A single message in a chat conversation. */
export interface ChatMessage {
  role: ChatRole;
  /** Plain-text content of the message. */
  text: string;
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/**
 * Coerces an unknown value into a ChatMessage, or null when it is malformed.
 * Used on data read back from storage — corrupt or older records are dropped
 * so the app never crashes on bad data (see AGENTS.md).
 */
export function sanitizeChatMessage(value: unknown): ChatMessage | null {
  if (!isPlainObject(value)) return null;
  const { role, text } = value;
  if (role !== "user" && role !== "model") return null;
  if (typeof text !== "string") return null;
  return { role, text };
}

/**
 * Coerces an unknown value into a ChatMessage array, dropping any bad
 * entries. A non-array input yields an empty history.
 */
export function sanitizeChatHistory(value: unknown): ChatMessage[] {
  if (!Array.isArray(value)) return [];
  return value
    .map(sanitizeChatMessage)
    .filter((message): message is ChatMessage => message !== null);
}
