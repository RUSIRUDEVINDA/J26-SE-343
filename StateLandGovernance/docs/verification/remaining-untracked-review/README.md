# Remaining untracked files - manual review, 2026-10-20

Inspected 133 untracked originals. Existing local JSON, evidence, PDFs, screenshots, tool source and application behavior were preserved. Only sanitized examples, comparison summaries and review documentation were prepared. No seeding, migration, runtime provisioning, staging or commit was executed.

## Privacy and runtime findings

- The account file contains nine local profiles, salted PBKDF2 password verifiers, actor/owner bindings, scoped assignments and trusted office/coordinator directory entries. These are not Git material; values are neither printed nor copied. Password hashes/salts are authentication material even for public test passwords.
- The spatial file contains seven synthetic-context rows, including local case bindings. It contains no credential fields or source-document bytes, but its binding data makes the whole working file unsuitable for Git. The new example contains only the two reserved synthetic contexts reused by the uploaded-case wrapper.
- No plaintext password, API key or signed JWT was found in the reviewed tooling sources. This is a static review, not a universal assurance. The acceptance browser scripts contain literal ISOLATED-TEST bearer markers deliberately recognized only by the isolated handler.
- ProjectWorkflowProvisioner reads the existing local database configuration and account file, is restricted to a named loopback Development database, and performs writes when explicitly invoked. Legacy research mode deliberately creates fixture candidates, reviews/snapshot and completeness under distinct provenance; review mode stops before review/publication. Never use it to seed an uploaded real case.
- WorkflowPlanningPersistenceProof references the C3 test project; the stabilization composition also uses actual C4 engines. Fixed loopback ports/database names and the batch4b test role are lab dependencies, not production credentials. Both main and stabilization paths call MigrateAsync; even retrieval modes are not guaranteed read-only. No tool was executed in this review.
- Generated proof output, data-protection material, local storage and browser screenshots stay under ignored .tmp. None belongs in a source commit.
- Synthetic generator dependency and output details are in its new source-side documentation. All generated answer files remain comparison fixtures.
- Both proposal PDFs have 11 pages and match their recorded SHA-256. The native PDF has the synthetic warning on all pages; every scanned page was visually reviewed in a contact sheet and carries it. No author/contact metadata was found beyond PDF format metadata. No purported complaint/conflict clearance proof is supplied.
- All 28 screenshots were opened for image/metadata inspection and OCR-reviewed without printing OCR text. Every image contains a Development/synthetic indicator; 27 expose session/case context. Hold the originals as optional local presentation artifacts. This is not publication clearance for any screenshot.
- Original case acceptance documents, source inventory, authority example and decision checklist can disclose source-derived facts, ownership identifiers, or installation-specific grants. Hold them. The authority example has real IDs despite its filename; the new unbound example replaces that disclosure for handover purposes.
- Raw evidence JSON is held conservatively, including files with no automatic privacy flag. Nested payloads/claims require source-specific approval; filename-based synthetic labels are insufficient. New indices retain only whitelisted statuses/outcomes, source filenames and digests; repeated nested fields are not distinct successful requests.

## Documentation links

## Static validation of tooling

Both `ProjectWorkflowProvisioner.csproj` and `WorkflowPlanningPersistenceProof.csproj` built successfully with `--no-restore`; each reported only NU1900 vulnerability-metadata unavailability. Neither executable nor either browser/API driver was run. These source builds are not new persistence, OCR, engine, authorization or browser acceptance demonstrations. The existing migration chain contains historical table-removal operations; do not describe arbitrary application or reversal as safe. Never point the proof runner at the ordinary application database.

## Link review results

The link audit found 121 local relative Markdown links; 84 target existing untracked files. See link-audit.json for each source/target. Do not commit acceptance prose while silently omitting its supporting private evidence. Keep those original reports local, or prepare source-specific sanitized reports and replace links in a separately authorized documentation change.

Tracked C4 handover/walkthrough documents may also reference held evidence; the historical list is not proof that a new engine/runtime/ledger run succeeded. No existing documentation link was rewritten in this review.

## Ownership and manual commands

C3/C4 are scope labels, not forensic author attribution. Use the user's approved C3 identity for their contributions; use the C4 owner only with confirmed contribution and authorization. Shared provisioning/proof/review work requires actual authorship confirmation, and may need approved co-authors. Do not change credentials or attribute all mixed work to one person.

Commands use exact inspected files, never entire directories. Review the index after every add and before committing. Stop if unrelated files were already staged. None of these commands was executed.

## 55. Shared - docs(dev): add sanitized local identity and spatial setup examples

Authorship: Shared: actual contribution authorship must be confirmed; do not infer.


