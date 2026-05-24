// YouTube URL helpers — pure, unit-tested in youtube.test.ts.

const VIDEO_ID = /^[A-Za-z0-9_-]{11}$/;
const PATH_PREFIXES = ["embed", "shorts", "live"];

/**
 * Extracts an 11-character YouTube video id from a URL or a bare id.
 * Supports youtube.com/watch?v=, youtu.be/, /embed/, /shorts/, /live/.
 * Returns null when no valid id can be found.
 */
export function extractVideoId(input: string): string | null {
  const trimmed = input.trim();
  if (VIDEO_ID.test(trimmed)) return trimmed;

  let url: URL;
  try {
    url = new URL(trimmed);
  } catch {
    return null;
  }

  const host = url.hostname.replace(/^www\./, "");

  if (host === "youtu.be") {
    const id = url.pathname.slice(1).split("/")[0];
    return VIDEO_ID.test(id) ? id : null;
  }

  if (
    host === "youtube.com" ||
    host === "m.youtube.com" ||
    host === "music.youtube.com"
  ) {
    const fromQuery = url.searchParams.get("v");
    if (fromQuery && VIDEO_ID.test(fromQuery)) return fromQuery;

    const segments = url.pathname.split("/").filter(Boolean);
    if (segments.length >= 2 && PATH_PREFIXES.includes(segments[0])) {
      return VIDEO_ID.test(segments[1]) ? segments[1] : null;
    }
  }

  return null;
}
