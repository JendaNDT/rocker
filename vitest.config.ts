import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

// Lets test files use the "@/" path alias just like the app does.
export default defineConfig({
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
});
