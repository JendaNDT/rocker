# W31 — Native AI tutor, editable plan and statistical feedback

Views 23–25 use native controls and an HTTP streaming client, without an AI
WebView, file/system tools or audio upload. The common UI shows the exact optional
progress/instrument context. A request or account model list is sent only by an
explicit button. Cancellation, timeout, quota, permission, missing model, malformed
SSE, disconnected/incomplete stream and output limits produce distinct status;
no POST is automatically retried. Incomplete responses remain drafts and cannot
be saved as complete assistant messages or accepted plans.

**Provider change:** current [Gemini terms](sources.json) restrict consumer usage
and require paid services for API clients in the EEA/Switzerland/UK. The previous
free-Gemini assumption is not used for this private application. New profiles use
the OpenAI API with their own account/key; model discovery reads the actual account.
Costs, eligibility and quota are those of that account. No key, subscription or
credit has been purchased or used. The historical Gemini adapter is mock-tested
for compatibility but the personal-app UI blocks Gemini requests, preserves the
old model/history/key and explains switching. A legacy migration stays lossless.

OpenAI and historical Gemini credentials occupy independent DPAPI files with
separate entropy. The old key is never sent to a new provider. Keys remain absent
from exports, backups, projects, protocol messages and diagnostics. The HTTP client
uses pinned HTTPS origins, header credentials, bounded reads and redirects disabled
in the production handler. Model IDs cannot supply an endpoint or path traversal.

The conversation stores original nullable timestamps, new message timestamps,
selection deletion, confirmed history clearing and atomic native text export.
Only the last 40 messages / 64,000 characters enter a request; full local history
remains available rather than being silently discarded. The plan requests a strict
JSON schema, validates 3–6 blocks, durations, total time and exact existing native
tasks, and renders editable controls before acceptance. An accepted plan is an
optional field in the existing AI settings document; no database-kind/version
change or fabricated activity is needed. Feedback explicitly reports statistics
and user notes, not unsubmitted sound or playing accuracy.

Linux: 28 mock HTTP/stream/cancel/timeout/model/plan assertions, full infrastructure
checks and frozen domain checks passed; 105 C# files parse with no syntax errors.
Mock responses deliberately fragment Czech UTF-8 and cancel mid-stream. Windows
smoke now drives all three real native forms, edits and accepts a plan, rejects an
unknown task, and reopens SQLite to verify plan/history/timestamps. Independent
provider DPAPI checks are included in the Windows infrastructure suite. Windows
validation is pending. No real provider request, credential, online quota or
hardware/manual acceptance has been verified.
