# W30 — YouTube practice with native controls

View 08 retains the official YouTube iframe only for video. Native WinUI owns
URL validation, explicit loading/playback, position seeking, A/B marks, numeric
range entry, approximate looping, available rates and the saved-loop list.
Users save/name/rename/delete/undo a range and reopen it without autoplay. The
original `yt-loops` records remain compatible; newly offered rates are accepted
up to the bridge's 16× bound instead of assuming only the historical 2× options.
Unavailable stored rates leave the observed current rate and explain the change.
An out-of-duration range is rejected without discarding the saved record.

The browser checks A/B on its 250 ms progress observation, including natural
video end. Paused playback is never forced to seek/play. The host does not claim
sample-accurate synchronization; YouTube audio is not downloaded or recorded.
Generation tokens and browser callback epochs prevent the previous video's
late readiness/error/progress from changing the replacement video's state.
Leaving the page pauses playback; returning does not automatically resume.

The existing isolated WebView2 profile, host-object denial, permission denial,
CSP, exact message origin and restricted top-level navigation remain in effect.
Missing runtime, offline/API timeout, deleted/private/disallowed embeds and
process failure show actionable status while other workspaces remain usable.
The shared-audio button stops native capture/transport through the device
coordinator, selects the real shared `Windows Audio` backend when present and
asks the user to select/confirm the interface. It never silently enables a mic
or changes the Windows default device. ASIO/YouTube coexistence is not claimed.

Local checks: eight browser-bridge tests, 41 protocol/practice assertions, frozen
domain checks (69 + 372 cases, 350 rhythm/palm/break states, 136 additional
assertions), TypeScript types and 97 C# syntax files passed. Added actual WinUI
smoke drives the native save/rename/delete/undo controls and reopens SQLite to
verify records. Windows form validation is pending. Online video, audible output,
hardware coexistence and physical keyboard acceptance remain unperformed.
