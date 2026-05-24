"use client";

import { useState } from "react";
import { summarizePractice } from "@/lib/ai/feedback";
import { streamChat } from "@/lib/ai/geminiClient";
import { FEEDBACK_SYSTEM_PROMPT } from "@/lib/ai/prompts";
import { STAGES } from "@/lib/lessons";
import { useAiStore } from "@/stores/aiStore";
import { useLessonProgressStore } from "@/stores/lessonProgressStore";
import { useProgressStore } from "@/stores/progressStore";

const TOTAL_LESSONS = STAGES.flatMap((stage) => stage.lessons).length;

/**
 * The "Zpětná vazba" panel of the AI tutor screen: sends a summary of the
 * learner's practice data (plus an optional note) to the AI and streams back
 * personal coaching feedback. Assumes an API key is set. Part of Bod 14.
 */
export function FeedbackPanel() {
  const apiKey = useAiStore((state) => state.apiKey);
  const model = useAiStore((state) => state.model);
  const completedLessonIds = useLessonProgressStore(
    (state) => state.completedLessonIds,
  );
  const dailyMinutes = useProgressStore((state) => state.dailyMinutes);
  const bpmRecords = useProgressStore((state) => state.bpmRecords);
  const unlockedAchievements = useProgressStore(
    (state) => state.unlockedAchievements,
  );

  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [feedback, setFeedback] = useState("");

  async function generate() {
    if (busy) return;
    setBusy(true);
    setError(null);
    setFeedback("");

    const summary = summarizePractice({
      completedLessons: completedLessonIds.length,
      totalLessons: TOTAL_LESSONS,
      dailyMinutes,
      bpmRecords,
      achievementCount: unlockedAchievements.length,
    });
    const trimmedNote = note.trim();
    const message = trimmedNote
      ? `${summary}\n\nJak se cítím a s čím bojuju: ${trimmedNote}`
      : summary;

    const result = await streamChat({
      apiKey,
      model,
      history: [{ role: "user", text: message }],
      systemPrompt: FEEDBACK_SYSTEM_PROMPT,
      onChunk: (text) => setFeedback(text),
    });
    setBusy(false);
    if (result.ok) {
      setFeedback(result.text);
    } else {
      setFeedback("");
      setError(result.error);
    }
  }

  return (
    <div>
      <div className="rounded-lg border border-neutral-800 bg-neutral-950 p-4">
        <p className="text-sm text-neutral-300">
          Projdu tvoje cvičební statistiky — lekce, série, odcvičený čas — a dám
          ti osobní zpětnou vazbu. Můžeš přidat pár slov o tom, jak se cítíš
          nebo s čím bojuješ.
        </p>

        <label
          htmlFor="feedback-note"
          className="mt-4 block text-sm font-medium text-neutral-300"
        >
          Jak se cítíš? S čím bojuješ? (nepovinné)
        </label>
        <textarea
          id="feedback-note"
          value={note}
          onChange={(event) => setNote(event.target.value)}
          rows={2}
          placeholder="např. nejde mi rytmus, nebo cvičím nepravidelně"
          className="mt-2 w-full resize-none rounded-lg border border-neutral-800 bg-neutral-900 px-3 py-2 text-sm text-neutral-100"
        />

        <button
          type="button"
          onClick={() => void generate()}
          disabled={busy}
          className="mt-4 rounded-lg bg-amber-500 px-4 py-2 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {busy
            ? "Připravuji…"
            : feedback
              ? "Získat znovu"
              : "Získej zpětnou vazbu"}
        </button>
      </div>

      {error && <p className="mt-3 text-sm text-red-400">{error}</p>}

      {busy && feedback.length === 0 && (
        <p className="mt-4 text-sm text-neutral-500">
          Procházím tvoje statistiky…
        </p>
      )}

      {feedback.length > 0 && (
        <div className="mt-4 rounded-lg border border-neutral-800 bg-neutral-900 px-4 py-3 text-sm whitespace-pre-wrap">
          {feedback}
        </div>
      )}
    </div>
  );
}
