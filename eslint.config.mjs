// ESLint flat config for Rocker (FIX_PLAN step 23).
//
// Purpose: pin down the two TypeScript safety-net habits the codebase
// already follows, so a future drift gets caught instead of slipping
// through. The audit confirmed zero `any` and zero `@ts-ignore` in
// production code; this just stops that from sliding back.
//
// Project-specific colour-token rule is intentionally NOT here yet.
// Adding it requires an exempt-file list (SVG fretboards, notation
// paper, polymorphic widgets, …) and naturally lives next to the
// remaining bg-accent/N → bg-accent-soft cleanup in FIX_PLAN step 25.
//
// The lint script is `eslint .` (Next.js 16 dropped the bundled
// `next lint`). Run with `npm run lint`.

import tsParser from "@typescript-eslint/parser";
import tsPlugin from "@typescript-eslint/eslint-plugin";

export default [
  {
    // Lint only source files; vitest output, .next/, out/ and
    // node_modules/ are ignored below.
    files: ["src/**/*.{ts,tsx}"],
    languageOptions: {
      parser: tsParser,
      parserOptions: {
        ecmaVersion: "latest",
        sourceType: "module",
        ecmaFeatures: { jsx: true },
      },
    },
    plugins: {
      "@typescript-eslint": tsPlugin,
    },
    rules: {
      // Audit confirmed 0 `any` in src/. Keep it that way — every escape
      // hatch must be a deliberate, named cast (`as unknown as Foo`) or a
      // generic, never a bare any.
      "@typescript-eslint/no-explicit-any": "error",

      // @ts-ignore silently hides the bug. @ts-expect-error at least
      // breaks the build the moment the underlying problem is fixed,
      // and only with a written description so reviewers know why.
      "@typescript-eslint/ban-ts-comment": [
        "error",
        {
          "ts-ignore": true,
          "ts-expect-error": "allow-with-description",
          "ts-nocheck": true,
          "ts-check": false,
          minimumDescriptionLength: 8,
        },
      ],
    },
  },
  {
    // Skip these wholesale — they are framework / generated artefacts,
    // not hand-written source.
    ignores: [
      ".next/**",
      "out/**",
      "node_modules/**",
      "scripts/**",
      "public/**",
    ],
  },
];
