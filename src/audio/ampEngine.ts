// This file is obsolete. The persistent AmpEngine singleton was rolled back
// because Next.js `output: 'export'` performs a full document reload on
// every Link navigation, which tears down the AudioContext anyway and
// makes a "live across screens" aparát physically impossible without
// switching off static export.
//
// AmpStudio (on /amp) and AmpWidget (on /practice and /metal) now own
// their own short-lived AmpChain instance via useLocalAmpChain. Shared
// settings live in useAmpStore.
//
// Run `git rm src/audio/ampEngine.ts` on the Mac to clean this up.
export {};
