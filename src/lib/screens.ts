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
  { href: "/tuner", label: "Ladička", description: "Naladění kytary v centech." },
  { href: "/metronome", label: "Metronom", description: "Tempo, takty, akcenty." },
  { href: "/practice", label: "Cvičení", description: "Lekce a riff trainer." },
  { href: "/tutor", label: "AI tutor", description: "Zeptej se na cokoliv o hraní." },
  { href: "/metal", label: "Metal", description: "Trenažéry pro rock a metal." },
  { href: "/amp", label: "Aparát", description: "Efekty a simulace aparátu." },
  { href: "/library", label: "Knihovna", description: "Akordy, stupnice, riffy." },
  { href: "/notation", label: "Noty", description: "Prohlížeč tabulatur a not." },
  { href: "/youtube", label: "YouTube", description: "Cvičení podle videa se smyčkou." },
  { href: "/recorder", label: "Nahrávání", description: "Nahraj a porovnej si hru." },
  { href: "/live", label: "Live", description: "Hraní bez rušení." },
  { href: "/profile", label: "Profil", description: "Nastavení a tvoje data." },
];
