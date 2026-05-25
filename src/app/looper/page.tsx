"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";

/**
 * The looper merged into the unified "Nahrávání" screen (screen-consolidation
 * plan, fáze D). This route is kept only so cached or bookmarked links to
 * /looper do not dead-end — it redirects to /recorder.
 */
export default function LooperPage() {
  const router = useRouter();

  useEffect(() => {
    router.replace("/recorder");
  }, [router]);

  return (
    <p className="p-6 text-sm text-muted">Přesměrovávám na Nahrávání…</p>
  );
}
