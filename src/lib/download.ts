// Browser-only file-download helper. Triggers a "save as" for a Blob via a
// temporary anchor element. Two details make this reliable across browsers:
//
//  - The anchor is appended to the document before clicking. Firefox ignores a
//    click on an anchor that is not in the DOM, so a detached <a> downloads
//    nothing.
//  - The object URL is revoked on a later tick, not synchronously. Revoking it
//    in the same frame as the click can cancel the download before it starts
//    in some browsers (notably for larger blobs).

/** Downloads a Blob as `filename`, prompting the browser's save flow. */
export function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  link.rel = "noopener";
  document.body.appendChild(link);
  link.click();
  link.remove();
  // Give the download time to start before releasing the URL. The leaked
  // reference is cleaned up here, or by the page unloading, whichever is first.
  window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
}
