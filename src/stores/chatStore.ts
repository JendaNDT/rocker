import { create } from "zustand";
import { persist } from "zustand/middleware";
import { sanitizeChatHistory } from "@/lib/ai/types";
import type { ChatMessage } from "@/lib/ai/types";

interface ChatState {
  /** The current AI tutor conversation, oldest message first. */
  messages: ChatMessage[];
  /** Replaces the whole conversation. */
  setMessages: (messages: ChatMessage[]) => void;
  /** Appends one message to the conversation. */
  addMessage: (message: ChatMessage) => void;
  /** Clears the conversation (the "Nová konverzace" action). */
  clear: () => void;
}

/**
 * AI tutor chat history, persisted in localStorage under "rocker-chat" so the
 * conversation survives a page reload or app restart. Small structured data,
 * so localStorage is enough (IndexedDB stays reserved for large blobs).
 */
export const useChatStore = create<ChatState>()(
  persist(
    (set) => ({
      messages: [],
      setMessages: (messages) => set({ messages }),
      addMessage: (message) =>
        set((state) => ({ messages: [...state.messages, message] })),
      clear: () => set({ messages: [] }),
    }),
    {
      name: "rocker-chat",
      // Stored data can be corrupt or from an older app version — every
      // message passes through the type guard; bad entries are dropped so
      // the app never starts with a broken conversation (see AGENTS.md).
      merge: (persisted, current) => {
        const safe = { ...current };
        if (typeof persisted === "object" && persisted !== null) {
          const stored = persisted as Record<string, unknown>;
          safe.messages = sanitizeChatHistory(stored.messages);
        }
        return safe;
      },
    },
  ),
);
