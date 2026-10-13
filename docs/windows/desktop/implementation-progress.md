# Desktop implementation progress

This working log tracks implementation and evidence separately. The approved scope remains D01–D36; this is not a complete candidate or a request for intermediate user testing.

## Current integration

- Application services own the single native handle, database, autosave and media repositories. The main workspace no longer disposes a window-owned audio instance.
- Operations carry owner identity and remain visible until their lease completes. Cancellation is cooperative.
- Document state is independent of host window. Event handlers capture their initiating session, including across awaits. Project history/zoom, score rendering, training and AI response state have separate session storage.
- The native desktop shell hosts a library, document tabs, inspector, attached tools and shared transport. Document/tool detachment transfers the same UI into a real Windows window. Tool close hides its view.
- Score and video hosts are separate compiled WinUI controls. Pickers/dialogs resolve the initiating window.
- Independent native media inspection decodes actual PCM/waveforms without an audio handle. Opening a song or score does not publish it into the active media slot.
- Monitoring explanation and a real enable action sit beside the final mix meter.

## Verification

27 portable application/session contracts passed locally. The native recording contract includes independent inspection, unchanged host counters and decode cancellation. C# semantic checks use real API references and stubbed XAML fields; they do not substitute for the Windows compiler/runtime.

The new Windows runtime suite exercises two project states, duplicate activation, actual score decoding and XAML-root transfer, tool singleton/hide/reopen, independent close and unchanged audio counters. The first actual Windows build, 38447931873 at fd9ccef, compiled successfully and passed the pre-existing domain/storage/native and legacy UI checks. Its new desktop scenario failed because the single-bar fixture could not select bar 2; no claim of passing desktop acceptance is made for that run. The test now uses the multi-bar GP fixture and preserves the actual validated selection.

Layout storage has seven additional passing portable checks: Unicode/canonical identities, negative monitor coordinates, hidden/attached tool state, canceled atomic replacement, duplicate tools, malformed/future layouts and recovery. Practice/exit ownership and owner-bound serialized dialogs are integrated. Full acceptance remains pending.

## Remaining integration

Layout persistence, all document-specific source/command routes, operation/exit barriers, capture ownership, full layout compositions, accessibility and broad desktop acceptance still need implementation/validation. D05–D36 are not declared complete by this log.

## Commands, compositions and cancellation

The portable service suite now has 36 checks, including failed/canceled save, captured command ownership and actual adopted native media tickets. All 21 Linux native suites passed; the two relevant recording/transport suites passed again after the media-identity ABI addition. TypeScript and all 567 web tests passed, and lint passed.

Windows run 38450045555 compiled and passed the older checks; the new tool reopen scenario exposed that `Window.Close()` forcibly destroys a WinUI window rather than simulating its title-bar close. The scenario now sends the real WM_CLOSE message and the registry also removes unexpectedly destroyed window hosts. No passing desktop runtime claim is attached to that failed run.

Current integration adds captured async invocation scopes, owner-bound commands, menus/status in auxiliary windows, Studio undo/redo and a bounded timeline composition, fullscreen Live, adopted-source identity verification, and profile restoration barriers. Final desktop compositions/coverage, layout refinements, migration and installer handoff remain in progress.
