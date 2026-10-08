# Component 2 UI preview

Design reference: Figma Prototype, node 328:363, “COMPONENT 2 — FINANCIAL INTELLIGENCE & PROPOSAL FLOW”.

The UI follows the inspected maroon/cream design, module navigation and 13-screen flow. Open `/component-02`.

## Screens

- `/component-02` — overview
- `/component-02/documents` — file selection, replacement and removal
- `/component-02/ocr` — interactive sample processing stages
- `/component-02/extracted-data` — editable sample profile and review confirmation
- `/component-02/document-issues` — evidence corrections
- `/component-02/assessment` — fixed B sample, factor breakdown
- `/component-02/escalation` — separate higher-risk sample
- `/component-02/prediction` — illustrative prediction display
- `/component-02/historical-cases` — fictional case search, filters and detail view
- `/component-02/proposal-generator` — local sample-template creation
- `/component-02/proposal-editor` — edit and save a draft in session memory
- `/component-02/explainable-ai` — evidence requirements for future recommendations
- `/component-02/proposal-ready` — review confirmation and `.txt` draft export

## Integration boundary

No real OCR, financial scoring request, prediction, RAG, AI generation or submission is performed. File selection retains only the selected filename in React state; contents are not uploaded or persisted. Fixed sample assessments do not recalculate when fields change, and this is explained on the review screen. Proposal generation creates an explicitly labelled local template. Drafts and sample edits are retained by the layout provider during client navigation and cleared on reload or leaving the module. No financial data is stored in localStorage.

CRIB remains optional in the UI. Bureau subgrades, XX, outstanding balances, monthly payments, default history and lease approval outputs remain distinct. Live missing-credit scoring needs an approved backend policy.

Connect future services through the existing .NET API and financial extraction contract. Replace fixtures with typed API results and add busy, error and permission states when endpoints exist. Do not reinterpret demo percentages as model performance.
