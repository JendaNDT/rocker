// Deprecated location — moved to src/hooks/useHydrated.ts in FIX_PLAN step 20
// so the lib layer can be React-free. This file is a thin re-export kept only
// because the sandbox cannot delete files; once Jenda runs `git rm
// src/lib/useHydrated.ts` locally, the rest of the codebase already imports
// from the new path.

export { useHydrated } from "@/hooks/useHydrated";
