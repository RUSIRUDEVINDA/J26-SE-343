# Component 2 document review

Run the financial API independently of the other modules during development:

```sh
cd /Users/ravindusandun/Documents/Research/J26-SE-343/StateLandGovernance
dotnet run --project src/Api/StateLandGovernance.Api.csproj --launch-profile component2
```

In a second terminal:

```sh
cd /Users/ravindusandun/Documents/Research/J26-SE-343/StateLandGovernance.Web
npm run dev
```

The frontend uses `NEXT_PUBLIC_API_BASE_URL=http://localhost:5264` in `.env.local`.
Azure credentials remain on the server in the repository-root `.env`, using
`Azure__DocumentIntelligence__Endpoint` and `Azure__DocumentIntelligence__Key`.
Restart the frontend after changing its API base URL.

Select documents, open Document Processing, and click **Upload and extract documents**.
The API receives actual file bytes and calls Azure Document Intelligence. It never
uses the synthetic URI fixture path. Read the original OCR text and tables in
Extracted Data. The backend reads the PDF page count and imports pages into batches
of at most two pages, then sends each batch to Azure sequentially. For a 13-page PDF,
these are 1-2, 3-4, 5-6, 7-8, 9-10, 11-12 and 13. Text and tables are combined in
original order, with original page numbers retained for tables. Two-page batches
above 4 MB are split further into single pages; an individual page above 4 MB must
be compressed before upload. PNG/JPEG uploads are one-page requests, limited to 4 MB.

PDF uploads remain limited to 10 MB and, by default, 200 pages. Configure the server
setting `Azure__DocumentIntelligence__MaximumUploadPages` to change the page limit.
The backend retries transient OCR failures and missing-page responses up to three
times per batch, respecting cancellation. A failed batch does not discard successful
batches: partial evidence remains viewable, but no financial fields are mapped from
an incomplete document. The UI shows processed/total pages and batch failures, and
blocks confirmation and assessment until all selected documents complete extraction.
Retry processing to resubmit incomplete documents only; completed documents are retained
in the unchanged browser session. Retrying an incomplete document reprocesses its
successful batches too. Azure requests are serialized in the API process and separated
by a two-second cooldown. Accepted operations are polled at a minimum two-second
interval without resubmitting their documents. Temporary 429 errors honour Retry-After
and exponential backoff; exhausted quota or long cooldowns stop remaining batches
with an actionable message instead of repeating every request.
Completeness proves page coverage, not OCR accuracy or financial interpretation.

Explicit, unambiguous summary labels can populate financial fields automatically.
Ordinary transaction tables are displayed for review; credits are not automatically
classified as income. Unsupported summaries remain blank. CRIB outstanding principal
is never converted into a monthly installment. Repayment amounts and default history
require explicit supporting evidence or human verification. Enter the requested lease
payment separately. Replacing, removing, or reprocessing files clears the prior profile
and assessment. Editing fields invalidates confirmation and the prior score.

Confirm all reviewed inputs, then open Assessment and calculate the advisory result.
The endpoint uses the existing Component 2 V1 domain scorer, including the 60% payment
capacity rule. Missing inputs, XX/missing CRIB grades, or unknown default history prevent
scoring rather than defaulting to a favourable result. There is no assessment persistence
or submission in this flow. Files and extracted results are held in memory for the session.

The `component2` launch profile is development-only and exposes only the financial
document and assessment controllers. The normal launch profiles retain their existing
module registrations and database requirements. Prediction, historical cases and
proposal generation remain labelled design previews.

## Table field suggestions

Use **Fill fields from extracted tables** to map already-read document evidence without
calling Azure again for complete documents. Each suggested field shows its derivation.
Supported six-month statements must have DATE/PARTICULARS/CREDITS/BALANCE columns,
six consecutive complete calendar months, and identifiable opening/closing records.
Recognized salary/wage/pension credits produce a suggested average over all six months;
transfers, refunds and loan credits are excluded. Other income sources need review.
Average account balance uses daily closing balances carried through non-transaction days.
Overdrafts are suggested as separate negative daily closing-balance episodes and require
confirmation against the reviewed evidence rule. Income consistency remains manual until
an approved upstream calculation is supplied. The requested lease payment is applicant input.

The current CRIB Score/Risk Grade/Probability of Default table supplies the bureau subgrade.
Active OWN/JNT facilities with explicit LKR currency, MNLY repayment frequency and clear
installment amounts can supply a summed repayment suggestion. Missing or misread required
facility cells prevent a partial debt total. Default history remains unknown unless an
explicit reviewed value is supplied. All suggestions require original-document verification.

Transport failures (including Azure status-zero exceptions and connection-reset IO errors)
are retried without displaying a fictitious HTTP 0 status. Failures while polling an
accepted operation retry that same operation, avoiding another document submission.
Persistent connection failures stop later batches and return incomplete evidence with
a readable network message; previously completed documents remain available for retry.

## Upload validation

The upload screen obtains limits from the API and validates each selected file before
adding it to the session. PDFs allow up to the configured page limit (default 200)
and 10 MB; PNG/JPEG files allow 4 MB. Invalid/empty files, mismatched extensions,
corrupt or locked PDFs, too many pages, and oversized individual PDF pages are
rejected before OCR. Valid files show their page count and file size. Validation sends
the selected file to the server for inspection but makes no Azure requests. Extraction
repeats validation to enforce the same rules even if browser checks are bypassed.
