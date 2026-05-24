"use client";

import { LoopPedal } from "@/components/looper/LoopPedal";

export default function LooperPage() {
  return (
    <div className="mx-auto max-w-3xl p-6">
      <h1 className="text-3xl font-bold tracking-tight">Looper</h1>
      <p className="mt-1 text-neutral-400">
        Loop pedál — nahraj smyčku, ta se opakuje, a vrstvi přes ni další
        nápady.
      </p>
      <div className="mt-6">
        <LoopPedal />
      </div>
    </div>
  );
}
