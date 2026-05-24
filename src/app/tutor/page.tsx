"use client";

import Link from "next/link";
import { useState } from "react";
import { ChatPanel } from "@/components/tutor/ChatPanel";
import { FeedbackPanel } from "@/components/tutor/FeedbackPanel";
import { PlanPanel } from "@/components/tutor/PlanPanel";
import { useHydrated } from "@/lib/useHydrated";
import { useAiStore } from "@/stores/aiStore";

/** Which AI tutor tool is shown. */
type Mode = "chat" | "plan" | "feedback";

const TAB_ACTIVE =
  "rounded-md bg-neutral-800 px-3 py-1.5 text-sm text-amber-500";
const TAB_IDLE =
  "rounded-md px-3 py-1.5 text-sm text-neutral-400 transition-colors hover:text-neutral-100";

export default function TutorPage() {
  const hydrated = useHydrated();
  const apiKey = useAiStore((state) => state.apiKey);
  const [mode, setMode] = useState<Mode>("chat");

  const hasKey = apiKey.trim().length > 0;

  return (
    <div className="mx-auto max-w-3xl p-6">
      <h1 className="text-3xl font-bold tracking-tight">AI tutor</h1>
      <p className="mt-1 text-neutral-400">
        Zeptej se na hraní, nebo si nech sestavit cvičební plán.
      </p>

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
          <div className="mt-6 inline-flex rounded-lg border border-neutral-800 p-1">
            <button
              type="button"
              onClick={() => setMode("chat")}
              className={mode === "chat" ? TAB_ACTIVE : TAB_IDLE}
            >
              Zeptat se
            </button>
            <button
              type="button"
              onClick={() => setMode("plan")}
              className={mode === "plan" ? TAB_ACTIVE : TAB_IDLE}
            >
              Plán cvičení
            </button>
            <button
              type="button"
              onClick={() => setMode("feedback")}
              className={mode === "feedback" ? TAB_ACTIVE : TAB_IDLE}
            >
              Zpětná vazba
            </button>
          </div>

          <div className="mt-4">
            {mode === "chat" ? (
              <ChatPanel />
            ) : mode === "plan" ? (
              <PlanPanel />
            ) : (
              <FeedbackPanel />
            )}
          </div>
        </>
      )}
    </div>
  );
}
