# Manual-test corrections — upload feedback, sign-in return and complaint history

2026-10-23, Asia/Colombo. Corrective frontend changes are active through the existing Next development server. Ordinary storage, backend/package, permissions, models and business rules are unchanged. No ordinary upload, verification, complaint action, inference, migration, staging, commit or push occurred. Commissioner assessment execution was deliberately not implemented, as requested.

## Actual PDF and verification findings

**`Proposal1.pdf` does not currently exist in ordinary PostgreSQL. The attachment table contains zero rows.** There are no stored PDF bytes, size, hash or download to validate. This is not evidence of a corrupted stored file, and the earlier browser's exact lost/rejected request cannot be reconstructed from database absence alone.

The actual saved context is:

| Item | ID |
|---|---|
| Officer screening request | `64105595-9f53-499c-8276-e71daec4af6c` |
| Investigation | `fbb610b8-6200-42e1-829c-9f50653c4e9d` |
| Early assessment | `30909f90-2444-48fe-8770-c3ce0ca89fc9` |
| Existing Legal Dispute Verified event | `2e17ff49-c777-48c8-8e75-578b8d567084` |

That revision1 verification has **zero attachment IDs and one document reference**. Its UTC timestamp is `2026-10-23T16:55:37.712683Z`. A document reference satisfied the existing verification input contract; it did not upload a file. The event and original screening remain unchanged. No attachment was retroactively linked to it. Current responsible DS retrieval confirms the empty attachment list and that separate verification; Commissioner reads it without verification authority, while other DS/applicant/anonymous access is denied.

Full column metadata plus constraints/index definitions and privacy-minimized record correlation are in READ_ONLY_RECORD_INVESTIGATION.json and SCHEMA_AND_TRIAGE_CORRELATION.json. No PDF bytes, original complaint text, private remarks or credentials were published.

## Upload root cause and correction

Previously, one `pending` slot held either an upload FormData or a verification decision. Save dispatched whichever payload was already retained, irrespective of which Save button was clicked. An outstanding verification could therefore be replayed while the user believed they were uploading. Every error was marked uncertain before the request completed, including definite validation/permission rejections. A successful write followed by failed list refresh appeared to be a failed write. There was no success announcement, and the attachment list remained collapsed. The exact historical browser payload is unavailable; this identifies the code defect without claiming which branch that earlier browser took.

Uploads now use a separate, single-flight controller with explicit Empty, Ready, Validating, Uploading, Waiting for confirmation, Saved, Failed, Uncertain and Loaded-from-server states. It displays filename, bytes and concern. Fetch has no genuine upload-byte progress events, so the indicator is indeterminate: no percentage is simulated. A real successful HTTP response transitions to waiting while its acknowledgement body is parsed. Success requires confirmed metadata matching the request context, file size/name and concern. Only server-returned attachments enter the expanded list.

After confirmation, a read refresh updates count, filename, concern, bytes, time, recorded DS, attachment reference/hash and authorized download. If refresh fails, the UI says **Upload accepted; attachment list could not be refreshed**, retains that acknowledgement and offers a read refresh, not another upload. Success stays visible. Client size/type/signature checks and actual server rejection messages are distinguished from transport/5xx/unreadable-acknowledgement uncertainty. Definite failures retain the selected file for deliberate retry.

An uncertain upload retains the exact FormData, original File and key in memory. Refresh never writes; replay is explicit and uses that same object. A scoped session marker survives reload without storing file bytes. If the page reloads, it explains that the original payload is unavailable, disables replay/replacement and directs the officer to investigate saved history; it never silently creates a different key. Changing account/request invalidates old callbacks. Verification uses a separate retained payload and feedback, and cannot be mistaken for an upload. Important feedback uses live regions, visible text/spinners and focus; reduced-motion styling is included.

## Applicant redirect root cause and correction

The earlier correction fixed the header link on the complaints page, but **Sign out still navigated to plain `/sign-in`**. That discarded the complaint return. Clicking the header again on the sign-in page could also discard an existing valid query. Sign out now uses the exact complaints allowlist for its current pathname. The header preserves an already-valid complaint return on `/sign-in`, with a Suspense boundary and noninteractive loading fallback rather than a temporarily wrong link.

The login handler's applicant eligibility/return policy is unchanged. Plain sign-in keeps its existing default; Commissioner and DS routing is unchanged. Unsafe destinations remain rejected. Three repeated sign-in/session-read/sign-out cycles pass using isolated client/session fixtures. Actual SSR checks confirm both complaints-page links and the header on the queried sign-in page. Authenticated complaint reads still succeed. Real browser click-through/refresh remains manual; SSR is not a browser-login observation.

## Actual complaint findings

