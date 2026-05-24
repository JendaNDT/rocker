import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Static export — the whole app is client-side, no backend (see ARCHITECTURE.md).
  output: "export",
  // Static export cannot use the Next.js image optimizer.
  images: {
    unoptimized: true,
  },
};

export default nextConfig;
