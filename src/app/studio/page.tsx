"use client";

import { Studio } from "@/components/studio/Studio";

export default function StudioPage() {
  return (
    <div className="mx-auto max-w-3xl p-6">
      <h1 className="text-3xl font-bold tracking-tight">Studio</h1>
      <p className="mt-1 text-neutral-400">
        Nahrávej stopy přes sebe — vrstvi podklad, sólo a další nápady.
      </p>
      <div className="mt-6">
        <Studio />
      </div>
    </div>
  );
}