| Complaint | Saved handling state | Finding |
|---|---|---|
| `67a68a6a-4429-451b-8c14-50f639035e0b` | AssignedToDs, revision3 | AssignDs followed by two DsReviewRecorded mirrors. **No StartInvestigation event** exists for this complaint. |
| `aac71241-a1fd-4f4b-822d-5d5a1ca37b2c` | UnderInvestigation, revision2 | StartInvestigation **was persisted**, event `d32c26c8-edc2-41bf-8f83-a4204bb17a13`, UTC `2026-10-23T17:03:03.088257Z`, from AssignedToDs to UnderInvestigation. |

The two Under review entries for the first complaint are distinct stored triage reviews:

- `a9a6b26c-fb2a-4ef9-a91a-a4c9fe578838`, UTC `17:05:04.4340687Z`;
- `c74a1f28-8fa2-4203-9637-276e8738a8f7`, UTC `17:05:11.6899846Z`.

Their reasons hash identically. Each has its own matching DsReviewRecorded handling mirror, revisions2/3, whose internal submission key is that review ID. The review API itself uses expected complaint/handling revisions, not a caller submission-key contract. Stale repeats conflict; a new submission using the updated revision legitimately appends another review. These are two accepted writes, not one event rendered twice. Whether the officer intended both cannot be inferred. Nothing was deleted or merged.

Selecting Start Investigation alone does not submit it; recording Under review does not start investigation. The UI now explains that distinction, shows complaint IDs, retains pending/success/rejection/uncertain feedback and opens handling history after confirmed actions. It retains exact action keys/bodies for deliberate replay, with a safe unavailable-payload message after reload. A confirmed write followed by failed read refresh is distinguished from failure. History refresh preserves the panel instance instead of clearing/remounting it or keying it by every new revision; acknowledgement notices also survive in the parent.

Processing and triage history is separate from handling and decision history. Entries show identifiers, timestamps, applicable audit revisions and permitted actor references; another identical triage entry is explicitly labeled/warned before submission. Same event IDs are deduplicated only in presentation; distinct repeated reviews remain. Applicant handling data stays server-sanitized, and extra triage actor labels are officer-only.

**Read-contract limitation:** the existing history DTO returns each event's resulting status but omits stored FromState. The frontend derives the prior state only from an adjacent authorized revision and otherwise explicitly says it was not returned. It does not guess from revision1, because legacy assignments may precede the first handling event. Actual database transitions are recorded in the audit evidence. No backend contract/package was changed solely to add that display field.

## Verification and deployment

- **182 frontend tests pass**, zero failed/skipped, with **16 final focused checks** after the last presentation refinements. New tests exercise upload phases, acknowledgement/list-refresh separation, uncertain exact replay, no-file-after-reload behavior, double-click protection, validation, accessible feedback, distinct histories and repeated login cycles.
- **35 focused backend tests** and **7 architecture tests pass**, zero failed/skipped. Focused lint has **0 errors / 3 existing hook/ref warnings**. TypeScript still has only the three known C1 errors. No whole production frontend build is claimed.
- **45 isolated evidence HTTP/PostgreSQL checks pass**, including a generated fictional PDF, exact persisted bytea/SHA256 and download, list refresh/new-host retrieval, idempotency, concurrency, invalid input and denied foreign/Commissioner writes. Original isolated screening/verification behavior is preserved.
- **10 isolated handling HTTP checks pass** on a newly seeded fictional complaint: Start Investigation persistence/replay, changed-content conflict, stale triage conflict, separate repeat reviews, privacy/DS scope and new-host retrieval. **Zero model calls.**
- **32 ordinary authenticated read-only/SSR checks pass** for the actual evidence/complaints, current permissions and sign-in chain. These submit no ordinary business action.
- All persistent ordinary row hashes, schema/history, grants/settings/sequences match the fresh task baseline. Database remains **state_land_governance_c4 / OID34144 / 41 tables / 21 migrations**. Private settings/permission documents, published API hashes and both frozen model files match. Other sessions' temporary tables were excluded after PostgreSQL correctly refused to read them; none were modified.

Existing services were reused: API16456/5264, frontend16692/3000 and ML36612/8104. The API remains `.tmp/c4-history-form-api` through `Start-C4HistoryFormsApprovedApi.ps1`. Only frontend source is served anew; no API deployment/restart or migrations were needed. The isolated PostgreSQL container was started for tests and stopped afterward, retaining its storage. Ordinary services were not stopped.

The first test runs caught unsupported Node strip-only parameter-property syntax, ref reads during render and test-local C# naming/array mistakes; these were corrected and rerun. No tests were skipped or expected outputs forced. No engine, rule, threshold, worker, blockchain, account/password or Commissioner execution change occurred.

Browser inventory is empty. Desktop/mobile rendering, focus movement, real repeated login and actual upload interaction remain [manual retests](MANUAL_RETEST.md). Exact task files/hashes and a task-only patch are in CHANGED_FILES.json / TASK_DIFF.patch; pre-existing working-tree changes are not attributed to this task. No staging, commit or push.
