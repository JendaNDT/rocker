"use client";

import { LoopPedal } from "@/components/looper/LoopPedal";
import { PageContainer } from "@/components/PageContainer";

export default function LooperPage() {
  return (
    <PageContainer size="focused">
      <h1 className="text-3xl font-bold tracking-tight">Looper</h1>
      <p className="mt-1 text-dim">
        Loop pedál — nahraj smyčku, ta se opakuje, a vrstvi přes ni další
        nápady.
      </p>
      <div className="mt-6">
        <LoopPedal />
      </div>
    </PageContainer>
  );
}
