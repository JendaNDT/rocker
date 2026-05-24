"use client";

import { useState } from "react";
import { AI_MODELS } from "@/lib/ai/config";
import { sendChat } from "@/lib/ai/geminiClient";
import { useHydrated } from "@/lib/useHydrated";
import { useAiStore } from "@/stores/aiStore";

/** State of the "test connection" check. */
type TestState =
  | { state: "idle" }
  | { state: "testing" }
  | { state: "ok" }
  | { state: "error"; message: string };

const PRIMARY_BUTTON =
  "rounded-lg bg-amber-500 px-4 py-2 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400 disabled:cursor-not-allowed disabled:opacity-50";
const SECONDARY_BUTTON =
  "rounded-lg border border-neutral-800 bg-neutral-950 px-4 py-2 text-sm transition-colors hover:border-neutral-700";

/**
 * AI tutor settings: the user's own Gemini API key (BYOK), model picker and
 * a connection test. Lives on the Profile screen. Part of Bod 14.
 */
export function AiSettings() {
  const hydrated = useHydrated();
  const apiKey = useAiStore((state) => state.apiKey);
  const setApiKey = useAiStore((state) => state.setApiKey);
  const model = useAiStore((state) => state.model);
  const setModel = useAiStore((state) => state.setModel);

  const [showKey, setShowKey] = useState(false);
  const [test, setTest] = useState<TestState>({ state: "idle" });

  // Before hydration the prerendered HTML uses the defaults.
  const keyValue = hydrated ? apiKey : "";
  const modelValue = hydrated ? model : AI_MODELS[0].id;
  const hasKey = keyValue.trim().length > 0;
  const modelNote = AI_MODELS.find((m) => m.id === modelValue)?.note ?? "";

  function changeKey(value: string) {
    setApiKey(value);
    // A new key invalidates any earlier test result.
    setTest({ state: "idle" });
  }

  function clearKey() {
    setApiKey("");
    setTest({ state: "idle" });
  }

  async function runTest() {
    setTest({ state: "testing" });
    const result = await sendChat({
      apiKey,
      model,
      history: [{ role: "user", text: "Odpověz jen slovem: ahoj." }],
    });
    setTest(
      result.ok
        ? { state: "ok" }
        : { state: "error", message: result.error },
    );
  }

  return (
    <div className="rounded-lg border border-neutral-800 bg-neutral-900 p-4">
      <p className="text-sm text-neutral-300">
        AI tutor odpovídá na otázky o hraní pomocí Google Gemini. Funguje na
        vlastní API klíč — vyzvedneš si ho zdarma, bez platební karty, v{" "}
        <a
          href="https://aistudio.google.com/apikey"
          target="_blank"
          rel="noreferrer"
          className="text-amber-500 underline hover:text-amber-400"
        >
          Google AI Studiu
        </a>
        . Klíč i model se ukládají jen v tomto prohlížeči.
      </p>

      <label
        htmlFor="ai-api-key"
        className="mt-4 block text-sm font-medium text-neutral-300"
      >
        Gemini API klíč
      </label>
      <div className="mt-2 flex gap-2">
        <input
          id="ai-api-key"
          type={showKey ? "text" : "password"}
          value={keyValue}
          disabled={!hydrated}
          onChange={(event) => changeKey(event.target.value)}
          placeholder="Vlož sem svůj klíč"
          autoComplete="off"
          spellCheck={false}
          className="w-full rounded-lg border border-neutral-800 bg-neutral-950 px-3 py-2 text-sm text-neutral-100"
        />
        <button
          type="button"
          onClick={() => setShowKey((value) => !value)}
          className={SECONDARY_BUTTON}
        >
          {showKey ? "Skrýt" : "Zobrazit"}
        </button>
      </div>

      <label
        htmlFor="ai-model"
        className="mt-4 block text-sm font-medium text-neutral-300"
      >
        Model
      </label>
      <select
        id="ai-model"
        value={modelValue}
        disabled={!hydrated}
        onChange={(event) => setModel(event.target.value)}
        className="mt-2 w-full rounded-lg border border-neutral-800 bg-neutral-950 px-3 py-2 text-sm text-neutral-100"
      >
        {AI_MODELS.map((aiModel) => (
          <option key={aiModel.id} value={aiModel.id}>
            {aiModel.label}
          </option>
        ))}
      </select>
      {modelNote && (
        <p className="mt-2 text-xs text-neutral-500">{modelNote}</p>
      )}

      <div className="mt-4 flex flex-wrap items-center gap-3">
        <button
          type="button"
          onClick={() => void runTest()}
          disabled={!hasKey || test.state === "testing"}
          className={PRIMARY_BUTTON}
        >
          {test.state === "testing" ? "Testuji…" : "Otestovat spojení"}
        </button>
        {hasKey && (
          <button type="button" onClick={clearKey} className={SECONDARY_BUTTON}>
            Smazat klíč
          </button>
        )}
      </div>

      {test.state === "ok" && (
        <p className="mt-3 text-sm text-emerald-400">
          Spojení funguje — AI tutor je připravený.
        </p>
      )}
      {test.state === "error" && (
        <p className="mt-3 text-sm text-red-400">{test.message}</p>
      )}

      <p className="mt-4 text-xs text-neutral-500">
        Tvoje dotazy a odpovědi se posílají na servery Google. U bezplatného
        tarifu je Google může použít ke zlepšování svých modelů. Zbytek
        aplikace funguje i bez AI a nic neodesílá.
      </p>
    </div>
  );
}