```powershell
git add -- "StateLandGovernance/docs/configuration/examples/development-accounts.example.json" "StateLandGovernance/docs/configuration/examples/portal-development-spatial.example.json" "StateLandGovernance/docs/configuration/examples/workflow-authority.unbound.example.json" "StateLandGovernance/docs/configuration/examples/LOCAL_DEVELOPMENT_SETUP.md"
git diff --cached --name-status
git diff --cached --check
git diff --cached
git commit -m "docs(dev): add sanitized local identity and spatial setup examples"
```

## 56. Shared - chore(integration): preserve explicit project workflow provisioning tools

Authorship: Shared: actual contribution authorship must be confirmed; do not infer.
Depends on committed C3/C4 contracts. Contains deliberate writes to the fixed local Development database when explicitly invoked. Do not run during commit review.

```powershell
git add -- "StateLandGovernance/tools/ProjectWorkflowProvisioner/ProjectWorkflowProvisioner.csproj" "StateLandGovernance/tools/ProjectWorkflowProvisioner/Program.cs" "StateLandGovernance/tools/ProjectWorkflowProvisioner/PortalResearchProvisioner.cs"
git diff --cached --name-status
git diff --cached --check
git diff --cached
git commit -m "chore(integration): preserve explicit project workflow provisioning tools"
```

## 57. Shared - test(integration): retain isolated workflow persistence proof host

Authorship: Shared: actual contribution authorship must be confirmed; do not infer.
All C# files are required by entry-point references. Read modes also run MigrateAsync before retrieval. Historical modes contain explicit C4 readiness/model/transport doubles; stabilization-host uses actual deterministic C4 engines. No runtime execution was performed here.

```powershell
git add -- "StateLandGovernance/tools/WorkflowPlanningPersistenceProof/WorkflowPlanningPersistenceProof.csproj" "StateLandGovernance/tools/WorkflowPlanningPersistenceProof/Program.cs" "StateLandGovernance/tools/WorkflowPlanningPersistenceProof/ConcurrencyPersistenceProof.cs" "StateLandGovernance/tools/WorkflowPlanningPersistenceProof/ExecutionPersistenceProof.cs" "StateLandGovernance/tools/WorkflowPlanningPersistenceProof/FullWorkflowPersistenceProof.cs" "StateLandGovernance/tools/WorkflowPlanningPersistenceProof/GovernedExtensionsPersistenceProof.cs" "StateLandGovernance/tools/WorkflowPlanningPersistenceProof/StabilizationAcceptanceHost.cs" "StateLandGovernance/tools/WorkflowPlanningPersistenceProof/README.md"
git diff --cached --name-status
git diff --cached --check
git diff --cached
git commit -m "test(integration): retain isolated workflow persistence proof host"
```

## 58. Shared - test(integration): retain labelled isolated browser and API drivers

Authorship: Shared: actual contribution authorship must be confirmed; do not infer.
Depends on 57; requires requests and Playwright plus installed Edge/browser. Bearer ISOLATED-TEST strings are literal fixtures for the separate acceptance handler, not signed production tokens. Browser selectors may be stale after the guided UI redesign; not rerun.

```powershell
git add -- "StateLandGovernance/tools/WorkflowPlanningPersistenceProof/stabilization-browser.py" "StateLandGovernance/tools/WorkflowPlanningPersistenceProof/stabilization-scenario.py"
git diff --cached --name-status
git diff --cached --check
git diff --cached
git commit -m "test(integration): retain labelled isolated browser and API drivers"
```

## 59. C3 - test(c3): add editable synthetic proposal generation sources

Authorship: User/C3 identity.


```powershell
git add -- "StateLandGovernance/tools/document-intelligence/create_synthetic_proposal.py" "StateLandGovernance/tools/document-intelligence/SYNTHETIC_PROPOSAL_GENERATOR.md" "output/pdf/synthetic-proposal-12-acre/proposal-source.json"
git diff --cached --name-status
git diff --cached --check
git diff --cached
git commit -m "test(c3): add editable synthetic proposal generation sources"
```

## 60. C3 - docs(c3): add synthetic proposal comparison and coverage artifacts

Authorship: User/C3 identity.
Expected values are comparison fixtures, never OCR/Gemini outputs or automatic review decisions.

```powershell
git add -- "output/pdf/synthetic-proposal-12-acre/proposal-editable.html" "output/pdf/synthetic-proposal-12-acre/requirement-coverage.csv" "output/pdf/synthetic-proposal-12-acre/requirement-coverage.md" "output/pdf/synthetic-proposal-12-acre/expected-17-fields.json" "output/pdf/synthetic-proposal-12-acre/expected-17-fields.md" "output/pdf/synthetic-proposal-12-acre/artifact-validation.json"
git diff --cached --name-status
git diff --cached --check
git diff --cached
git commit -m "docs(c3): add synthetic proposal comparison and coverage artifacts"
```

## 61. C3 - test(c3): include labelled synthetic proposal upload PDFs

Authorship: User/C3 identity.
Optional binary artifacts: both reviewed 11-page PDFs match the generation manifest; scanned PDF is image-only. No ZIP needed.

