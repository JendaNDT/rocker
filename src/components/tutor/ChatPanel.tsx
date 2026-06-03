"use client";

import { useEffect, useRef, useState } from "react";
import { buildInstrumentContext, buildPracticeContext } from "@/lib/ai/context";
import { streamChat } from "@/lib/ai/geminiClient";
import { SUGGESTED_QUESTIONS, TUTOR_SYSTEM_PROMPT } from "@/lib/ai/prompts";
import type { ChatMessage } from "@/lib/ai/types";
import { STAGES } from "@/lib/lessons";
import { resolveTuning } from "@/lib/resolveTuning";
import { useAiStore } from "@/stores/aiStore";
import { useChatStore } from "@/stores/chatStore";
import { useCustomTuningsStore } from "@/stores/customTuningsStore";
import { useInstrumentStore } from "@/stores/instrumentStore";
import { useLessonProgressStore } from "@/stores/lessonProgressStore";
import { useProgressStore } from "@/stores/progressStore";
import { useSettingsStore } from "@/stores/settingsStore";

const TOTAL_LESSONS = STAGES.flatMap((stage) => stage.lessons).length;

/**
 * The "Zeptat se" chat panel of the AI tutor screen. Assumes an API key is
 * set — the parent screen gates on that. Part of Bod 14. The chat's system
 * prompt carries short practice- and instrument-context blocks so answers fit
 * the learner and their guitar (feature-interconnection plan, A2/B2).
 */
export function ChatPanel() {
  const apiKey = useAiStore((state) => state.apiKey);
  const model = useAiStore((state) => state.model);

  // Practice and instrument data — feed the context blocks appended to the
  // system prompt.
  const completedLessonIds = useLessonProgressStore(
    (state) => state.completedLessonIds,
  );
  const dailyMinutes = useProgressStore((state) => state.dailyMinutes);
  const unlockedAchievements = useProgressStore(
    (state) => state.unlockedAchievements,
  );
  const profile = useInstrumentStore((state) => state.profile);
  const tuningId = useSettingsStore((state) => state.tuningId);
  const customTunings = useCustomTuningsStore((state) => state.tunings);

  const messages = useChatStore((state) => state.messages);
  const setMessages = useChatStore((state) => state.setMessages);
  const addMessage = useChatStore((state) => state.addMessage);
  const clearChat = useChatStore((state) => state.clear);

  const [input, setInput] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // The reply currently streaming in — empty until the first chunk arrives.
  const [streaming, setStreaming] = useState("");
  // Index of the most recently copied assistant message. Reset after 2 s so
  // the "Zkopírováno" feedback fades back to "Kopírovat".
  const [copiedIndex, setCopiedIndex] = useState<number | null>(null);

  const endRef = useRef<HTMLDivElement>(null);
  const mountedRef = useRef(true);
  // Aborts the in-flight stream on unmount so an abandoned conversation
  // does not keep eating tokens from the user's BYOK quota.
  const abortControllerRef = useRef<AbortController | null>(null);

  // Track mount state so an in-flight stream can't update an unmounted panel.
  useEffect(() => {
    return () => {
      mountedRef.current = false;
      abortControllerRef.current?.abort();
    };
  }, []);

  // Keep the latest message in view as the conversation grows.
  useEffect(() => {
    endRef.current?.scrollIntoView({ block: "nearest" });
  }, [messages, busy, streaming]);

  /** Streams a reply from the AI for the given history and appends it. */
  async function ask(history: ChatMessage[]) {
    // Cancel any in-flight stream from a previous ask() (e.g. fast retry).
    abortControllerRef.current?.abort();
    const controller = new AbortController();
    abortControllerRef.current = controller;

    setBusy(true);
    setError(null);
    setStreaming("");
    // Rebuilt per request so they reflect the learner's current state.
    const practiceContext = buildPracticeContext({
      completedLessons: completedLessonIds.length,
      totalLessons: TOTAL_LESSONS,
      dailyMinutes,
      achievementCount: unlockedAchievements.length,
    });
    const instrumentContext = buildInstrumentContext(
      profile,
      resolveTuning(tuningId, customTunings).label,
    );
    const result = await streamChat({
      apiKey,
      model,
      history,
      signal: controller.signal,
      systemPrompt: `${TUTOR_SYSTEM_PROMPT}\n\n${practiceContext}\n\n${instrumentContext}`,
      onChunk: (text) => {
        if (mountedRef.current) setStreaming(text);
      },
    });
    // Clear our ref only if this request is still the latest one — a newer
    // ask() may have replaced it while we were awaiting.
    if (abortControllerRef.current === controller) {
      abortControllerRef.current = null;
    }
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

  /** Copies an assistant reply into the clipboard with brief visual feedback. */
  function copyReply(index: number, text: string): void {
    if (typeof navigator === "undefined" || !navigator.clipboard) return;
    void navigator.clipboard.writeText(text).then(() => {
      setCopiedIndex(index);
      window.setTimeout(() => {
        // Only reset if this is still the current copy — a later copy on
        // another reply shouldn't get its feedback shortened.
        setCopiedIndex((current) => (current === index ? null : current));
      }, 2000);
    });
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

        {messages.map((message, index) => {
          const isModel = message.role === "model";
          return (
            <div
              key={index}
              className={isModel ? "mr-auto max-w-[85%]" : "ml-auto max-w-[85%]"}
            >
              <div
                className={
                  isModel
                    ? "rounded-lg border border-line bg-surface px-3 py-2 text-sm whitespace-pre-wrap"
                    : "rounded-lg border border-accent/40 bg-accent-soft px-3 py-2 text-sm whitespace-pre-wrap"
                }
              >
                {message.text}
              </div>
              {isModel && (
                <button
                  type="button"
                  onClick={() => copyReply(index, message.text)}
                  className="mt-1 text-xs text-dim transition-colors hover:text-text"
                >
                  {copiedIndex === index ? "Zkopírováno" : "Kopírovat"}
                </button>
              )}
            </div>
          );
        })}

        {busy && streaming.length > 0 && (
          <div className="mr-auto max-w-[85%] rounded-lg border border-line bg-surface px-3 py-2 text-sm whitespace-pre-wrap">
            {streaming}
          </div>
        )}

        {busy && streaming.length === 0 && (
          <div
            role="status"
            aria-live="polite"
            className="mr-auto max-w-[85%] rounded-lg border border-line bg-surface px-3 py-2 text-sm text-muted"
          >
            Přemýšlí…
          </div>
        )}

        {error && (
          <div className="mr-auto max-w-[85%]" role="alert">
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

      {/* On narrow screens (< 640 px) the textarea sits above the button so a
          rows=2 + paddings don't overflow the 320 px iPhone width. On sm+
          the original side-by-side layout is back. */}
      <div className="mt-3 flex flex-col gap-2 sm:flex-row">
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
          className="min-h-12 self-end rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90 disabled:cursor-not-allowed disabled:opacity-50 sm:shrink-0"
        >
          Odeslat
        </button>
      </div>
    </div>
  );
}
