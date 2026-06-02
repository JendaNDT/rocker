/**
 * Single source of truth for the app's top-level screens.
 * Used by the navigation and the home page.
 */
export interface Screen {
  href: string;
  label: string;
  description: string;
}

export const SCREENS: Screen[] = [
  { href: "/", label: "Domů", description: "Denní plán a rychlý přehled." },
  {
    href: "/live",
    label: "Live",
    description: "Hraní s kytarou v ruce — ladička, metronom, nahrávání.",
  },
  { href: "/tuner", label: "Ladička", description: "Naladění kytary v centech." },
  { href: "/metronome", label: "Metronom", description: "Tempo, takty, akcenty." },
  { href: "/practice", label: "Cvičení", description: "Lekce a riff trainer." },
  { href: "/tutor", label: "AI tutor", description: "Zeptej se na cokoliv o hraní." },
  { href: "/games", label: "Hry", description: "Hmatníkové hry a ear training." },
  { href: "/metal", label: "Metal", description: "Trenažéry pro rock a metal." },
  { href: "/amp", label: "Aparát", description: "Efekty a simulace aparátu." },
  { href: "/library", label: "Knihovna", description: "Akordy, stupnice, riffy." },
  { href: "/notation", label: "Noty", description: "Prohlížeč tabulatur a not." },
  { href: "/recorder", label: "Nahrávání", description: "Nahrávky, Studio a Looper na jednom místě." },
  { href: "/profile", label: "Profil", description: "Nastavení a tvoje data." },
];
