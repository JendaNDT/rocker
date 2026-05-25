"use client";

import { AmpStudio } from "@/components/amp/AmpStudio";
import { PageContainer } from "@/components/PageContainer";

export default function AmpPage() {
  return (
    <PageContainer>
      <h1 className="text-3xl font-bold tracking-tight">Aparát</h1>
      <p className="mt-1 text-dim">
        Efekty a simulace aparátu — zatím první ověřovací verze se zkreslením.
      </p>
      <div className="mt-6">
        <AmpStudio />
      </div>
    </PageContainer>
  );
}
