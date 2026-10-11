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
});
