import { create } from "zustand";
import { persist } from "zustand/middleware";
import { DEFAULT_AI_MODEL, isKnownModel } from "@/lib/ai/config";

interface AiState {
  /** The user's own Gemini API key (BYOK). Empty string when not set. */
  apiKey: string;
  setApiKey: (apiKey: string) => void;
  /** Selected Gemini model id (see src/lib/ai/config.ts). */
  model: string;
  setModel: (model: string) => void;
}

/**
 * AI tutor settings, persisted in localStorage under "rocker-ai". Holds the
 * user's own Gemini API key (BYOK) and chosen model — small structured data,
 * so localStorage is enough (IndexedDB stays reserved for large blobs).
 */
export const useAiStore = create<AiState>()(
  persist(
    (set) => ({
      apiKey: "",
      setApiKey: (apiKey) => set({ apiKey }),
      model: DEFAULT_AI_MODEL,
      setModel: (model) => set({ model }),
    }),
    {
      name: "rocker-ai",
      // Stored data can be corrupt or written by an older app version. Each
      // field is checked here; anything invalid falls back to a safe default
      // so the app never starts in a broken AI state (see AGENTS.md).
      merge: (persisted, current) => {
        const safe = { ...current };
        if (typeof persisted === "object" && persisted !== null) {
          const stored = persisted as Record<string, unknown>;
          if (typeof stored.apiKey === "string") {
            safe.apiKey = stored.apiKey;
          }
          if (typeof stored.model === "string" && isKnownModel(stored.model)) {
            safe.model = stored.model;
          }
        }
        return safe;
      },
    },
  ),
);
