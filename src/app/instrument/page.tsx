"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";

/**
 * The instrument profile moved into the Profil screen (screen-consolidation
 * plan, fáze A). This route is kept only so cached or bookmarked links to
 * /instrument do not dead-end — it redirects to /profile. Next.js static
 * export has no server redirects, so the redirect is client-side.
 */
export default function InstrumentPage() {
  const router = useRouter();

  useEffect(() => {
    router.replace("/profile");
  }, [router]);

  return <p className="p-6 text-sm text-muted">Přesměrovávám na Profil…</p>;
}
