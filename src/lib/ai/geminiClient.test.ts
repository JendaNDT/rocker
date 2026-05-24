import { afterEach, describe, expect, it, vi } from "vitest";
import { sendChat, streamChat } from "./geminiClient";
import type { ChatMessage } from "./types";

const API_BASE = "https://gemini.test/v1beta";
const HISTORY: ChatMessage[] = [{ role: "user", text: "Jak naladit kytaru?" }];

/** Builds a minimal fake Response with the fields geminiClient reads. */
function fakeResponse(body: unknown, init?: { ok?: boolean; status?: number }) {
  return {
    ok: init?.ok ?? true,
    status: init?.status ?? 200,
    json: async () => body,
  } as unknown as Response;
}

/** A Gemini success body carrying the given reply text. */
function geminiReply(text: string) {
  return { candidates: [{ content: { parts: [{ text }] } }] };
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("sendChat", () => {
  it("returns the reply text on success", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => fakeResponse(geminiReply("Použij ladičku."))),
    );

    const result = await sendChat({
      apiKey: "key-123",
      model: "gemini-2.5-flash",
      history: HISTORY,
      apiBase: API_BASE,
    });

    expect(result).toEqual({ ok: true, text: "Použij ladičku." });
  });

  it("calls the model endpoint with the key header and a system-instructed body", async () => {
    const fetchMock = vi.fn(async () => fakeResponse(geminiReply("ok")));
    vi.stubGlobal("fetch", fetchMock);

    await sendChat({
      apiKey: "  key-123  ",
      model: "gemini-2.5-flash",
      history: HISTORY,
      apiBase: API_BASE,
    });

    expect(fetchMock).toHaveBeenCalledOnce();
    const [url, init] = fetchMock.mock.calls[0] as unknown as [
      string,
      RequestInit,
    ];
    expect(url).toBe(`${API_BASE}/models/gemini-2.5-flash:generateContent`);
    expect(init.method).toBe("POST");

    const headers = init.headers as Record<string, string>;
    expect(headers["x-goog-api-key"]).toBe("key-123"); // trimmed

    const body = JSON.parse(init.body as string);
    expect(body.systemInstruction.parts[0].text).toContain("kytarový");
    expect(body.contents).toEqual([
      { role: "user", parts: [{ text: "Jak naladit kytaru?" }] },
    ]);
  });

  it("reports a missing API key without calling the network", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    const result = await sendChat({
      apiKey: "   ",
      model: "gemini-2.5-flash",
      history: HISTORY,
      apiBase: API_BASE,
    });

    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error).toContain("API klíč");
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("maps a 400 response to a bad-key message", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => fakeResponse({}, { ok: false, status: 400 })),
    );

    const result = await sendChat({
      apiKey: "key-123",
      model: "gemini-2.5-flash",
      history: HISTORY,
      apiBase: API_BASE,
    });

    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error).toContain("klíč");
  });

  it("maps a 429 response to a rate-limit message", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => fakeResponse({}, { ok: false, status: 429 })),
    );

    const result = await sendChat({
      apiKey: "key-123",
      model: "gemini-2.5-flash",
      history: HISTORY,
      apiBase: API_BASE,
    });

    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error).toContain("limit");
  });

  it("maps a 500 response to a server-trouble message", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => fakeResponse({}, { ok: false, status: 500 })),
    );

    const result = await sendChat({
      apiKey: "key-123",
      model: "gemini-2.5-flash",
      history: HISTORY,
      apiBase: API_BASE,
    });

    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error).toContain("Server");
  });

  it("reports a network failure when fetch throws", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => {
        throw new TypeError("network down");
      }),
    );

    const result = await sendChat({
      apiKey: "key-123",
      model: "gemini-2.5-flash",
      history: HISTORY,
      apiBase: API_BASE,
    });

    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error).toContain("připojení");
  });

  it("reports a blocked prompt", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        fakeResponse({ promptFeedback: { blockReason: "SAFETY" } }),
      ),
    );

    const result = await sendChat({
      apiKey: "key-123",
      model: "gemini-2.5-flash",
      history: HISTORY,
      apiBase: API_BASE,
    });

    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error).toContain("filtr");
  });

  it("reports an empty answer when no candidate text is returned", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => fakeResponse({ candidates: [] })),
    );

    const result = await sendChat({
      apiKey: "key-123",
      model: "gemini-2.5-flash",
      history: HISTORY,
      apiBase: API_BASE,
    });

    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error).toContain("odpověď");
  });
});

/** One Gemini SSE event carrying the given text chunk. */
function sseEvent(text: string): string {
  const body = { candidates: [{ content: { parts: [{ text }] } }] };
  return `data: ${JSON.stringify(body)}\n\n`;
}

/** A fake streaming Response whose body yields the given raw chunks. */
function sseResponse(
  chunks: string[],
  init?: { ok?: boolean; status?: number },
): Response {
  const encoder = new TextEncoder();
  const stream = new ReadableStream<Uint8Array>({
    start(controller) {
      for (const chunk of chunks) controller.enqueue(encoder.encode(chunk));
      controller.close();
    },
  });
  return {
    ok: init?.ok ?? true,
    status: init?.status ?? 200,
    body: stream,
  } as unknown as Response;
}

describe("streamChat", () => {
  it("accumulates text from multiple SSE events", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        sseResponse([sseEvent("Nalaď "), sseEvent("kytaru.")]),
      ),
    );

    const result = await streamChat({
      apiKey: "key-123",
      model: "gemini-2.5-flash",
      history: HISTORY,
      apiBase: API_BASE,
      onChunk: () => {},
    });

    expect(result).toEqual({ ok: true, text: "Nalaď kytaru." });
  });

  it("calls onChunk with the growing cumulative text", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        sseResponse([sseEvent("Nalaď "), sseEvent("kytaru.")]),
      ),
    );

    const chunks: string[] = [];
    await streamChat({
      apiKey: "key-123",
      model: "gemini-2.5-flash",
      history: HISTORY,
      apiBase: API_BASE,
      onChunk: (text) => chunks.push(text),
    });

    expect(chunks).toEqual(["Nalaď ", "Nalaď kytaru."]);
  });

  it("handles an SSE event split across two stream chunks", async () => {
    const event = sseEvent("Odpověď");
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        sseResponse([event.slice(0, 12), event.slice(12)]),
      ),
    );

    const result = await streamChat({
      apiKey: "key-123",
      model: "gemini-2.5-flash",
      history: HISTORY,
      apiBase: API_BASE,
      onChunk: () => {},
    });

    expect(result).toEqual({ ok: true, text: "Odpověď" });
  });

  it("maps a 400 response to a bad-key message", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => sseResponse([], { ok: false, status: 400 })),
    );

    const result = await streamChat({
      apiKey: "key-123",
      model: "gemini-2.5-flash",
      history: HISTORY,
      apiBase: API_BASE,
      onChunk: () => {},
    });

    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error).toContain("klíč");
  });

  it("reports an empty answer when the stream carries no text", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => sseResponse([])),
    );

    const result = await streamChat({
      apiKey: "key-123",
      model: "gemini-2.5-flash",
      history: HISTORY,
      apiBase: API_BASE,
      onChunk: () => {},
    });

    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error).toContain("odpověď");
  });

  it("reports a missing API key without calling the network", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    const result = await streamChat({
      apiKey: "   ",
      model: "gemini-2.5-flash",
      history: HISTORY,
      apiBase: API_BASE,
      onChunk: () => {},
    });

    expect(result.ok).toBe(false);
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
