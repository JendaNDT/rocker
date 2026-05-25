"use client";

import { useEffect, useRef, useState } from "react";
import { streamChat } from "@/lib/ai/geminiClient";
import { SUGGESTED_QUESTIONS } from "@/lib/ai/prompts";
import type { ChatMessage } from "@/lib/ai/types";
import { useAiStore } from "@/stores/aiStore";
import { useChatStore } from "@/stores/chatStore";

/**
 * The "Zeptat se" chat panel of the AI tutor screen. Assumes an API key is
 * set — the parent screen gates on that. Part of Bod 14.
 */
export function ChatPanel() {
  const apiKey = useAiStore((state) => state.apiKey);
  const model = useAiStore((state) => state.model);

  const messages = useChatStore((state) => state.messages);
  const setMessages = useChatStore((state) => state.setMessages);
  const addMessage = useChatStore((state) => state.addMessage);
  const clearChat = useChatStore((state) => state.clear);

  const [input, setInput] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // The reply currently streaming in — empty until the first chunk arrives.
  const [streaming, setStreaming] = useState("");

  const endRef = useRef<HTMLDivElement>(null);
  const mountedRef = useRef(true);

  // Track mount state so an in-flight stream can't update an unmounted panel.
  useEffect(() => {
    return () => {
      mountedRef.current = false;
    };
  }, []);

  // Keep the latest message in view as the conversation grows.
  useEffect(() => {
    endRef.current?.scrollIntoView({ block: "nearest" });
  }, [messages, busy, streaming]);

  /** Streams a reply from the AI for the given history and appends it. */
  async function ask(history: ChatMessage[]) {
    setBusy(true);
    setError(null);
    setStreaming("");
    const result = await streamChat({
      apiKey,
      model,
      history,
      onChunk: (text) => {
        if (mountedRef.current) setStreaming(text);
      },
    });
    // A finished reply is saved to the store even if the user navigated away;
    // component-state updates are skipped once the panel has unmounted.
    if (result.ok) {
      addMessage({ role: "model", text: result.text });
    }
    if (!mountedRef.current) return;
    setStreaming("");
    setBusy(false);
    if (!result.ok) {
      setError(result.error);
    }
  }

  /** Sends the given text as a new user message. */
  function submit(rawText: string) {
    const text = rawText.trim();
    if (!text || busy) return;
    const nextHistory: ChatMessage[] = [
      ...messages,
      { role: "user", text },
    ];
    setMessages(nextHistory);
    setInput("");
    void ask(nextHistory);
  }

  function send() {
    submit(input);
  }

  /** Re-sends the last user message after a failed request. */
  function retry() {
    if (busy) return;
    const last = messages[messages.length - 1];
    if (!last || last.role !== "user") return;
    void ask(messages);
  }

  function reset() {
    clearChat();
    setError(null);
    setInput("");
  }

  function handleKeyDown(event: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      send();
    }
  }

  return (
    <div>
      {messages.length > 0 && (
        <div className="mb-3 flex justify-end">
          <button
            type="button"
            onClick={reset}
            className="rounded-lg border border-line bg-bg px-3 py-1.5 text-sm text-dim transition-colors hover:border-line-strong"
          >
            Nová konverzace
          </button>
        </div>
      )}

      <div className="max-h-[60vh] space-y-3 overflow-y-auto rounded-lg border border-line bg-bg p-4">
        {messages.length === 0 && !busy && (
          <div className="py-6">
            <p className="text-center text-sm text-muted">
              Napiš svou první otázku, nebo zkus některou z těchto:
            </p>
            <div className="mt-3 flex flex-col gap-2">
              {SUGGESTED_QUESTIONS.map((question) => (
                <button
                  key={question}
                  type="button"
                  onClick={() => submit(question)}
                  className="rounded-lg border border-line bg-surface px-3 py-2 text-left text-sm text-text transition-colors hover:border-accent hover:text-text"
                >
                  {question}
                </button>
              ))}
            </div>
          </div>
        )}

        {messages.map((message, index) => (
          <div
            key={index}
            className={
              message.role === "user"
                ? "ml-auto max-w-[85%] rounded-lg border border-accent/40 bg-accent/10 px-3 py-2 text-sm whitespace-pre-wrap"
                : "mr-auto max-w-[85%] rounded-lg border border-line bg-surface px-3 py-2 text-sm whitespace-pre-wrap"
            }
          >
            {message.text}
          </div>
        ))}

        {busy && streaming.length > 0 && (
          <div className="mr-auto max-w-[85%] rounded-lg border border-line bg-surface px-3 py-2 text-sm whitespace-pre-wrap">
            {streaming}
          </div>
        )}

        {busy && streaming.length === 0 && (
          <div className="mr-auto max-w-[85%] rounded-lg border border-line bg-surface px-3 py-2 text-sm text-muted">
            Přemýšlí…
          </div>
        )}

        {error && (
          <div className="mr-auto max-w-[85%]">
            <p className="text-sm text-danger">{error}</p>
            <button
              type="button"
              onClick={retry}
              className="mt-2 rounded-md border border-line bg-surface px-3 py-1 text-xs transition-colors hover:border-line-strong"
            >
              Zkusit znovu
            </button>
          </div>
        )}

        <div ref={endRef} />
      </div>

      <div className="mt-3 flex gap-2">
        <textarea
          value={input}
          onChange={(event) => setInput(event.target.value)}
          onKeyDown={handleKeyDown}
          rows={2}
          placeholder="Napiš otázku… (Enter odešle, Shift+Enter nový řádek)"
          className="min-w-0 flex-1 resize-none rounded-lg border border-line bg-bg px-3 py-2 text-sm text-text"
        />
        <button
          type="button"
          onClick={send}
          disabled={busy || input.trim().length === 0}
          className="shrink-0 self-end rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90 disabled:cursor-not-allowed disabled:opacity-50"
        >
          Odeslat
        </button>
      </div>
    </div>
  );
}
