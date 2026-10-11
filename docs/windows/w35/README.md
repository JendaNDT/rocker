# W35 — Native files, commands and instance activation

Typed open handles audio, notation and portable project manifests from native
pickers, recent files, drag/drop and Explorer command-line activation. Only known
formats and fully qualified paths are accepted. Recent history is bounded,
case-insensitive, atomic and preserves Czech paths; clearing it does not delete
files. One current-user mutex/pipe channel redirects later launches to the existing
window and audio host. A corrupt/disconnected client cannot terminate that window.
The installer will register the portable project association in W41.

Ctrl+O, Ctrl+Shift+O and Ctrl+S have native menus. Space toggles the current
media/video/Looper transport outside editable and interactive controls; Ctrl+Z
returns defined Studio edits and preserves native text undo. Escape is left to
native dialogs. The footer has native playback/pause, stop, reveal/select in Explorer
and clipboard-path context actions. Project import is cancellable, guarded against
recording or competing writes, and preserves last-good manifests. No arbitrary
executable, external command or URL is launched from a dropped file.

Checks cover Czech recent paths, deduplication, cancellation, invalid activation,
current-user pipe UTF-8 forwarding/cancellation and real native recent/context menu.
Windows smoke additionally starts a second executable and checks primary-window
survival. Native picker, file association and physical transport interactions are
not claimed verified by the synthetic checks.
