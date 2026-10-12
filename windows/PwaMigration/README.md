# Explicit browser exporter (MIT)

Build pinned helper: `npm exec --yes --package=esbuild@0.25.12 -- esbuild windows/PwaMigration/exporter.ts --bundle --minify --platform=browser --format=iife --target=chrome120 --outfile=windows/PwaMigration/exporter.js`.
The local helper page embeds that IIFE in its bookmark link. It reads the original
Rocker origin only when explicitly run there. No script is downloaded at runtime.
Original MIT source helpers and corresponding license are included separately in
the application/source notices. Browser/native integration evidence: docs/windows/w37.
