"use client";

import { NotationViewer } from "@/components/notation/NotationViewer";

export default function NotationPage() {
  return (
    <div className="mx-auto max-w-3xl p-6">
      <h1 className="text-3xl font-bold tracking-tight">Noty</h1>
      <p className="mt-1 text-dim">
        Nahraj svůj Guitar Pro nebo MusicXML soubor — zobrazí se tabulatura
        i notový zápis.
      </p>
      <div className="mt-6">
        <NotationViewer />
      </div>
    </div>
  );
}
