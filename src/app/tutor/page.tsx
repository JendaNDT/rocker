"use client";

import Link from "next/link";
import { useState } from "react";
import { ChatPanel } from "@/components/tutor/ChatPanel";
import { FeedbackPanel } from "@/components/tutor/FeedbackPanel";
import { PlanPanel } from "@/components/tutor/PlanPanel";
import { PageContainer } from "@/components/PageContainer";
import { useHydrated } from "@/lib/useHydrated";
import { useAiStore } from "@/stores/aiStore";

/** Which AI tutor tool is shown. */
type Mode = "chat" | "plan" | "feedback";

const TAB_ACTIVE =
  "rounded-md bg-surface-2 px-3 py-1.5 text-sm text-accent";
const TAB_IDLE =
  "rounded-md px-3 py-1.5 text-sm text-dim transition-colors hover:text-text";

export default function TutorPage() {
  const hydrated = useHydrated();
  const apiKey = useAiStore((state) => state.apiKey);
  const [mode, setMode] = useState<Mode>("chat");

  const hasKey = apiKey.trim().length > 0;

  return (
    <PageContainer size="focused">
      <h1 className="text-3xl font-bold tracking-tight">AI tutor</h1>
      <p className="mt-1 text-dim">
        Zeptej se na hraní, nebo si nech sestavit cvičební plán.
      </p>

      {!hydrated ? (
        <p className="mt-6 text-sm text-muted">Načítám…</p>
      ) : !hasKey ? (
        <div className="mt-6 rounded-lg border border-line bg-surface p-4">
          <p className="text-sm text-text">
            AI tutor potřebuje vlastní klíč Google Gemini — vyzvedneš si ho
            zdarma. Nastav ho v Profilu v sekci „AI tutor", pak se sem vrať.
          </p>
          <Link
            href="/profile"
            className="mt-4 inline-block rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90"
          >
            Otevřít nastavení
          </Link>
        </div>
      ) : (
        <>
          <div className="mt-6 inline-flex rounded-lg border border-line p-1">
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
    </PageContainer>
  );
}
