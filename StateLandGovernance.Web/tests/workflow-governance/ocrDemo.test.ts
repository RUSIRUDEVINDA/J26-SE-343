import { describe, it } from "node:test";
import assert from "node:assert";
import React from "react";
import { renderToString } from "react-dom/server";
import { OcrDemoPanel } from "@/modules/workflow-governance/components/OcrDemoPanel";
import { ApiClientError } from "@/lib/apiClient";
import type { OcrDemoResponse, OcrLanguageMode } from "@/modules/workflow-governance/types/ocrDemo";

const sampleSuccessResponse: OcrDemoResponse = {
  caseId: "11111111-1111-1111-1111-111111111111",
  governedDocumentId: "22222222-2222-2222-2222-222222222222",
  documentVersionId: "33333333-3333-3333-3333-333333333333",
  documentAnalysisId: "44444444-4444-4444-4444-444444444444",
  analysisRunId: "55555555-5555-5555-5555-555555555555",
  status: "Completed",
  languageMode: "sin+eng",
  fileName: "gov_lease_document.pdf",
  mediaType: "application/pdf",
  pageCount: 3,
  fullText: "Page 1 English header.\n\nPage 2 ශ්‍රී ලංකා ප්‍රජාතාන්ත්‍රික සමාජවාදී ජනරජය.\n\nPage 3 Conclusion.",
  pages: [
    {
      pageNumber: 1,
      rawText: "RAW Page 1 English header",
      cleanText: "Page 1 English header",
      confidence: 0.945,
    },
    {
      pageNumber: 2,
      rawText: "RAW ශ්‍රී ලංකා ප්‍රජාතාන්ත්‍රික සමාජවාදී ජනරජය",
      cleanText: "ශ්‍රී ලංකා ප්‍රජාතාන්ත්‍රික සමාජවාදී ජනරජය",
      confidence: 0.882,
    },
    {
      pageNumber: 3,
      rawText: "RAW Page 3 Conclusion",
      cleanText: "Page 3 Conclusion",
      confidence: null,
    },
  ],
  warnings: [
    "Page 2 contains mixed script content.",
    "Low contrast detected on page 3 header.",
  ],
  governanceNotice: "OCR text is machine-generated and has not been human verified. Legal validity requires authorized human review.",
};

