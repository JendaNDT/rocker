// Lesson content for the practice path. Pure data + types.
// Chord steps reference chord ids from src/lib/chords.ts.

export type LessonStepKind = "text" | "chord" | "note";

interface BaseStep {
  title: string;
  body: string;
}

export interface TextStep extends BaseStep {
  kind: "text";
}

export interface ChordStep extends BaseStep {
  kind: "chord";
  chordId: string;
}

export interface NoteStep extends BaseStep {
  kind: "note";
  /** MIDI note the learner should play; verified via pitch detection. */
  targetMidi: number;
  targetLabel: string;
}

export type LessonStep = TextStep | ChordStep | NoteStep;

export interface Lesson {
  id: string;
  title: string;
  estimatedMinutes: number;
  steps: LessonStep[];
}

export interface Stage {
  id: string;
  title: string;
  description: string;
  lessons: Lesson[];
}

export const STAGES: Stage[] = [
  {
    id: "stage-1",
    title: "Stage 1 — Začínáme",
    description: "Prázdné struny, otevřené akordy a první rytmika.",
    lessons: [
      {
        id: "drzeni",
        title: "Držení kytary a trsátka",
        estimatedMinutes: 4,
        steps: [
          {
            kind: "text",
            title: "Posaď se rovně",
            body: "Sedni si rovně, kytaru opři o pravé stehno a tělo lehce přitiskni k hrudníku. Krk kytary míří mírně vzhůru. Nehrb se — záda i ramena uvolni.",
          },
          {
            kind: "text",
            title: "Jak držet trsátko",
            body: "Trsátko polož na bříško ukazováčku a shora ho přidrž palcem. Vyčnívat má jen malý kousek hrotu. Drž ho pevně, ale ne křečovitě.",
          },
          {
            kind: "text",
            title: "Levá ruka",
            body: "Palec levé ruky je vzadu za krkem, zhruba proti prostředníčku. Prsty tlač na struny špičkami, těsně za pražcem — ne přímo na něj.",
          },
        ],
      },
      {
        id: "prazdne-struny",
        title: "Prázdné struny",
        estimatedMinutes: 6,
        steps: [
          {
            kind: "text",
            title: "Šest strun",
            body: "Kytara má šest strun. Nejsilnější je spodní E, nejtenčí horní e. Teď si zkusíš zahrát tři z nich naprázdno — bez tlačení prstů.",
          },
          {
            kind: "note",
            title: "Spodní struna E",
            body: "Zahraj nejsilnější (nejvyšší) strunu naprázdno. Aplikace poslouchá a potvrdí správný tón.",
            targetMidi: 40,
            targetLabel: "E (6. struna)",
          },
          {
            kind: "note",
            title: "Struna D",
            body: "Zahraj naprázdno čtvrtou strunu odshora — strunu D.",
            targetMidi: 50,
            targetLabel: "D (4. struna)",
          },
          {
            kind: "note",
            title: "Horní struna e",
            body: "Zahraj nejtenčí strunu naprázdno — horní e.",
            targetMidi: 64,
            targetLabel: "e (1. struna)",
          },
        ],
      },
      {
        id: "akord-em",
        title: "První akord: Em",
        estimatedMinutes: 7,
        steps: [
          {
            kind: "text",
            title: "Proč zrovna Em",
            body: "Em je nejjednodušší akord — potřebuješ jen dva prsty a zní plně. Je skvělý start pro rock i metal.",
          },
          {
            kind: "chord",
            title: "Zahraj Em",
            body: "Polož prsty podle diagramu a pomalu přejeď přes všechny struny. Každý tón má znít čistě. Když to drhne, přitlač víc a zkontroluj, že se prsty nedotýkají sousedních strun.",
            chordId: "Em",
          },
        ],
      },
      {
        id: "akord-e",
        title: "Akord E",
        estimatedMinutes: 7,
        steps: [
          {
            kind: "text",
            title: "Z Em do E",
            body: "Akord E je Em plus jeden prst navíc. Přidáš prst na třetí strunu a získáš jasnější, durový zvuk.",
          },
          {
            kind: "chord",
            title: "Zahraj E",
            body: "Drž tvar z diagramu a brnkni přes všech šest strun. Zkus přepínat mezi Em a E — to je tvůj první přechod mezi akordy.",
            chordId: "E",
          },
        ],
      },
      {
        id: "akordy-am-a",
        title: "Akordy Am a A",
        estimatedMinutes: 9,
        steps: [
          {
            kind: "text",
            title: "Druhá dvojice",
            body: "Am a A fungují podobně jako Em a E, jen začínají na páté struně. Spodní E strunu v nich nehraješ.",
          },
          {
            kind: "chord",
            title: "Zahraj Am",
            body: "Tři prsty podle diagramu. Brnkni od páté struny dolů — spodní E vynech.",
            chordId: "Am",
          },
          {
            kind: "chord",
            title: "Zahraj A",
            body: "Všechny tři prsty jsou na druhém pražci vedle sebe. Je to stísněné — dej prstům čas si na to zvyknout.",
            chordId: "A",
          },
        ],
      },
      {
        id: "prechody",
        title: "Přechody mezi akordy",
        estimatedMinutes: 10,
        steps: [
          {
            kind: "text",
            title: "Pomalu a čistě",
            body: "Přechod mezi akordy je o svalové paměti. Cvič pomalu — radši čistě a beze spěchu než rychle a zmateně. Rychlost přijde sama.",
          },
          {
            kind: "chord",
            title: "Akord Em",
            body: "Postav Em a nech ho znít.",
            chordId: "Em",
          },
          {
            kind: "chord",
            title: "Přejdi na E",
            body: "Plynule přidej prst a přejdi na E. Pak zpátky na Em. Opakuj klidně desetkrát v pomalém tempu.",
            chordId: "E",
          },
        ],
      },
      {
        id: "akordy-d-g",
        title: "Akordy D a G",
        estimatedMinutes: 10,
        steps: [
          {
            kind: "text",
            title: "Poslední dvojice Stage 1",
            body: "D a G ti spolu s předchozími akordy otevřou stovky písní. G se zpočátku zdá těžký — chce roztažené prsty.",
          },
          {
            kind: "chord",
            title: "Zahraj D",
            body: "D se hraje od čtvrté struny. Tři prsty tvoří malý trojúhelník.",
            chordId: "D",
          },
          {
            kind: "chord",
            title: "Zahraj G",
            body: "G používá všechny struny a roztažené prsty. Dej tomu čas, je to běh na delší trať.",
            chordId: "G",
          },
        ],
      },
      {
        id: "strumming",
        title: "Strumming — rytmika pravé ruky",
        estimatedMinutes: 7,
        steps: [
          {
            kind: "text",
            title: "Pohyb z lokte a zápěstí",
            body: "Brnkání vychází z uvolněného zápěstí, ne z prstů. Pohyb je plynulý shora dolů přes struny. Zapni si metronom na pomalé tempo a hraj jeden úder dolů na každou dobu.",
          },
          {
            kind: "text",
            title: "Údery dolů i nahoru",
            body: "Až ti jdou údery dolů, přidej mezi ně údery nahoru. Ruka se hýbe pořád stejně, jen občas struny zasáhne i při cestě zpět. Drž tempo, ne sílu.",
          },
        ],
      },
    ],
  },
  {
    id: "stage-2",
    title: "Stage 2 — Power chordy a palm mute",
    description: "Stavební kameny rockových a metalových riffů.",
    lessons: [
      {
        id: "power-chord",
        title: "Co je power chord",
        estimatedMinutes: 7,
        steps: [
          {
            kind: "text",
            title: "Dva tóny, plný zvuk",
            body: "Power chord není dur ani moll — jsou to jen dva nebo tři tóny. Proto zní skvěle se zkreslením a je základem rocku a metalu. Značí se číslem 5, třeba E5.",
          },
          {
            kind: "chord",
            title: "Zahraj E5",
            body: "E5 zahraješ na spodních strunách. Brnkni jen struny z diagramu — ostatní nech být.",
            chordId: "E5",
          },
        ],
      },
      {
        id: "power-a5-d5",
        title: "Power chordy A5 a D5",
        estimatedMinutes: 8,
        steps: [
          {
            kind: "text",
            title: "Stejný princip, jiná struna",
            body: "A5 a D5 fungují jako E5, jen začínají na jiné struně. Pravá ruka musí trefit jen ty správné struny — to chce cvik.",
          },
          {
            kind: "chord",
            title: "Zahraj A5",
            body: "Power chord s kořenem na páté struně.",
            chordId: "A5",
          },
          {
            kind: "chord",
            title: "Zahraj D5",
            body: "Power chord s kořenem na čtvrté struně.",
            chordId: "D5",
          },
        ],
      },
      {
        id: "pohyblivy-power",
        title: "Pohyblivý power chord",
        estimatedMinutes: 10,
        steps: [
          {
            kind: "text",
            title: "Jeden tvar, celý krk",
            body: "Power chord má pořád stejný tvar — stačí ho posunout po krku a získáš jiný akord. Když umíš F5, umíš i G5, A5 a další; jen posuneš ruku.",
          },
          {
            kind: "chord",
            title: "Zahraj F5",
            body: "Tvar power chordu na prvním pražci. Ukazováček je kořen.",
            chordId: "F5",
          },
          {
            kind: "chord",
            title: "Posuň na G5",
            body: "Stejný tvar posunutý o dva pražce výš je G5. Zkus mezi F5 a G5 přepínat.",
            chordId: "G5",
          },
        ],
      },
      {
        id: "palm-mute",
        title: "Palm mute",
        estimatedMinutes: 8,
        steps: [
          {
            kind: "text",
            title: "Tlumení hranou dlaně",
            body: "Palm mute je tlumený, dunivý zvuk typický pro metal. Polož hranu pravé dlaně lehce na struny těsně u kobylky a brnkni. Struny mají znít přidušeně, ne úplně hluše.",
          },
          {
            kind: "text",
            title: "Cvič na power chordu",
            body: "Zahraj E5 s palm mutem a pravidelně opakuj jeden tón — to je základ metalového rytmu. Zapni metronom a drž stálé tempo. Síla je v pravidelnosti, ne v rychlosti.",
          },
        ],
      },
    ],
  },
];

export function getStage(id: string): Stage | undefined {
  return STAGES.find((stage) => stage.id === id);
}

export function getLesson(id: string): Lesson | undefined {
  for (const stage of STAGES) {
    const lesson = stage.lessons.find((item) => item.id === id);
    if (lesson) return lesson;
  }
  return undefined;
}
