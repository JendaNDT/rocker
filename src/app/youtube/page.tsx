"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";

/**
 * The YouTube practice player merged into the "Hraj se skladbou" tab of the
 * practice screen (screen-consolidation plan, fáze C). This route is kept
 * only so cached or bookmarked links to /youtube do not dead-end — it
 * redirects to /practice. Next.js static export has no server redirects,
 * so the redirect is client-side.
 */
export default function YoutubePage() {
  const router = useRouter();

  useEffect(() => {
    router.replace("/practice");
  }, [router]);

  return <p className="p-6 text-sm text-muted">Přesměrovávám na Cvičení…</p>;
}