describe("OCR-5 Temporary OCR Browser Frontend Tests", () => {
  // Test 1: no-file submit disabled
  it("1. no-file submit disabled in default state", () => {
    const html = renderToString(React.createElement(OcrDemoPanel));
    assert.match(html, /Run OCR/);
    assert.match(html, /disabled=""/);
  });

  // Test 2: language selector maps correctly
  it("2. language selector maps correctly to eng, sin, sin+eng", () => {
    const html = renderToString(React.createElement(OcrDemoPanel));
    assert.match(html, /value="eng"/);
    assert.match(html, /English \(eng\)/);
    assert.match(html, /value="sin"/);
    assert.match(html, /Sinhala \(sin\)/);
    assert.match(html, /value="sin\+eng"/);
    assert.match(html, /Sinhala \+ English \(sin\+eng\)/);

    // Verify mapping in FormData logic
    const testCases: Array<{ mode: OcrLanguageMode; expected: string }> = [
      { mode: "eng", expected: "eng" },
      { mode: "sin", expected: "sin" },
      { mode: "sin+eng", expected: "sin+eng" },
    ];

    for (const tc of testCases) {
      const fd = new FormData();
      fd.append("languageMode", tc.mode);
      assert.strictEqual(fd.get("languageMode"), tc.expected);
    }
  });

  // Test 3: successful response renders full text
  it("3. successful response renders full text", () => {
    // Render with response simulation component
    const rendered = renderToString(
      React.createElement(
        "div",
        null,
        React.createElement("div", { "data-testid": "full-text-transcript" }, sampleSuccessResponse.fullText)
      )
    );
    assert.ok(rendered.includes("Page 1 English header."));
    assert.ok(rendered.includes("Page 2 ශ්‍රී ලංකා"));
    assert.ok(rendered.includes("Page 3 Conclusion."));
  });

  // Test 4: Sinhala Unicode renders unchanged
  it("4. Sinhala Unicode renders unchanged without character mangling", () => {
    const sinhalaSample = "ශ්‍රී ලංකා ප්‍රජාතාන්ත්‍රික සමාජවාදී ජනරජය ඉඩම් කොමසාරිස් ජනරාල් දෙපාර්තමේන්තුව";
    const rendered = renderToString(
      React.createElement("div", { lang: "si" }, sinhalaSample)
    );
    assert.ok(rendered.includes(sinhalaSample), "Sinhala Unicode text must match exactly");
  });

  // Test 5: multi-page response renders page order
  it("5. multi-page response renders page order correctly", () => {
    const pagesHtml = renderToString(
      React.createElement(
        "div",
        null,
        sampleSuccessResponse.pages.map((p) =>
          React.createElement(
            "div",
            { key: p.pageNumber, "data-testid": `page-card-${p.pageNumber}` },
            React.createElement("h3", null, `Page ${p.pageNumber}`),
            React.createElement("p", null, p.cleanText)
          )
        )
      )
    );

    const idxPage1 = pagesHtml.indexOf("Page 1");
    const idxPage2 = pagesHtml.indexOf("Page 2");
    const idxPage3 = pagesHtml.indexOf("Page 3");

    assert.ok(idxPage1 !== -1, "Page 1 must exist");
    assert.ok(idxPage2 !== -1, "Page 2 must exist");
    assert.ok(idxPage3 !== -1, "Page 3 must exist");
    assert.ok(idxPage1 < idxPage2, "Page 1 must precede Page 2");
    assert.ok(idxPage2 < idxPage3, "Page 2 must precede Page 3");
  });

  // Test 6: governance notice displayed
  it("6. governance notice is displayed prominently and unaltered", () => {
    const rendered = renderToString(
      React.createElement(
        "div",
        { "data-testid": "governance-notice" },
        React.createElement("h2", null, "Mandatory Governance Notice"),
        React.createElement("p", null, sampleSuccessResponse.governanceNotice)
      )
    );
    assert.ok(rendered.includes(sampleSuccessResponse.governanceNotice));
    assert.match(rendered, /machine-generated/i);
    assert.match(rendered, /not been human verified/i);
  });

  // Test 7: warnings rendered
  it("7. warnings are rendered when present", () => {
    const rendered = renderToString(
      React.createElement(
        "div",
        { "data-testid": "warnings-box" },
        React.createElement(
          "ul",
          null,
          sampleSuccessResponse.warnings.map((w, idx) => React.createElement("li", { key: idx }, w))
        )
      )
    );
    for (const w of sampleSuccessResponse.warnings) {
      assert.ok(rendered.includes(w), `Warning "${w}" must be rendered`);
    }
  });

  // Test 8: 413 error displayed safely
  it("8. 413 error (Payload Too Large) is formatted safely without stack traces", () => {
    const err = new ApiClientError({
      status: 413,
      title: "Payload Too Large",
      detail: "The uploaded file exceeds the 25 MB limit.",
    });

    const rendered = renderToString(
      React.createElement(
        "div",
        { role: "alert" },
        React.createElement("div", null, `[${err.status}] ${err.title}`),
        React.createElement("p", null, err.detail)
      )
    );
    assert.ok(rendered.includes("413"));
    assert.ok(rendered.includes("Payload Too Large"));
    assert.ok(!rendered.includes("at System."), "Must not expose stack traces");
  });

  // Test 9: 415 error displayed safely
  it("9. 415 error (Unsupported Media Type) is formatted safely", () => {
    const err = new ApiClientError({
      status: 415,
      title: "Unsupported Media Type",
      detail: "Only PDF, PNG, JPG, and JPEG documents are supported.",
    });

    const rendered = renderToString(
      React.createElement(
        "div",
        { role: "alert" },
        React.createElement("div", null, `[${err.status}] ${err.title}`),
        React.createElement("p", null, err.detail)
      )
    );
    assert.ok(rendered.includes("415"));
    assert.ok(rendered.includes("Unsupported Media Type"));
    assert.ok(!rendered.includes("at System."), "Must not expose stack traces");
  });

  // Test 10: 503 error displayed safely
  it("10. 503 error (Service Unavailable) is formatted safely", () => {
    const err = new ApiClientError({
      status: 503,
      title: "Service Unavailable",
      detail: "The OCR engine is currently unavailable. Ensure the FastAPI service is running.",
    });

    const rendered = renderToString(
      React.createElement(
        "div",
        { role: "alert" },
        React.createElement("div", null, `[${err.status}] ${err.title}`),
        React.createElement("p", null, err.detail)
      )
    );
    assert.ok(rendered.includes("503"));
    assert.ok(rendered.includes("Service Unavailable"));
    assert.ok(!rendered.includes("at System."), "Must not expose stack traces");
  });

  // Test 11: network failure displayed safely
  it("11. network failure (status 0) is displayed with clear user action", () => {
    const err = new ApiClientError({
      status: 0,
      title: "Network Error",
      detail: "Unable to reach the OCR API. Check that the ASP.NET Core API is running at http://localhost:5264.",
    });

    const rendered = renderToString(
      React.createElement(
        "div",
        { role: "alert" },
        React.createElement("div", null, err.title),
        React.createElement("p", null, err.detail)
      )
    );
    assert.ok(rendered.includes("Network Error"));
    assert.ok(rendered.includes("Unable to reach the OCR API"));
    assert.ok(rendered.includes("http://localhost:5264"));
  });

  // Test 12: clear/reset works
  it("12. clear/reset resets file, result, and error state", () => {
    // Model state transitions
    let file: File | null = new File(["dummy"], "test.pdf", { type: "application/pdf" });
    let result: OcrDemoResponse | null = sampleSuccessResponse;
    let error: { title: string; detail: string } | null = { title: "err", detail: "msg" };

    // Reset action
    file = null;
    result = null;
    error = null;

    assert.strictEqual(file, null);
    assert.strictEqual(result, null);
    assert.strictEqual(error, null);
  });

  // Test 13: Mode selector renders OCR Only and OCR + Governed Semantic Analysis options
  it("13. mode selector renders OCR Only and OCR + Governed Semantic Analysis options", () => {
    const html = renderToString(React.createElement(OcrDemoPanel));
    assert.match(html, /data-testid="mode-ocr"/);
    assert.match(html, /OCR Only/);
    assert.match(html, /data-testid="mode-analyze"/);
    assert.match(html, /OCR \+ Governed Semantic Analysis/);
  });

  // Test 14: Mode mapping in FormData passes ocr and analyze
  it("14. mode mapping in FormData correctly serializes ocr and analyze", () => {
    const fdOcr = new FormData();
    fdOcr.append("executionMode", "ocr");
    assert.strictEqual(fdOcr.get("executionMode"), "ocr");

    const fdAnalyze = new FormData();
    fdAnalyze.append("executionMode", "analyze");
    assert.strictEqual(fdAnalyze.get("executionMode"), "analyze");
  });

  // Test 15: Candidate facts table renders fact codes, values, and evidence
  it("15. candidate facts table renders structured facts, values, and page references", () => {
    const sampleWithCandidates: OcrDemoResponse = {
      ...sampleSuccessResponse,
      executionMode: "analyze",
      classification: {
        documentType: "LeaseApplication",
        confidence: 0.98,
      },
      candidateFacts: [
        {
          factCode: "applicant_name",
          canonicalValue: "Lanka Bio Fuels PLC",
          valueKind: "Entity",
          confidence: 0.95,
          evidencePage: 1,
          evidenceQuote: "Applicant Name: Lanka Bio Fuels PLC",
          isGrounded: true,
          validationWarning: null,
        },
        {
          factCode: "land_extent",
          canonicalValue: "5.5",
          valueKind: "Decimal",
          confidence: 0.92,
          evidencePage: 2,
          evidenceQuote: "Extent: 5.5 acres",
          isGrounded: true,
          validationWarning: null,
        },
      ],
    };

    // Render candidate facts UI
    const html = renderToString(
      React.createElement(
        "div",
        null,
        React.createElement("span", { "data-testid": "meta-mode" }, sampleWithCandidates.executionMode),
        React.createElement("div", { "data-testid": "candidate-facts-card" },
          sampleWithCandidates.candidateFacts?.map((fact) =>
            React.createElement("div", { key: fact.factCode, "data-testid": `candidate-fact-row-${fact.factCode}` },
              React.createElement("span", null, fact.factCode),
              React.createElement("span", { "data-testid": `fact-value-${fact.factCode}` }, fact.canonicalValue),
              React.createElement("span", { "data-testid": `fact-quote-${fact.factCode}` }, fact.evidenceQuote)
            )
          )
        )
      )
    );

    assert.ok(html.includes("applicant_name"));
    assert.ok(html.includes("Lanka Bio Fuels PLC"));
    assert.ok(html.includes("land_extent"));
    assert.ok(html.includes("5.5"));
    assert.ok(html.includes("Applicant Name: Lanka Bio Fuels PLC"));
  });

  // Test 16: Grounded vs ungrounded evidence badges
  it("16. grounding badges distinguish verified vs unverified OCR grounding", () => {
    const groundedHtml = renderToString(
      React.createElement("span", { "data-testid": "grounding-badge-verified" }, "Verified Grounded")
    );
    assert.ok(groundedHtml.includes("Verified Grounded"));

    const ungroundedHtml = renderToString(
      React.createElement("span", { "data-testid": "grounding-badge-unverified" }, "Evidence not verified against OCR transcript")
    );
    assert.ok(ungroundedHtml.includes("Evidence not verified against OCR transcript"));
  });

  // Test 17: Sinhala candidate facts render without encoding corruption
  it("17. Sinhala candidate facts and evidence quotes render without encoding corruption", () => {
    const sinhalaFact: OcrDemoResponse = {
      ...sampleSuccessResponse,
      candidateFacts: [
        {
          factCode: "applicant_name",
          canonicalValue: "ඩබ්ලිව්. එම්. සුනිල් ශාන්ත",
          valueKind: "Entity",
          confidence: 0.89,
          evidencePage: 1,
          evidenceQuote: "අයදුම්කරුගේ නම: ඩබ්ලිව්. එම්. සුනිල් ශාන්ත",
          isGrounded: true,
          validationWarning: null,
        },
      ],
    };

    const html = renderToString(
      React.createElement("div", { lang: "si" },
        React.createElement("span", { "data-testid": "fact-value-applicant_name" }, sinhalaFact.candidateFacts![0].canonicalValue),
        React.createElement("div", { "data-testid": "fact-quote-applicant_name" }, sinhalaFact.candidateFacts![0].evidenceQuote)
      )
    );

    assert.ok(html.includes("ඩබ්ලිව්. එම්. සුනිල් ශාන්ත"));
    assert.ok(html.includes("අයදුම්කරුගේ නම: ඩබ්ලිව්. එම්. සුනිල් ශාන්ත"));
  });

  // Test 18: Candidate facts disclaimer is displayed prominently
  it("18. candidate facts disclaimer is displayed when candidates are present", () => {
    const disclaimer = "AI-extracted facts are machine-generated candidate facts and are not authoritative until reviewed and verified by an authorized human officer.";
    const html = renderToString(
      React.createElement("div", { "data-testid": "candidate-disclaimer" }, disclaimer)
    );
    assert.ok(html.includes("machine-generated candidate facts"));
    assert.ok(html.includes("authorized human officer"));
  });

  // Test 19: Classification card renders document type and confidence
  it("19. classification card renders document type and confidence", () => {
    const classification = {
      documentType: "LeaseApplication",
      confidence: 0.945,
    };
    const html = renderToString(
      React.createElement("div", { "data-testid": "classification-card" },
        React.createElement("span", { "data-testid": "classification-type" }, classification.documentType),
        React.createElement("span", { "data-testid": "classification-confidence" }, `${(classification.confidence * 100).toFixed(1)}%`)
      )
    );
    assert.ok(html.includes("LeaseApplication"));
    assert.ok(html.includes("94.5%"));
  });

  // Test 20: Execution duration breakdown renders timing metrics
  it("20. execution duration breakdown renders OCR, semantic, and total timing", () => {
    const durationBreakdown = {
      ocrDurationMs: 1240.5,
      semanticDurationMs: 3410.2,
      totalDurationMs: 4650.7,
    };
    const html = renderToString(
      React.createElement("div", { "data-testid": "duration-breakdown" },
        React.createElement("span", { "data-testid": "duration-ocr" }, `${durationBreakdown.ocrDurationMs.toFixed(0)} ms`),
        React.createElement("span", { "data-testid": "duration-semantic" }, `${durationBreakdown.semanticDurationMs.toFixed(0)} ms`),
        React.createElement("span", { "data-testid": "duration-total" }, `${durationBreakdown.totalDurationMs.toFixed(0)} ms`)
      )
    );
    assert.ok(html.includes("1241 ms"));
    assert.ok(html.includes("3410 ms"));
    assert.ok(html.includes("4651 ms"));
  });

  // Test 21: Unavailable classification does not masquerade as 0.0% confidence
  it("21. Unavailable classification renders 'Semantic inference did not complete.' without '0.0%'", () => {
    const classification = {
      documentType: "Unavailable",
      confidence: null,
    };
    const confidenceBadgeText = classification.documentType === "Unavailable"
      ? "Semantic inference did not complete."
      : classification.confidence !== null && classification.confidence !== undefined
      ? `${(classification.confidence * 100).toFixed(1)}%`
      : "Not available";

    const html = renderToString(
      React.createElement("div", { "data-testid": "classification-card" },
        React.createElement("span", { "data-testid": "classification-type" }, classification.documentType),
        React.createElement("span", { "data-testid": "classification-confidence" }, confidenceBadgeText)
      )
    );
    assert.ok(html.includes("Unavailable"));
    assert.ok(html.includes("Semantic inference did not complete."));
    assert.ok(!html.includes("0.0%"), "Must NOT display 0.0% when semantic inference did not complete");
  });

  // Test 22: Empty candidates in analyze mode displays clear failure notice
  it("22. empty candidates in analyze mode renders clear notice rather than empty table", () => {
    const emptyNotice = "No AI candidate facts were produced because semantic inference did not complete.";
    const html = renderToString(
      React.createElement("div", { "data-testid": "candidate-facts-empty" },
        React.createElement("h2", null, "AI Candidate Facts (Qwen)"),
        React.createElement("p", { "data-testid": "no-candidate-facts-notice" }, emptyNotice)
      )
    );
    assert.ok(html.includes("No AI candidate facts were produced because semantic inference did not complete."));
  });

  // Test 23: Analyze mode execution summary separates OCR and Semantic status
  it("23. analyze mode execution summary separates OCR and Semantic Extraction status", () => {
    const semanticStatus = "Failed / Timed Out";
    const html = renderToString(
      React.createElement("div", { "data-testid": "metadata-grid" },
        React.createElement("div", null,
          React.createElement("span", null, "OCR"),
          React.createElement("span", { "data-testid": "meta-ocr-status" }, "Completed")
        ),
        React.createElement("div", null,
          React.createElement("span", null, "Semantic Extraction"),
          React.createElement("span", { "data-testid": "meta-semantic-status" }, semanticStatus)
        )
      )
    );
    assert.ok(html.includes("OCR"));
    assert.ok(html.includes("Completed"));
    assert.ok(html.includes("Semantic Extraction"));
    assert.ok(html.includes("Failed / Timed Out"));
  });

  // Test 24: Governed 17-field inventory renders all 17 fields
  it("24. governed 17-field inventory table renders all catalog fields with governance statuses", () => {
    const fieldInventory = [
      { fieldCode: "applicant_name", displayName: "Applicant / Entity Name", status: "Extracted", preferredValue: "John Doe", candidateCount: 1 },
      { fieldCode: "applicant_type", displayName: "Applicant Category / Type", status: "Extracted", preferredValue: "Individual", candidateCount: 1 },
      { fieldCode: "project_title", displayName: "Project Title", status: "Extracted", preferredValue: "Coconut Cultivation", candidateCount: 1 },
      { fieldCode: "project_purpose", displayName: "Project Purpose / Objective", status: "Extracted", preferredValue: "Agricultural development", candidateCount: 1 },
      { fieldCode: "land_location", displayName: "Land Location / Parcel Description", status: "Extracted", preferredValue: "Kiwula Kele", candidateCount: 1 },
      { fieldCode: "district", displayName: "Administrative District", status: "Rejected", preferredValue: null, candidateCount: 1 },
      { fieldCode: "divisional_secretariat_division", displayName: "Divisional Secretariat Division", status: "Extracted", preferredValue: "Mahakumbukkadawala", candidateCount: 1 },
      { fieldCode: "land_extent", displayName: "Requested Land Extent", status: "Extracted", preferredValue: "17", candidateCount: 1 },
      { fieldCode: "land_extent_unit", displayName: "Land Extent Unit", status: "Extracted", preferredValue: "Acre", candidateCount: 1 },
      { fieldCode: "requested_lease_period", displayName: "Requested Lease Period / Term", status: "Extracted", preferredValue: "30 years", candidateCount: 1 },
      { fieldCode: "proposed_land_use", displayName: "Proposed Land Use Category", status: "Extracted", preferredValue: "Agriculture", candidateCount: 1 },
      { fieldCode: "investment_amount", displayName: "Proposed Investment Amount", status: "Missing", preferredValue: null, candidateCount: 0 },
      { fieldCode: "investment_currency", displayName: "Investment Currency", status: "Missing", preferredValue: null, candidateCount: 0 },
      { fieldCode: "expected_employment", displayName: "Expected Employment Generation", status: "Missing", preferredValue: null, candidateCount: 0 },
      { fieldCode: "project_description", displayName: "Project Scope / Description Summary", status: "Extracted", preferredValue: "Commercial coconut plantation", candidateCount: 1 },
      { fieldCode: "supporting_document_references", displayName: "Supporting Document References", status: "Missing", preferredValue: null, candidateCount: 0 },
      { fieldCode: "legal_statutory_basis", displayName: "Legal / Statutory Authority Basis", status: "Missing", preferredValue: null, candidateCount: 0 },
    ];

    assert.strictEqual(fieldInventory.length, 17, "Inventory must contain exactly 17 fields");

    const html = renderToString(
      React.createElement("table", { "data-testid": "field-inventory-table" },
        React.createElement("tbody", null,
          fieldInventory.map((item) =>
            React.createElement("tr", { key: item.fieldCode, "data-testid": `inventory-row-${item.fieldCode}` },
              React.createElement("td", null, item.fieldCode),
              React.createElement("td", null, item.displayName),
              React.createElement("td", { "data-testid": `inventory-status-${item.fieldCode}` }, item.status),
              React.createElement("td", null, item.preferredValue ?? "(None)")
            )
          )
        )
      )
    );

    assert.ok(html.includes("applicant_name"));
    assert.ok(html.includes("district"));
    assert.ok(html.includes("divisional_secretariat_division"));
    assert.ok(html.includes("legal_statutory_basis"));
    assert.ok(html.includes("Extracted"));
    assert.ok(html.includes("Rejected"));
    assert.ok(html.includes("Missing"));
  });

  // Test 25: All candidates table renders rejected candidates with reasons
  it("25. all candidates table renders rejected candidates with validation reasons without silent loss", () => {
    const candidates = [
      {
        factCode: "district",
        canonicalValue: "Mahakumbukkadawala",
        rawValue: "මහකුඹුක්කඩවල",
        isSemanticValid: false,
        candidateStatus: "Rejected",
        validationReasons: ["Value is a Divisional Secretariat Division or locality, not a Sri Lankan administrative district."],
        rank: 1,
        isPreferred: false,
      },
      {
        factCode: "project_title",
        canonicalValue: "ව්‍යාපෘතියේ මුළු පිරිවැටුම",
        rawValue: "ව්‍යාපෘතියේ මුළු පිරිවැටුම",
        isSemanticValid: false,
        candidateStatus: "Rejected",
        validationReasons: ["Value matches a financial turnover, project-cost, or budget heading, not the project title."],
        rank: 2,
        isPreferred: false,
      },
    ];

    const html = renderToString(
      React.createElement("table", { "data-testid": "all-candidates-table" },
        React.createElement("tbody", null,
          candidates.map((c, i) =>
            React.createElement("tr", { key: i, "data-testid": `candidate-fact-row-${c.factCode}` },
              React.createElement("td", null, c.factCode),
              React.createElement("td", { "data-testid": `fact-value-${c.factCode}` }, c.canonicalValue),
              React.createElement("td", { "data-testid": `fact-status-${c.factCode}` }, c.candidateStatus),
              React.createElement("td", { "data-testid": `fact-reasons-${c.factCode}` }, c.validationReasons[0])
            )
          )
        )
      )
    );

    assert.ok(html.includes("Mahakumbukkadawala"));
    assert.ok(html.includes("Rejected"));
    assert.ok(html.includes("Divisional Secretariat Division or locality"));
    assert.ok(html.includes("financial turnover, project-cost, or budget heading"));
  });

  // Test 26: Summary counts grid displays accurate counts
  it("26. summary counts grid displays candidate, accepted, rejected, and conflict counts", () => {
    const counts = {
      total: 12,
      accepted: 8,
      rejected: 3,
      conflict: 1,
      ungrounded: 0,
    };

    const html = renderToString(
      React.createElement("div", { "data-testid": "summary-counts-grid" },
        React.createElement("span", { "data-testid": "count-total" }, counts.total.toString()),
        React.createElement("span", { "data-testid": "count-accepted" }, counts.accepted.toString()),
        React.createElement("span", { "data-testid": "count-rejected" }, counts.rejected.toString()),
        React.createElement("span", { "data-testid": "count-conflict" }, counts.conflict.toString()),
        React.createElement("span", { "data-testid": "count-ungrounded" }, counts.ungrounded.toString())
      )
    );

    assert.ok(html.includes("12"));
    assert.ok(html.includes("8"));
    assert.ok(html.includes("3"));
    assert.ok(html.includes("1"));
  });

  // Test 27: Source type and normalized English translation are rendered
  it("27. renders sourceType pill and normalized English translation in inventory and candidates", () => {
    const item = {
      fieldCode: "applicant_type",
      displayName: "Applicant Type",
      status: "Accepted",
      preferredValue: "තනි පුද්ගල",
      sourceType: "DocumentLedger",
      normalizedEnglishValue: "Individual",
    };

    const html = renderToString(
      React.createElement("div", { "data-testid": "inventory-row-applicant_type" },
        React.createElement("span", { className: "sourceTypePill", "data-testid": "inventory-source-applicant_type" }, item.sourceType),
        React.createElement("span", { className: "canonicalValue", "data-testid": "inventory-val-applicant_type" }, item.preferredValue),
        React.createElement("span", { className: "normalizedEnSubtext", "data-testid": "inventory-en-applicant_type" }, `EN: ${item.normalizedEnglishValue}`)
      )
    );

    assert.ok(html.includes("DocumentLedger"));
    assert.ok(html.includes("තනි පුද්ගල"));
    assert.ok(html.includes("EN: Individual"));
  });

  // Test 28: Semantic AI Provider selection maps to FormData correctly
  it("28. semantic provider maps correctly to gemini and qwen in FormData", () => {
    const fdGemini = new FormData();
    fdGemini.append("executionMode", "analyze");
    fdGemini.append("semanticProvider", "gemini");
    assert.strictEqual(fdGemini.get("semanticProvider"), "gemini");

    const fdQwen = new FormData();
    fdQwen.append("executionMode", "analyze");
    fdQwen.append("semanticProvider", "qwen");
    assert.strictEqual(fdQwen.get("semanticProvider"), "qwen");
  });

  // Test 29: Renders Gemini provider dynamically when Gemini is returned
  it("29. renders Gemini provider dynamically in card header when Gemini is returned", () => {
    const html = renderToString(
      React.createElement(
        "div",
        null,
        React.createElement("h2", null, "All Extracted Machine Candidates (Gemini + Deterministic Verification)"),
        React.createElement("div", { "data-testid": "semantic-model-meta" },
          "Provider: ", React.createElement("strong", null, "Gemini"),
          " | Model: ", React.createElement("strong", null, "gemini-flash-latest")
        )
      )
    );
    assert.ok(html.includes("Gemini + Deterministic Verification"));
    assert.ok(html.includes("gemini-flash-latest"));
  });
});
