"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";

/**
 * The multi-track studio merged into the unified "Nahrávání" screen
 * (screen-consolidation plan, fáze D). This route is kept only so cached or
 * bookmarked links to /studio do not dead-end — it redirects to /recorder.
 */
export default function StudioPage() {
  const router = useRouter();

  useEffect(() => {
    router.replace("/recorder");
  }, [router]);

  return (
    <p className="p-6 text-sm text-muted">Přesměrovávám na Nahrávání…</p>
  );
}
