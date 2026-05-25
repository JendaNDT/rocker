import { describe, expect, it } from "vitest";
import { buildInstrumentContext, buildPracticeContext } from "./context";

describe("buildPracticeContext", () => {
  it("reports an empty practice history", () => {
    const text = buildPracticeContext(
      {
        completedLessons: 0,
        totalLessons: 12,
        dailyMinutes: {},
        achievementCount: 0,
      },
      "2026-05-25",
    );
    expect(text).toContain("Hotové lekce: 0 z 12.");
    expect(text).toContain("nemá rozjetou sérii");
    expect(text).toContain("Zatím nemá žádné odcvičené minuty.");
    expect(text).toContain("Odemčené achievementy: 0.");
  });

  it("reports streak, totals and this week's minutes", () => {
    const text = buildPracticeContext(
      {
        completedLessons: 4,
        totalLessons: 12,
        dailyMinutes: {
          "2026-05-25": 20,
          "2026-05-24": 15,
          "2026-05-10": 30,
        },
        achievementCount: 3,
      },
      "2026-05-25",
    );
    expect(text).toContain("Hotové lekce: 4 z 12.");
    expect(text).toContain("Aktuální série cvičení: 2 dní v řadě.");
    expect(text).toContain(
      "Odcvičeno celkem 65 minut, z toho tento týden 35.",
    );
    expect(text).toContain("Odemčené achievementy: 3.");
  });

  it("rounds fractional minutes to whole numbers", () => {
    const text = buildPracticeContext(
      {
        completedLessons: 1,
        totalLessons: 12,
        dailyMinutes: { "2026-05-25": 12.4, "2026-05-24": 7.7 },
        achievementCount: 0,
      },
      "2026-05-25",
    );
    expect(text).toContain("Odcvičeno celkem 20 minut");
  });

  it("marks the block as hidden from the user", () => {
    const text = buildPracticeContext(
      {
        completedLessons: 1,
        totalLessons: 12,
        dailyMinutes: {},
        achievementCount: 0,
      },
      "2026-05-25",
    );
    expect(text).toContain("uživatel ho nevidí");
  });
});

describe("buildInstrumentContext", () => {
  it("describes a fully filled instrument profile", () => {
    const text = buildInstrumentContext(
      {
        name: "Solton Double Cut",
        bodyType: "Double cut, masivní tělo",
        pickups: [
          { position: "neck", type: "humbucker" },
          { position: "bridge", type: "humbucker" },
        ],
        scaleLength: '25,5"',
        stringGauge: ".010-.046",
        notes: "Nové struny.",
      },
      "Standardní (E A D G B E)",
    );
    expect(text).toContain("Nástroj: Solton Double Cut.");
    expect(text).toContain("Tělo: Double cut, masivní tělo.");
    expect(text).toContain("Ladění: Standardní (E A D G B E).");
    expect(text).toContain("Snímače: krk humbucker, kobylka humbucker.");
    expect(text).toContain('menzura 25,5", struny .010-.046.');
    expect(text).toContain("Poznámky uživatele: Nové struny.");
  });

  it("skips empty optional fields and reports no pickups", () => {
    const text = buildInstrumentContext(
      {
        name: "",
        bodyType: "",
        pickups: [],
        scaleLength: "",
        stringGauge: "",
        notes: "",
      },
      "Drop D (D A D G B E)",
    );
    expect(text).toContain("Nástroj: neuvedeno.");
    expect(text).toContain("Ladění: Drop D (D A D G B E).");
    expect(text).toContain("Snímače: neuvedené.");
    expect(text).not.toContain("Tělo:");
    expect(text).not.toContain("menzura");
    expect(text).not.toContain("Poznámky");
  });

  it("marks the block as hidden from the user", () => {
    const text = buildInstrumentContext(
      {
        name: "X",
        bodyType: "",
        pickups: [],
        scaleLength: "",
        stringGauge: "",
        notes: "",
      },
      "Standardní (E A D G B E)",
    );
    expect(text).toContain("uživatel ho nevidí");
  });
});
