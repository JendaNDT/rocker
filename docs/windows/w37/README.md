# W37 — Real browser-to-native migration

A disposable Chromium 154 profile ran the actual PWA on localhost, loaded all 11
persisted groups and five authored legacy recordings in the real IndexedDB store,
then clicked the actual export button. A second download ran the bundled bookmark
helper through a real `javascript:` location. Both imported into separate clean
native SQLite profiles, preserving all bytes/hashes, channels/encoded files,
metadata/times/markers, custom tuning, presets, video loops and AI history.
The explicit synthetic credential was redacted. No personal profile was read and
no production PWA/main deployment was changed.

The native package includes a local HTML helper. The user drags its export bookmark
to the existing browser's bookmark bar, opens their original Rocker origin/profile
and explicitly exports there. The helper is built only from the project's original
MIT archive/backup/recording/download code, with native read-only IndexedDB access;
it has no network, evaluation, deletion or provider calls. Opening the local helper
alone cannot read another origin's storage. Current browser restrictions may reject
a bookmark; that case uses the existing exporter or old JSON plus separate audio.
No generic protection bypass is suggested.

Both browser archives are [fixtures](fixtures/) included in Windows automation.
The two imports passed 13 assertions each: 17 typed documents, all five recordings,
reopened data, idempotent repeated merge and cancelled-import preservation. Existing
28 migration and 20 backup assertions cover older/empty/future/corrupt/missing-media,
malicious paths, scopes/conflicts and interrupted recoverable transactions.
Native preview reports imported/retained records and conversion warnings. Missing
recordings in an old JSON backup are explicit; volatile unsaved Studio/Looper state
is never claimed migrated. Browser source data remained 11 groups/five recordings.

See [browser evidence](browser-validation.json), [UI import](ui-import-validation.json)
and [bookmark import](bookmark-import-validation.json). Reproduce with a local PWA
server and `scripts/verify-windows-browser-migration.mjs`, providing installed
Playwright/Chromium paths; that script uses an isolated browser and blocks nonlocal
requests. Manual migration of the brother's actual profile and physical playback
remain deferred, not passed.