```powershell
git add -- "output/pdf/synthetic-proposal-12-acre/SYNTHETIC_RESEARCH_12_ACRE_scanned.pdf" "output/pdf/synthetic-proposal-12-acre/SYNTHETIC_RESEARCH_12_ACRE_text.pdf"
git diff --cached --name-status
git diff --cached --check
git diff --cached
git commit -m "test(c3): include labelled synthetic proposal upload PDFs"
```

## 62. C3 - docs(c3): add portable manual synthetic upload instructions

Authorship: User/C3 identity.


```powershell
git add -- "StateLandGovernance/docs/verification/remaining-untracked-review/SYNTHETIC_UPLOAD_GUIDE.md"
git diff --cached --name-status
git diff --cached --check
git diff --cached
git commit -m "docs(c3): add portable manual synthetic upload instructions"
```

## 63. C3 - docs(c3): retain unconfigured routing and evidence contract examples

Authorship: User/C3 identity.
Nulls remain unanswered; no official policy, provider installation or clearance is claimed.

```powershell
git add -- "StateLandGovernance/docs/configuration/pending-approval/institutional-routing.json" "StateLandGovernance/docs/configuration/pending-approval/governance-evidence.json" "StateLandGovernance/docs/configuration/pending-approval/screening-evidence-context.json" "StateLandGovernance/docs/configuration/pending-approval/spatial-review-evidence.json"
git diff --cached --name-status
git diff --cached --check
git diff --cached
git commit -m "docs(c3): retain unconfigured routing and evidence contract examples"
```

## 64. C4 - docs(c4): retain an unconfigured catalogue rule contract example

Authorship: C4 owner only after contribution confirmation and authorization.


```powershell
git add -- "StateLandGovernance/docs/configuration/pending-approval/c4-catalogue-rule.json"
git diff --cached --name-status
git diff --cached --check
git diff --cached
git commit -m "docs(c4): retain an unconfigured catalogue rule contract example"
```

## 65. Shared - docs(integration): record the manifest to audit registration dependency

Authorship: Shared: actual contribution authorship must be confirmed; do not infer.
Owner proposal only; not a new registration endpoint or ledger receipt.

```powershell
git add -- "StateLandGovernance/docs/verification/owner-proposals/POST_WORKFLOW_INTEGRITY_CONTRACT.md"
git diff --cached --name-status
git diff --cached --check
git diff --cached
git commit -m "docs(integration): record the manifest to audit registration dependency"
```

## 66. C3 - docs(c3): add sanitized historical handover evidence index

Authorship: User/C3 identity.
Source association is C3-oriented, not proof of individual authorship; underlying records may span shared integration.

```powershell
git add -- "StateLandGovernance/docs/verification/remaining-untracked-review/c3-historical-evidence.summary.json"
git diff --cached --name-status
git diff --cached --check
git diff --cached
git commit -m "docs(c3): add sanitized historical handover evidence index"
```

## 67. C4 - docs(c4): add sanitized historical engine evidence index

Authorship: C4 owner only after contribution confirmation and authorization.


```powershell
git add -- "StateLandGovernance/docs/verification/remaining-untracked-review/c4-historical-evidence.summary.json"
git diff --cached --name-status
git diff --cached --check
git diff --cached
git commit -m "docs(c4): add sanitized historical engine evidence index"
```

## 68. Shared - docs(review): record untracked artifact privacy and dependency review

Authorship: Shared: actual contribution authorship must be confirmed; do not infer.
Review documentation belongs to the confirmed reviewer(s), not automatically the C4 owner.

```powershell
git add -- "StateLandGovernance/docs/verification/remaining-untracked-review/README.md" "StateLandGovernance/docs/verification/remaining-untracked-review/inventory.json" "StateLandGovernance/docs/verification/remaining-untracked-review/link-audit.json" "StateLandGovernance/docs/verification/remaining-untracked-review/manual-groups.json"
git diff --cached --name-status
git diff --cached --check
git diff --cached
git commit -m "docs(review): record untracked artifact privacy and dependency review"
```

## Held artifacts

- Both working local runtime JSON files; do not copy their verifiers/grants into examples.
- All 28 original screenshots and the duplicate ZIP bundle.
- Installation-specific old MANUAL_TEST_GUIDE.md, raw acceptance JSON and case-specific source/decision documents.
- Old owner patch files are historical proposals that may overlap applied work; do not apply or stage them as current implementation without reconciliation.
- Historical manual-commit-plan.json contains machine-specific command paths and stale staging metadata; do not run it as the new sequence. C4_FAILURES.md and its classification JSON are useful audit records, but retain them with their original README only after confirming historical context. They are not automatically assigned to the C4 owner.
- The original spatial-inputs-v1 fixture contains an installation-specific case binding; use the minimal new spatial example for a new checkout.

No secrets, grants, original evidence, source facts, credentials or environment files were removed.
