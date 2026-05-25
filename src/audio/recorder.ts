// Recorder — architecture layer 5 (audio). Wraps MediaRecorder to capture a
// take from the live input stream. Browser-only; MediaRecorder is referenced
// inside methods only, never at import time, so SSR / static export is safe.
// One recording runs at a time, hence a singleton.

/** The result of a finished recording. */
export interface RecordingResult {
  blob: Blob;
  /** Wall-clock length of the take in seconds. */
  durationSec: number;
  /** MIME type of the blob. */
  mimeType: string;
}

// Preferred container/codec order. Opus in WebM is the best supported on the
// target platforms (desktop / Android Chrome); the rest are fallbacks.
const MIME_CANDIDATES = [
  "audio/webm;codecs=opus",
  "audio/webm",
  "audio/ogg;codecs=opus",
  "audio/mp4",
];

class Recorder {
  private mediaRecorder: MediaRecorder | null = null;
  private chunks: Blob[] = [];
  private startTime = 0;

  /** True if this browser can record audio at all. */
  isSupported(): boolean {
    return typeof MediaRecorder !== "undefined";
  }

  /** True while a take is being captured. */
  isRecording(): boolean {
    return this.mediaRecorder?.state === "recording";
  }

  /** Picks the best supported MIME type, or "" to let the browser decide. */
  private pickMimeType(): string {
    if (!this.isSupported()) return "";
    for (const candidate of MIME_CANDIDATES) {
      if (MediaRecorder.isTypeSupported(candidate)) return candidate;
    }
    return "";
  }

  /**
   * Starts recording from the given input stream. Returns false if recording
   * is unsupported or one is already running.
   */
  start(stream: MediaStream): boolean {
    if (!this.isSupported() || this.isRecording()) return false;

    const mimeType = this.pickMimeType();
    const mediaRecorder = mimeType
      ? new MediaRecorder(stream, { mimeType })
      : new MediaRecorder(stream);

    this.chunks = [];
    mediaRecorder.ondataavailable = (event) => {
      if (event.data.size > 0) this.chunks.push(event.data);
    };
    // If recording fails (e.g. the input track ends), drop the recorder so
    // isRecording() turns false and a later stop() returns null instead of
    // hanging forever on an onstop that will never fire.
    mediaRecorder.onerror = () => {
      this.mediaRecorder = null;
      this.chunks = [];
    };

    mediaRecorder.start();
    this.mediaRecorder = mediaRecorder;
    this.startTime = performance.now();
    return true;
  }

  /**
   * Stops the current recording and resolves with the captured take, or null
   * if nothing was recording. Duration is measured wall-clock (start to stop)
   * because WebM/Opus blobs often lack a duration header.
   */
  stop(): Promise<RecordingResult | null> {
    const mediaRecorder = this.mediaRecorder;
    if (!mediaRecorder || mediaRecorder.state === "inactive") {
      return Promise.resolve(null);
    }

    return new Promise((resolve) => {
      let settled = false;
      const finish = (result: RecordingResult | null) => {
        if (settled) return;
        settled = true;
        this.mediaRecorder = null;
        this.chunks = [];
        resolve(result);
      };
      mediaRecorder.onstop = () => {
        const durationSec = (performance.now() - this.startTime) / 1000;
        const mimeType = mediaRecorder.mimeType || "audio/webm";
        const blob = new Blob(this.chunks, { type: mimeType });
        finish({ blob, durationSec, mimeType });
      };
      // If the recorder errors instead of stopping cleanly, resolve with null
      // so the caller (loop pedal / multitrack) never hangs on the await.
      mediaRecorder.onerror = () => finish(null);
      try {
        mediaRecorder.stop();
      } catch {
        finish(null);
      }
    });
  }
}

/** Shared singleton instance. */
export const recorder = new Recorder();
