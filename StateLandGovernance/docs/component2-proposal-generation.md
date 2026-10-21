# Component 2 proposal generation

The proposal screen calls the existing `GenerateLeaseProposalCommandHandler` through
`POST /api/lease-feasibility/proposals/generate`. It produces a full project document,
rather than claiming to optimize approved lease terms. The older `LeaseProposalDto`,
lease-term domain engine and historical-case repository are retained for future
term optimization; they are not implemented by this full-document workflow.

## Implemented

- Fixed 14-section schema transcribed from the applicant-supplied Department form,
  including initial / 15-year layouts, building areas, financial periods, schedule
  responsibilities and closing signature fields. Numbering duplication in the scan
  is normalized without omitting fields.
- Human-reviewed summaries from the two supplied scanned example proposals.
  Each entry has document, physical PDF page, section, purpose category and role.
  `source-manifest.json` records SHA-256 fingerprints of the supplied files.
  Summaries are not represented as verbatim OCR or verified approved cases.
- Small-corpus retrieval filters by section and coconut/poultry purpose; other
  purposes receive both relevant examples as writing references. It is section
  retrieval, not embedding similarity or historical outcome prediction.
- Applicant statements and model suggestions remain distinct. Unknown fields
  remain `[TO BE CONFIRMED]`. Presence checks do not establish content correctness.
- Nullable decimal budget calculations preserve unknown values and losses.
  Annual figures assume 12 identical operating months and are labelled accordingly.
- Optional user-confirmed personal financial inputs are recalculated using the
  existing scoring engine and appended separately from projected project income.
  Identity fields and this assessment are excluded from model context.
- Session-only editing, source / missing-field review, text export, actual DOCX
  export, and browser Print / Save PDF. Exported DOCX uses A4 and an installed
  Sinhala-capable font. Default is Sinhala MN (available on the local Mac); set
  `ProposalGeneration__DocumentFont` to an installed Sinhala-capable font such as
  Noto Sans Sinhala for other environments. Fonts are not embedded.
  Drafts are not persisted by the server or submitted to government services.

## Optional AI configuration

Set server-side environment variables (never frontend variables):

```
ProposalGeneration__ApiKey=<your private API key>
ProposalGeneration__Model=<a model available to your account supporting JSON output>
```

No model is silently selected. API credentials are not returned by the status
endpoint or committed. Restart the API after configuring. The UI enables explicit
AI drafting when both settings exist; this is configuration detection, not a
provider connectivity check. The model receives sections 2–14 and their retrieved
summaries through the Responses API, with `store=false`. Do not place personal
identifiers in narrative fields. This setting does not claim zero provider retention.

Official integration reference: https://developers.openai.com/api/docs/guides/text

Without configuration, structured drafting works and is labelled as having no LLM.
An explicit AI request fails rather than silently returning a substitute. Model
outputs must include sections 2–14; incomplete responses, duplicate sections and
recognized citations referring to unknown source IDs are rejected. Narrative
factual accuracy still requires human review; prompt instructions do not prove it.

## API

- `GET /api/lease-feasibility/proposals/requirements`: schema and configuration status.
- `POST /api/lease-feasibility/proposals/generate`: applicationId, projectName,
  purpose, facts keyed by schema, nullable budget, useAi, language (`en` or `si`),
  optional reviewedFinancials. English field labels remain in structured Sinhala
  drafts; Sinhala titles and optional Sinhala model narrative are supported.
- `POST /api/lease-feasibility/proposals/export/docx`: `{ "text": "edited draft" }`.

Run the existing local Component 2 API profile and frontend:

```
dotnet run --project StateLandGovernance/src/Api/StateLandGovernance.Api.csproj --launch-profile component2
cd StateLandGovernance.Web
npm run dev
```

Open `/component-02/proposal-generator`. The existing fictional historical-case
preview remains explicitly fictional and is not used by the real generation path.

## Remaining boundaries

No vector database, arbitrary PDF ingestion, authenticated persistent draft library,
verified government approval corpus, attachment-content verification, automatic
survey diagrams or complete translation of structured field labels is claimed.
The curated three-document corpus supports an initial implementation. Additional
sources should be reviewed, assigned roles and included with page provenance.

## Validation

Component 2 unit suite: 145 tests passed (12 new proposal tests). API build,
frontend TypeScript and targeted ESLint passed. Synthetic HTTP checks verified
14 sections, reference filtering, calculations, invalid-input rejection, explicit
unconfigured-AI failure and DOCX export. Browser generation, navigation, session
save and Word download were verified. Export rendering was inspected, including
Sinhala glyphs with a temporary fontconfig pointing to installed system fonts.
Live provider generation remains unverified until credentials/model are configured.

## Department form layout update

Word export and the inline printable preview now share a recreated 11-page Sinhala
Department form: personal fields, dotted writing lines, three diagram boxes,
materials / costs / sales / working capital / investment tables, monthly and annual
profit-and-loss rows, a 12-month responsibility grid and the signature/date/phone
block. The blank form and a small synthetic populated draft rendered to 11 pages.
Longer applicant content can expand the document; rows are not clipped to force
11 pages. The footer tracks actual Word page numbers.

`POST /export/docx` and `POST /export/preview` accept the saved draft text. The
14 numbered headings must remain. `blankForm: true` returns the blank template.
The preview endpoint returns encoded HTML; Component 2 shows it in a sandboxed
iframe with its own Print / Save PDF button, without requiring browser popups.

Known fields and derived totals populate the form. For itemized table data use
pipe-separated rows in displayed column order; non-tabular text remains as notes.
Unknown values become blank form spaces. Additional model narrative and personal
financial assessment details remain in an annexure. Source/review metadata remains
in the editor and text export rather than appearing in the Department form.
Diagram boxes hold attachment references; image upload/placement is not implemented.

Layout validation: 151 Component 2 tests passed, including 6 form-layout checks.
Final blank and filled Word exports were rendered and all 11 pages inspected.
TypeScript and targeted lint passed; the inline browser preview was verified.
