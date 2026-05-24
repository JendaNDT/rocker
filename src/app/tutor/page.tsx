"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { sendChat } from "@/lib/ai/geminiClient";
import type { ChatMessage } from "@/lib/ai/types";
import { useHydrated } from "@/lib/useHydrated";
import { useAiStore } from "@/stores/aiStore";

export default function TutorPage() {
  const hydrated = useHydrated();
  const apiKey = useAiStore((state) => state.apiKey);
  const model = useAiStore((state) => state.model);

  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const endRef = useRef<HTMLDivElement>(null);

  // Keep the latest message in view as the conversation grows.
  useEffect(() => {
    endRef.current?.scrollIntoView({ block: "nearest" });
  }, [messages, busy]);

  const hasKey = apiKey.trim().length > 0;

  /** Sends the given history to the AI and appends the reply. */
  async function ask(history: ChatMessage[]) {
    setBusy(true);
    setError(null);
    const result = await sendChat({ apiKey, model, history });
    setBusy(false);
    if (result.ok) {
      setMessages((current) => [
        ...current,
        { role: "model", text: result.text },
      ]);
    } else {
      setError(result.error);
    }
  }

  function send() {
    const text = input.trim();
    if (!text || busy) return;
    const nextHistory: ChatMessage[] = [
      ...messages,
      { role: "user", text },
    ];
    setMessages(nextHistory);
    setInput("");
    void ask(nextHistory);
  }

  /** Re-sends the last user message after a failed request. */
  function retry() {
    if (busy) return;
    const last = messages[messages.length - 1];
    if (!last || last.role !== "user") return;
    void ask(messages);
  }

  function reset() {
    setMessages([]);
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
    <div className="mx-auto max-w-3xl p-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">AI tutor</h1>
          <p className="mt-1 text-neutral-400">
            Zeptej se na techniku, cvičení nebo zvuk.
          </p>
        </div>
        {hydrated && hasKey && messages.length > 0 && (
          <button
            type="button"
            onClick={reset}
            className="shrink-0 rounded-lg border border-neutral-800 bg-neutral-950 px-3 py-1.5 text-sm text-neutral-400 transition-colors hover:border-neutral-700"
          >
            Nová konverzace
          </button>
        )}
      </div>

      {!hydrated ? (
        <p className="mt-6 text-sm text-neutral-500">Načítám…</p>
      ) : !hasKey ? (
        <div className="mt-6 rounded-lg border border-neutral-800 bg-neutral-900 p-4">
          <p className="text-sm text-neutral-300">
            AI tutor potřebuje vlastní klíč Google Gemini — vyzvedneš si ho
            zdarma. Nastav ho v Profilu v sekci „AI tutor", pak se sem vrať.
          </p>
          <Link
            href="/profile"
            className="mt-4 inline-block rounded-lg bg-amber-500 px-4 py-2 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400"
          >
            Otevřít nastavení
          </Link>
        </div>
      ) : (
        <>
          <div className="mt-6 max-h-[60vh] space-y-3 overflow-y-auto rounded-lg border border-neutral-800 bg-neutral-950 p-4">
            {messages.length === 0 && !busy && (
              <p className="py-8 text-center text-sm text-neutral-500">
                Napiš svou první otázku — třeba „Jak procvičit rychlejší
                střídavý úhozy?" nebo „Proč mi vrže akord G?"
              </p>
            )}

            {messages.map((message, index) => (
              <div
                key={index}
                className={
                  message.role === "user"
                    ? "ml-auto max-w-[85%] rounded-lg border border-amber-600/40 bg-amber-500/10 px-3 py-2 text-sm whitespace-pre-wrap"
                    : "mr-auto max-w-[85%] rounded-lg border border-neutral-800 bg-neutral-900 px-3 py-2 text-sm whitespace-pre-wrap"
                }
              >
                {message.text}
              </div>
            ))}

            {busy && (
              <div className="mr-auto max-w-[85%] rounded-lg border border-neutral-800 bg-neutral-900 px-3 py-2 text-sm text-neutral-500">
                Přemýšlí…
              </div>
            )}

            {error && (
              <div className="mr-auto max-w-[85%]">
                <p className="text-sm text-red-400">{error}</p>
                <button
                  type="button"
                  onClick={retry}
                  className="mt-2 rounded-md border border-neutral-800 bg-neutral-900 px-3 py-1 text-xs transition-colors hover:border-neutral-700"
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
              className="min-w-0 flex-1 resize-none rounded-lg border border-neutral-800 bg-neutral-950 px-3 py-2 text-sm text-neutral-100"
            />
            <button
              type="button"
              onClick={send}
              disabled={busy || input.trim().length === 0}
              className="shrink-0 self-end rounded-lg bg-amber-500 px-4 py-2 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400 disabled:cursor-not-allowed disabled:opacity-50"
            >
              Odeslat
            </button>
          </div>
        </>
      )}
    </div>
  );
}
