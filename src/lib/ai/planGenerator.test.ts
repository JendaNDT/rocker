import { afterEach, describe, expect, it, vi } from "vitest";
import { generatePlan, sanitizeGeneratedPlan } from "./planGenerator";

const API_BASE = "https://gemini.test/v1beta";

const BASE_OPTIONS = {
  apiKey: "key-123",
  model: "gemini-2.5-flash",
  minutes: 15,
  completedLessons: 4,
  totalLessons: 12,
  streakDays: 3,
  apiBase: API_BASE,
};

/** A fake Response carrying the given JSON body. */
function fakeResponse(body: unknown, init?: { ok?: boolean; status?: number }) {
  return {
    ok: init?.ok ?? true,
    status: init?.status ?? 200,
    json: async () => body,
  } as unknown as Response;
}

/** A Gemini JSON-mode response whose reply text is the given object. */
function geminiJson(obj: unknown) {
  return {
    candidates: [{ content: { parts: [{ text: JSON.stringify(obj) }] } }],
  };
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("sanitizeGeneratedPlan", () => {
  it("parses a valid plan and maps screens to routes", () => {
    const plan = sanitizeGeneratedPlan({
      items: [
        { title: "Naladění", detail: "Nalaď kytaru.", minutes: 2, screen: "tuner" },
        { title: "Riff", detail: "Zahraj riff.", minutes: 8, screen: "practice" },
      ],
    });
    expect(plan).toEqual({
      items: [
        { title: "Naladění", detail: "Nalaď kytaru.", minutes: 2, href: "/tuner" },
        { title: "Riff", detail: "Zahraj riff.", minutes: 8, href: "/practice" },
      ],
      totalMinutes: 10,
    });
  });

  it("sets href to null for an unknown screen", () => {
    const plan = sanitizeGeneratedPlan({
      items: [{ title: "Strečink", detail: "Protáhni prsty.", minutes: 3, screen: "xyz" }],
    });
    expect(plan?.items[0].href).toBeNull();
  });

  it("drops malformed items but keeps the valid ones", () => {
    const plan = sanitizeGeneratedPlan({
      items: [
        { title: "OK", detail: "Platný blok.", minutes: 5, screen: "metal" },
        { title: "Chybný", minutes: 4, screen: "metal" },
        { title: "Záporný", detail: "x", minutes: -2, screen: "metal" },
      ],
    });
    expect(plan?.items).toHaveLength(1);
    expect(plan?.items[0].title).toBe("OK");
  });

  it("returns null when there are no usable items", () => {
    expect(sanitizeGeneratedPlan({ items: [] })).toBeNull();
    expect(sanitizeGeneratedPlan({ items: [{ title: "x" }] })).toBeNull();
    expect(sanitizeGeneratedPlan({ nope: true })).toBeNull();
    expect(sanitizeGeneratedPlan("garbage")).toBeNull();
  });
});

describe("generatePlan", () => {
  it("returns a structured plan on success", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        fakeResponse(
          geminiJson({
            items: [
              { title: "Naladění", detail: "Nalaď kytaru.", minutes: 3, screen: "tuner" },
              { title: "Rozehřátí", detail: "Chromatika.", minutes: 12, screen: "metronome" },
            ],
          }),
        ),
      ),
    );

    const result = await generatePlan(BASE_OPTIONS);

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.plan.totalMinutes).toBe(15);
      expect(result.plan.items[0].href).toBe("/tuner");
    }
  });

  it("sends a JSON-mode request with a response schema", async () => {
    const fetchMock = vi.fn(async () =>
      fakeResponse(
        geminiJson({
          items: [{ title: "A", detail: "B", minutes: 15, screen: "practice" }],
        }),
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    await generatePlan({ ...BASE_OPTIONS, focus: "palm mute" });

    const [, init] = fetchMock.mock.calls[0] as unknown as [
      string,
      RequestInit,
    ];
    const body = JSON.parse(init.body as string);
    expect(body.generationConfig.responseMimeType).toBe("application/json");
    expect(body.generationConfig.responseSchema).toBeDefined();
    expect(body.contents[0].parts[0].text).toContain("palm mute");
  });

  it("reports a malformed JSON reply", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        fakeResponse({
          candidates: [{ content: { parts: [{ text: "{not json" }] } }],
        }),
      ),
    );

    const result = await generatePlan(BASE_OPTIONS);
    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error).toContain("Plán");
  });

  it("reports a reply with no usable items", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => fakeResponse(geminiJson({ items: [] }))),
    );

    const result = await generatePlan(BASE_OPTIONS);
    expect(result.ok).toBe(false);
  });

  it("maps a 400 response to a bad-key message", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => fakeResponse({}, { ok: false, status: 400 })),
    );

    const result = await generatePlan(BASE_OPTIONS);
    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error).toContain("klíč");
  });

  it("reports a missing API key without calling the network", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    const result = await generatePlan({ ...BASE_OPTIONS, apiKey: "  " });
    expect(result.ok).toBe(false);
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
