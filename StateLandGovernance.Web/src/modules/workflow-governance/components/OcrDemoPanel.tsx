"use client";

import React, { useState, useRef, useId } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { processOcrDemoDocument } from "../services/ocrDemoApi";
import type { OcrDemoResponse, OcrLanguageMode, OcrExecutionMode, SemanticProvider } from "../types/ocrDemo";
import styles from "./OcrDemoPanel.module.css";

const ACCEPTED_EXTENSIONS = [".pdf", ".png", ".jpg", ".jpeg"];
const ACCEPTED_MIME_TYPES = "application/pdf,image/png,image/jpeg,.pdf,.png,.jpg,.jpeg";

export interface OcrDemoPanelProps {
  onProcessOverride?: (
    file: File,
    languageMode: OcrLanguageMode,
    executionMode?: OcrExecutionMode,
    semanticProvider?: SemanticProvider
  ) => Promise<OcrDemoResponse>;
}

export function OcrDemoPanel({ onProcessOverride }: OcrDemoPanelProps) {
  const [file, setFile] = useState<File | null>(null);
  const [languageMode, setLanguageMode] = useState<OcrLanguageMode>("sin+eng");
  const [executionMode, setExecutionMode] = useState<OcrExecutionMode>("ocr");
  const [semanticProvider, setSemanticProvider] = useState<SemanticProvider>("gemini");
  const [isLoading, setIsLoading] = useState(false);
  const [result, setResult] = useState<OcrDemoResponse | null>(null);
  const [error, setError] = useState<{ title: string; detail: string; status?: number } | null>(null);
  const [expandedRawPages, setExpandedRawPages] = useState<Record<number, boolean>>({});
  const [isDragging, setIsDragging] = useState(false);

  const fileInputRef = useRef<HTMLInputElement>(null);
  const fileInputId = useId();

  const handleFileChange = (incomingFile: File | null) => {
    if (!incomingFile) return;

    const lowerName = incomingFile.name.toLowerCase();
    const isAllowed = ACCEPTED_EXTENSIONS.some((ext) => lowerName.endsWith(ext));
    if (!isAllowed) {
      setError({
        title: "Unsupported File Format",
        detail: "Only PDF, PNG, JPG, and JPEG documents are accepted. Please select a valid document file.",
        status: 415,
      });
      setFile(null);
      return;
    }

    setFile(incomingFile);
    setError(null);
  };

  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setIsDragging(false);
    if (isLoading) return;
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      handleFileChange(e.dataTransfer.files[0]);
    }
  };

  const handleDragOver = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    if (!isLoading) {
      setIsDragging(true);
    }
  };

  const handleDragLeave = () => {
    setIsDragging(false);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!file || isLoading) return;

    setIsLoading(true);
    setError(null);

    try {
      const runner = onProcessOverride ?? processOcrDemoDocument;
      const response = await runner(file, languageMode, executionMode, semanticProvider);
      setResult(response);
      setExpandedRawPages({});
    } catch (err: unknown) {
      if (err instanceof ApiClientError) {
        let detail = err.detail || "OCR request failed.";
        let title = err.title || "Processing Error";

        if (err.status === 413) {
          title = "Payload Too Large";
          detail = err.detail || "The uploaded file exceeds the maximum allowed size (25 MB).";
        } else if (err.status === 415) {
          title = "Unsupported Media Type";
          detail = err.detail || "Unsupported file format. Please upload a PDF, PNG, or JPEG file.";
        } else if (err.status === 422) {
          title = "Unprocessable Document";
          detail = err.detail || "Unable to extract content from the uploaded document.";
        } else if (err.status === 503) {
          title = "Service Unavailable";
          detail = err.detail || "The OCR service is currently unavailable. Ensure the FastAPI service is running.";
        } else if (err.status === 504) {
          title = "Gateway Timeout";
          detail = err.detail || "The OCR service timed out while processing the document.";
        } else if (err.status === 0) {
          title = "API Connection Failed";
          detail = "Unable to reach the OCR API. Check that the ASP.NET Core API is running at http://localhost:5264.";
        }

        setError({
          status: err.status,
          title,
          detail,
        });
      } else {
        setError({
          title: "Unexpected Error",
          detail: err instanceof Error ? err.message : "An unexpected error occurred.",
        });
      }
    } finally {
      setIsLoading(false);
    }
  };

  const handleClear = () => {
    setFile(null);
    setResult(null);
    setError(null);
    setExpandedRawPages({});
    if (fileInputRef.current) {
      fileInputRef.current.value = "";
    }
  };

  const toggleRawText = (pageNumber: number) => {
    setExpandedRawPages((prev) => ({
      ...prev,
      [pageNumber]: !prev[pageNumber],
    }));
  };

  const formatFileSize = (bytes: number): string => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  };

  const activeProvider = result?.semanticProvider
    ? (result.semanticProvider.toLowerCase().includes("gemini")
        ? "Gemini"
        : (result.semanticProvider.toLowerCase().includes("qwen") || result.semanticProvider.toLowerCase().includes("ollama"))
        ? "Qwen"
        : result.semanticProvider)
    : (result?.modelIdentifier?.toLowerCase().includes("gemini")
        ? "Gemini"
        : result?.modelIdentifier?.toLowerCase().includes("qwen")
        ? "Qwen"
        : "Semantic AI");

  const activeModel = result?.modelIdentifier ?? (activeProvider === "Gemini" ? "gemini-flash-latest" : "qwen3:1.7b");

  return (
    <div className={styles.container}>
      <div className={styles.devBadge}>
        <span>⚠️ Development Only — Component 3 OCR Slice</span>
      </div>

      <h1 className={styles.title}>Development OCR Demo</h1>
      <p className={styles.subtitle}>
        Upload a PDF, PNG, or JPEG document to validate the end-to-end governed OCR pipeline (ASP.NET Core → FastAPI → Tesseract) and semantic candidate extraction (Gemini / Qwen). Machine-generated text only; not human verified.
      </p>

      {/* UPLOAD FORM CARD */}
      <div className={styles.card}>
        <form onSubmit={handleSubmit}>
          {/* FILE DROP ZONE */}
          <div
            className={`${styles.dropZone} ${isDragging ? styles.dropZoneActive : ""} ${
              isLoading ? styles.dropZoneDisabled : ""
            }`}
            onClick={() => !isLoading && fileInputRef.current?.click()}
            onDrop={handleDrop}
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            role="button"
            tabIndex={0}
            onKeyDown={(e) => {
              if ((e.key === "Enter" || e.key === " ") && !isLoading) {
                fileInputRef.current?.click();
              }
            }}
            aria-label="File upload dropzone"
          >
            <input
              id={fileInputId}
              ref={fileInputRef}
              type="file"
              accept={ACCEPTED_MIME_TYPES}
              disabled={isLoading}
              className={styles.fileInputHidden}
              onChange={(e) => {
                if (e.target.files && e.target.files.length > 0) {
                  handleFileChange(e.target.files[0]);
                }
              }}
            />
            <p className={styles.dropZoneText}>
              {file ? "Click or drag to choose another file" : "Click to select a file, or drag and drop here"}
            </p>
            <p className={styles.dropZoneHint}>Supported formats: PDF, PNG, JPG, JPEG (Max 25 MB)</p>
          </div>

          {file && (
            <div className={styles.selectedFileInfo} data-testid="selected-file-info">
              <div>
                <span className={styles.fileName}>{file.name}</span>
                <span className={styles.fileSize}>({formatFileSize(file.size)})</span>
              </div>
              <button
                type="button"
                className={styles.clearButton}
                onClick={handleClear}
                disabled={isLoading}
                aria-label="Remove selected file"
              >
                Remove
              </button>
            </div>
          )}

          {/* MODE SELECTOR */}
          <div style={{ marginTop: "1.5rem" }}>
            <label className={styles.sectionLabel} id="execution-mode-label">
              Processing Mode
            </label>
            <div
              className={styles.modeOptions}
              role="radiogroup"
              aria-labelledby="execution-mode-label"
            >
              <label
                className={`${styles.modeOption} ${
                  executionMode === "ocr" ? styles.modeOptionSelected : ""
                }`}
              >
                <input
                  type="radio"
                  name="executionMode"
                  value="ocr"
                  checked={executionMode === "ocr"}
                  onChange={() => setExecutionMode("ocr")}
                  disabled={isLoading}
                  data-testid="mode-ocr"
                />
                <span>OCR Only</span>
              </label>

              <label
                className={`${styles.modeOption} ${
                  executionMode === "analyze" ? styles.modeOptionSelected : ""
                }`}
              >
                <input
                  type="radio"
                  name="executionMode"
                  value="analyze"
                  checked={executionMode === "analyze"}
                  onChange={() => setExecutionMode("analyze")}
                  disabled={isLoading}
                  data-testid="mode-analyze"
                />
                <span>OCR + Governed Semantic Analysis</span>
              </label>
            </div>
          </div>

          {/* SEMANTIC AI PROVIDER SELECTOR */}
          {executionMode === "analyze" && (
            <div data-testid="semantic-provider-section">
              <label className={styles.sectionLabel} id="semantic-provider-label">
                Semantic AI Provider
              </label>
              <div
                className={styles.languageOptions}
                role="radiogroup"
                aria-labelledby="semantic-provider-label"
              >
                <label
                  className={`${styles.languageOption} ${
                    semanticProvider === "gemini" ? styles.languageOptionSelected : ""
                  }`}
                >
                  <input
                    type="radio"
                    name="semanticProvider"
                    value="gemini"
                    checked={semanticProvider === "gemini"}
                    onChange={() => setSemanticProvider("gemini")}
                    disabled={isLoading}
                    data-testid="provider-gemini"
                  />
                  <span>Google Gemini (gemini-flash-latest) [Recommended]</span>
                </label>

                <label
                  className={`${styles.languageOption} ${
                    semanticProvider === "qwen" ? styles.languageOptionSelected : ""
                  }`}
                >
                  <input
                    type="radio"
                    name="semanticProvider"
                    value="qwen"
                    checked={semanticProvider === "qwen"}
                    onChange={() => setSemanticProvider("qwen")}
                    disabled={isLoading}
                    data-testid="provider-qwen"
                  />
                  <span>Ollama Qwen (qwen3:1.7b) [Local]</span>
                </label>
              </div>
            </div>
          )}

          {/* LANGUAGE SELECTOR */}
          <div>
            <label className={styles.sectionLabel} id="language-mode-label">
              OCR Language Mode
            </label>
            <div
              className={styles.languageOptions}
              role="radiogroup"
              aria-labelledby="language-mode-label"
            >
              <label
                className={`${styles.languageOption} ${
                  languageMode === "eng" ? styles.languageOptionSelected : ""
                }`}
              >
                <input
                  type="radio"
                  name="languageMode"
                  value="eng"
                  checked={languageMode === "eng"}
                  onChange={() => setLanguageMode("eng")}
                  disabled={isLoading}
                  data-testid="lang-eng"
                />
                <span>English (eng)</span>
              </label>

              <label
                className={`${styles.languageOption} ${
                  languageMode === "sin" ? styles.languageOptionSelected : ""
                }`}
              >
                <input
                  type="radio"
                  name="languageMode"
                  value="sin"
                  checked={languageMode === "sin"}
                  onChange={() => setLanguageMode("sin")}
                  disabled={isLoading}
                  data-testid="lang-sin"
                />
                <span>Sinhala (sin)</span>
              </label>

              <label
                className={`${styles.languageOption} ${
                  languageMode === "sin+eng" ? styles.languageOptionSelected : ""
                }`}
              >
                <input
                  type="radio"
                  name="languageMode"
                  value="sin+eng"
                  checked={languageMode === "sin+eng"}
                  onChange={() => setLanguageMode("sin+eng")}
                  disabled={isLoading}
                  data-testid="lang-sin-eng"
                />
                <span>Sinhala + English (sin+eng)</span>
              </label>
            </div>
          </div>

          {/* ACTION BUTTONS */}
          <div className={styles.actionRow}>
            <button
              type="submit"
              className={styles.submitButton}
              disabled={!file || isLoading}
              data-testid="submit-btn"
            >
              {isLoading
                ? (executionMode === "analyze" ? "Running OCR & Analysis..." : "Running OCR...")
                : (executionMode === "analyze" ? (semanticProvider === "gemini" ? "Run OCR + Gemini Analysis" : "Run OCR + Qwen Analysis") : "Run OCR")}
            </button>

            {(file || result || error) && (
              <button
                type="button"
                className={styles.clearButton}
                onClick={handleClear}
                disabled={isLoading}
                data-testid="reset-btn"
              >
                Clear result / Try another document
              </button>
            )}
          </div>
        </form>
      </div>

      {/* LOADING STATE */}
      {isLoading && (
        <div className={styles.loadingBox} data-testid="loading-box" role="status">
          <span>⏳ Uploading and running OCR pipeline...</span>
        </div>
      )}

      {/* ERROR MESSAGE */}
      {error && (
        <div className={styles.errorBox} data-testid="error-box" role="alert">
          <div className={styles.errorTitle}>
            {error.status ? `[${error.status}] ` : ""}
            {error.title}
          </div>
          <p className={styles.errorDetail}>{error.detail}</p>
        </div>
      )}

      {/* RESULTS SECTION */}
      {result && (
        <div data-testid="ocr-results">
          {/* GOVERNANCE NOTICE */}
          <div className={styles.governanceNotice} data-testid="governance-notice">
            <h2 className={styles.governanceNoticeTitle}>Mandatory Governance Notice</h2>
            <p className={styles.governanceNoticeText}>{result.governanceNotice}</p>
          </div>

          {/* CANDIDATE FACTS DISCLAIMER */}
          {result.candidateFacts && result.candidateFacts.length > 0 && (
            <div className={styles.candidateDisclaimer} data-testid="candidate-disclaimer">
              <strong>AI Candidate Facts Notice:</strong> AI-extracted facts are machine-generated candidate facts and are not authoritative until reviewed and verified by an authorized human officer.
            </div>
          )}

          {/* MACHINE CLASSIFICATION CARD */}
          {result.classification && (
            <div className={styles.classificationCard} data-testid="classification-card">
              <div className={styles.classificationInfo}>
                <span className={styles.classificationLabel}>Machine Document Classification:</span>
                <span className={styles.classificationValue} data-testid="classification-type">
                  {result.classification.documentType}
                </span>
              </div>
              <div className={styles.classificationInfo}>
                <span className={styles.classificationLabel}>Confidence:</span>
                <span className={styles.confidenceBadge} data-testid="classification-confidence">
                  {result.classification.documentType === "Unavailable"
                    ? "Semantic inference did not complete."
                    : result.classification.confidence !== null && result.classification.confidence !== undefined
                    ? `${(result.classification.confidence * 100).toFixed(1)}%`
                    : "Not available"}
                </span>
              </div>
              {(result.modelIdentifier || result.semanticProvider) && (
                <div className={styles.metaSubtext} data-testid="semantic-model-meta">
                  Provider: <strong>{activeProvider}</strong> | Model: <strong>{activeModel}</strong> | Prompt: <strong>{result.promptVersion ?? "default"}</strong>
                </div>
              )}

              {/* SUMMARY COUNTS GRID */}
              {result.executionMode === "analyze" && (
                <div className={styles.summaryCountsGrid} data-testid="summary-counts-grid">
                  <div className={styles.summaryCountItem}>
                    <span className={styles.summaryCountLabel}>Total Candidates</span>
                    <span className={styles.summaryCountValue} data-testid="count-total">
                      {result.candidateCount ?? (result.allCandidates?.length ?? result.candidateFacts?.length ?? 0)}
                    </span>
                  </div>
                  <div className={styles.summaryCountItem}>
                    <span className={styles.summaryCountLabel}>Accepted</span>
                    <span className={styles.summaryCountValue} style={{ color: "#166534" }} data-testid="count-accepted">
                      {result.acceptedCount ?? (result.candidateFacts?.filter(c => c.candidateStatus === "Accepted" || c.isPreferred).length ?? 0)}
                    </span>
                  </div>
                  <div className={styles.summaryCountItem}>
                    <span className={styles.summaryCountLabel}>Rejected</span>
                    <span className={styles.summaryCountValue} style={{ color: "#991b1b" }} data-testid="count-rejected">
                      {result.rejectedCount ?? 0}
                    </span>
                  </div>
                  <div className={styles.summaryCountItem}>
                    <span className={styles.summaryCountLabel}>Conflict</span>
                    <span className={styles.summaryCountValue} style={{ color: "#c2410c" }} data-testid="count-conflict">
                      {result.conflictCount ?? 0}
                    </span>
                  </div>
                  <div className={styles.summaryCountItem}>
                    <span className={styles.summaryCountLabel}>Ungrounded</span>
                    <span className={styles.summaryCountValue} style={{ color: "#6b21a8" }} data-testid="count-ungrounded">
                      {result.ungroundedCount ?? 0}
                    </span>
                  </div>
                  <div className={styles.summaryCountItem}>
                    <span className={styles.summaryCountLabel}>Warnings</span>
                    <span className={styles.summaryCountValue} style={{ color: "#854d0e" }} data-testid="count-warnings">
                      {result.warnings?.length ?? 0}
                    </span>
                  </div>
                </div>
              )}
            </div>
          )}

          {/* GOVERNED 17-FIELD INVENTORY TABLE */}
          {result.executionMode === "analyze" && result.fieldInventory && result.fieldInventory.length > 0 && (
            <div className={styles.card} data-testid="field-inventory-card">
              <h2 style={{ margin: "0 0 0.5rem 0", fontSize: "1.1rem" }}>
                Governed Proposal Field Inventory (17-Field Catalog)
              </h2>
              <p style={{ margin: "0 0 1rem 0", fontSize: "0.85rem", color: "#64748b" }}>
                Governed state of all 17 canonical proposal fields. Fields are marked as Extracted, Missing, Rejected, Conflict, Needs Review, or Ungrounded.
              </p>
              <div className={styles.factsTableWrapper}>
                <table className={styles.factsTable} data-testid="field-inventory-table">
                  <thead>
                    <tr>
                      <th>Canonical Field</th>
                      <th>Display Name</th>
                      <th>Governance Status</th>
                      <th>Preferred Governed Value</th>
                      <th>Source Page</th>
                      <th>Confidence</th>
                      <th>Grounded</th>
                      <th>Candidates</th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.fieldInventory.map((item) => {
                      let badgeClass = styles.badgeMissing;
                      const st = item.status.toLowerCase();
                      if (st === "extracted" || st === "accepted") badgeClass = styles.badgeAccepted;
                      else if (st === "rejected") badgeClass = styles.badgeRejected;
                      else if (st === "conflict") badgeClass = styles.badgeConflict;
                      else if (st === "needs review" || st === "needsreview") badgeClass = styles.badgeNeedsReview;
                      else if (st === "ungrounded") badgeClass = styles.badgeUngrounded;

                      return (
                        <tr key={item.fieldCode} data-testid={`inventory-row-${item.fieldCode}`}>
                          <td>
                            <span className={styles.factCode}>{item.fieldCode}</span>
                          </td>
                          <td>
                            <strong>{item.displayName}</strong>
                          </td>
                          <td>
                            <span className={badgeClass} data-testid={`inventory-status-${item.fieldCode}`}>
                              {item.status}
                            </span>
                          </td>
                          <td>
                            {item.preferredValue ? (
                              <div>
                                {item.sourceType && (
                                  <span className={styles.sourceTypePill} data-testid={`inventory-source-${item.fieldCode}`}>
                                    {item.sourceType}
                                  </span>
                                )}
                                <span
                                  className={styles.canonicalValue}
                                  lang={result.languageMode.includes("sin") ? "si" : "en"}
                                  data-testid={`inventory-val-${item.fieldCode}`}
                                >
                                  {item.preferredValue}
                                </span>
                                {item.normalizedEnglishValue && item.normalizedEnglishValue !== item.preferredValue && (
                                  <span
                                    className={styles.normalizedEnSubtext}
                                    data-testid={`inventory-en-${item.fieldCode}`}
                                  >
                                    EN: {item.normalizedEnglishValue}
                                  </span>
                                )}
                              </div>
                            ) : (
                              <span style={{ color: "#94a3b8", fontStyle: "italic" }}>
                                {item.status === "Rejected" ? "(Rejected by validation)" : "(Missing / Not extracted)"}
                              </span>
                            )}
                          </td>
                          <td>
                            {item.pageNumber !== null && item.pageNumber !== undefined
                              ? `Page ${item.pageNumber}`
                              : "-"}
                          </td>
                          <td>
                            {item.confidence !== null && item.confidence !== undefined
                              ? `${(item.confidence * 100).toFixed(1)}%`
                              : "-"}
                          </td>
                          <td>
                            {item.isGrounded ? (
                              <span className={styles.groundedBadge}>Verified</span>
                            ) : (
                              <span style={{ color: "#94a3b8" }}>-</span>
                            )}
                          </td>
                          <td>{item.candidateCount ?? 0}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* ALL EXTRACTED CANDIDATES TABLE OR INCOMPLETE NOTICE */}
          {result.executionMode === "analyze" && (
            (result.allCandidates && result.allCandidates.length > 0) ||
            (result.candidateFacts && result.candidateFacts.length > 0) ? (
              <div className={styles.card} data-testid="candidate-facts-card">
                <h2 style={{ margin: "0 0 0.5rem 0", fontSize: "1.1rem" }}>
                  All Extracted Machine Candidates ({activeProvider} + Deterministic Verification)
                </h2>
                <p style={{ margin: "0 0 1rem 0", fontSize: "0.85rem", color: "#64748b" }}>
                  Full candidate inventory with verbatim OCR evidence quotes, source page numbers, deterministic validation results, and ranking. Machine candidates only; not legally authoritative.
                </p>
                <div className={styles.factsTableWrapper}>
                  <table
                    className={styles.factsTable}
                    data-testid="all-candidates-table"
                    // Also maintain candidate-facts-table testid for backward compatibility
                    id="candidate-facts-table"
                  >
                    <thead>
                      <tr>
                        <th>Fact Code</th>
                        <th>Rank / Preferred</th>
                        <th>Canonical / Normalized Value</th>
                        <th>Raw Model Value</th>
                        <th>Evidence &amp; Page</th>
                        <th>Confidence</th>
                        <th>Grounding Status</th>
                        <th>Semantic Validation</th>
                        <th>Candidate Status</th>
                        <th>Validation Reasons</th>
                      </tr>
                    </thead>
                    <tbody>
                      {(result.allCandidates && result.allCandidates.length > 0 ? result.allCandidates : result.candidateFacts || []).map((fact, idx) => {
                        let candBadgeClass = styles.badgeMissing;
                        const cst = (fact.candidateStatus || "Accepted").toLowerCase();
                        if (cst === "accepted") candBadgeClass = styles.badgeAccepted;
                        else if (cst === "rejected") candBadgeClass = styles.badgeRejected;
                        else if (cst === "conflict") candBadgeClass = styles.badgeConflict;
                        else if (cst === "needsreview" || cst === "needs review") candBadgeClass = styles.badgeNeedsReview;
                        else if (cst === "ungrounded") candBadgeClass = styles.badgeUngrounded;

                        return (
                          <tr key={`${fact.factCode}-${idx}`} data-testid={`candidate-fact-row-${fact.factCode}`}>
                            <td>
                              <span className={styles.factCode}>{fact.factCode}</span>
                            </td>
                            <td>
                              {fact.isPreferred ? (
                                <span className={styles.preferredBadge} data-testid={`fact-preferred-${fact.factCode}`}>
                                  ★ Preferred (Rank {fact.rank ?? 1})
                                </span>
                              ) : (
                                <span style={{ fontSize: "0.8rem", color: "#64748b" }}>
                                  Rank {fact.rank ?? (idx + 1)}
                                </span>
                              )}
                            </td>
                            <td>
                              <div>
                                {fact.sourceType && (
                                  <span className={styles.sourceTypePill} data-testid={`fact-source-${fact.factCode}`}>
                                    {fact.sourceType}
                                  </span>
                                )}
                                <span
                                  className={styles.canonicalValue}
                                  lang={result.languageMode.includes("sin") ? "si" : "en"}
                                  data-testid={`fact-value-${fact.factCode}`}
                                >
                                  {fact.normalizedValue ?? fact.canonicalValue}
                                </span>
                                {fact.normalizedEnglishValue && fact.normalizedEnglishValue !== (fact.normalizedValue ?? fact.canonicalValue) && (
                                  <span
                                    className={styles.normalizedEnSubtext}
                                    data-testid={`fact-en-${fact.factCode}`}
                                  >
                                    EN: {fact.normalizedEnglishValue}
                                  </span>
                                )}
                              </div>
                            </td>
                            <td>
                              <span
                                style={{ fontSize: "0.85rem", color: "#475569" }}
                                lang={result.languageMode.includes("sin") ? "si" : "en"}
                                data-testid={`fact-raw-${fact.factCode}`}
                              >
                                {fact.rawValue ?? fact.canonicalValue}
                              </span>
                            </td>
                            <td>
                              <div className={styles.evidencePage}>
                                {fact.evidencePage !== null && fact.evidencePage !== undefined
                                  ? `Page ${fact.evidencePage}`
                                  : "Page N/A"}
                              </div>
                              {fact.evidenceQuote && (
                                <div
                                  className={styles.evidenceQuote}
                                  lang={result.languageMode.includes("sin") ? "si" : "en"}
                                  data-testid={`fact-quote-${fact.factCode}`}
                                >
                                  "{fact.evidenceQuote}"
                                </div>
                              )}
                            </td>
                            <td>
                              {fact.confidence !== null && fact.confidence !== undefined
                                ? `${(fact.confidence * 100).toFixed(1)}%`
                                : "N/A"}
                            </td>
                            <td>
                              {fact.isGrounded ? (
                                <span className={styles.groundedBadge} data-testid={`fact-grounded-${fact.factCode}`}>
                                  Verified Grounded
                                </span>
                              ) : (
                                <span className={styles.unverifiedBadge} data-testid={`fact-unverified-${fact.factCode}`}>
                                  Evidence not verified against OCR transcript
                                </span>
                              )}
                              {fact.validationWarning && (
                                <div className={styles.factWarning} data-testid={`fact-warning-${fact.factCode}`}>
                                  ⚠️ {fact.validationWarning}
                                </div>
                              )}
                            </td>
                            <td>
                              {fact.isSemanticValid !== false ? (
                                <span className={styles.badgeAccepted}>Valid</span>
                              ) : (
                                <span className={styles.badgeRejected}>Invalid</span>
                              )}
                            </td>
                            <td>
                              <span className={candBadgeClass} data-testid={`fact-status-${fact.factCode}`}>
                                {fact.candidateStatus ?? "Accepted"}
                              </span>
                            </td>
                            <td>
                              {fact.validationReasons && fact.validationReasons.length > 0 ? (
                                <ul className={styles.reasonsList} data-testid={`fact-reasons-${fact.factCode}`}>
                                  {fact.validationReasons.map((r, rIdx) => (
                                    <li key={rIdx}>{r}</li>
                                  ))}
                                </ul>
                              ) : (
                                <span style={{ color: "#94a3b8" }}>-</span>
                              )}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              </div>
            ) : (
              <div className={styles.card} data-testid="candidate-facts-empty">
                <h2 style={{ margin: "0 0 0.5rem 0", fontSize: "1.1rem" }}>
                  AI Candidate Facts ({activeProvider})
                </h2>
                <p className={styles.emptyNotice} data-testid="no-candidate-facts-notice">
                  No AI candidate facts were produced because semantic inference did not complete.
                </p>
              </div>
            )
          )}

          {/* WARNINGS PANEL */}
          {result.warnings && result.warnings.length > 0 && (
            <div className={styles.warningsBox} data-testid="warnings-box">
              <strong>Processing Warnings:</strong>
              <ul className={styles.warningsList}>
                {result.warnings.map((w, idx) => (
                  <li key={idx}>{w}</li>
                ))}
              </ul>
            </div>
          )}

          {/* METADATA SUMMARY */}
          <div className={styles.card}>
            <h2 style={{ margin: "0 0 1rem 0", fontSize: "1.1rem" }}>Execution Summary</h2>
            <div className={styles.metadataGrid}>
              {result.executionMode === "analyze" ? (
                <>
                  <div className={styles.metaItem}>
                    <span className={styles.metaLabel}>OCR</span>
                    <span className={styles.metaValue} data-testid="meta-ocr-status">
                      Completed
                    </span>
                  </div>
                  <div className={styles.metaItem}>
                    <span className={styles.metaLabel}>Semantic Extraction</span>
                    <span
                      className={styles.metaValue}
                      data-testid="meta-semantic-status"
                      style={{
                        color:
                          result.semanticStatus === "Failed / Timed Out" ||
                          result.classification?.documentType === "Unavailable"
                            ? "#dc2626"
                            : undefined,
                      }}
                    >
                      {result.semanticStatus ??
                        (result.classification?.documentType === "Unavailable"
                          ? "Failed / Timed Out"
                          : "Completed")}
                    </span>
                  </div>
                </>
              ) : (
                <div className={styles.metaItem}>
                  <span className={styles.metaLabel}>Status</span>
                  <span className={styles.metaValue} data-testid="meta-status">
                    {result.status}
                  </span>
                </div>
              )}
              <div className={styles.metaItem}>
                <span className={styles.metaLabel}>Execution Mode</span>
                <span className={styles.metaValue} data-testid="meta-mode">
                  {result.executionMode ?? "ocr"}
                </span>
              </div>
              <div className={styles.metaItem}>
                <span className={styles.metaLabel}>File Name</span>
                <span className={styles.metaValue}>{result.fileName}</span>
              </div>
              <div className={styles.metaItem}>
                <span className={styles.metaLabel}>Language Mode</span>
                <span className={styles.metaValue} data-testid="meta-lang">
                  {result.languageMode}
                </span>
              </div>
              <div className={styles.metaItem}>
                <span className={styles.metaLabel}>Pages</span>
                <span className={styles.metaValue} data-testid="meta-pages">
                  {result.pageCount}
                </span>
              </div>
              <div className={styles.metaItem}>
                <span className={styles.metaLabel}>Media Type</span>
                <span className={styles.metaValue}>{result.mediaType}</span>
              </div>
            </div>

            {/* DURATION BREAKDOWN */}
            {result.durationBreakdown && (
              <div className={styles.durationGrid} data-testid="duration-breakdown">
                <div className={styles.durationItem}>
                  <span className={styles.durationLabel}>OCR Duration:</span>
                  <span className={styles.durationValue} data-testid="duration-ocr">
                    {result.durationBreakdown.ocrDurationMs !== null && result.durationBreakdown.ocrDurationMs !== undefined
                      ? `${result.durationBreakdown.ocrDurationMs.toFixed(0)} ms`
                      : "N/A"}
                  </span>
                </div>
                <div className={styles.durationItem}>
                  <span className={styles.durationLabel}>Semantic Duration:</span>
                  <span className={styles.durationValue} data-testid="duration-semantic">
                    {result.durationBreakdown.semanticDurationMs !== null && result.durationBreakdown.semanticDurationMs !== undefined
                      ? `${result.durationBreakdown.semanticDurationMs.toFixed(0)} ms`
                      : "N/A"}
                  </span>
                </div>
                <div className={styles.durationItem}>
                  <span className={styles.durationLabel}>Total Duration:</span>
                  <span className={styles.durationValue} data-testid="duration-total">
                    {result.durationBreakdown.totalDurationMs !== null && result.durationBreakdown.totalDurationMs !== undefined
                      ? `${result.durationBreakdown.totalDurationMs.toFixed(0)} ms`
                      : "N/A"}
                  </span>
                </div>
              </div>
            )}

            {/* FULL TEXT DISPLAY */}
            <h3 style={{ margin: "1.5rem 0 0.5rem 0", fontSize: "1rem" }}>Full OCR Transcript</h3>
            <div
              className={styles.fullTextArea}
              data-testid="full-text-transcript"
              lang={result.languageMode.includes("sin") ? "si" : "en"}
            >
              {result.fullText || <em>(No transcript produced)</em>}
            </div>
          </div>

          {/* PAGE-BY-PAGE BREAKDOWN */}
          <div className={styles.card}>
            <h2 style={{ margin: "0 0 1rem 0", fontSize: "1.1rem" }}>Page-by-Page Recognition</h2>
            {result.pages && result.pages.length > 0 ? (
              result.pages.map((page) => (
                <div
                  key={page.pageNumber}
                  className={styles.pageCard}
                  data-testid={`page-card-${page.pageNumber}`}
                >
                  <div className={styles.pageHeader}>
                    <h3 className={styles.pageTitle}>Page {page.pageNumber}</h3>
                    {page.confidence !== null && page.confidence !== undefined ? (
                      <span className={styles.confidenceBadge} data-testid="ocr-confidence-badge">
                        OCR confidence: {(page.confidence * 100).toFixed(1)}%
                      </span>
                    ) : (
                      <span className={styles.confidenceBadge} data-testid="ocr-confidence-badge">
                        OCR confidence: Not available
                      </span>
                    )}
                  </div>

                  <div
                    className={styles.pageCleanText}
                    data-testid={`page-clean-${page.pageNumber}`}
                    lang={result.languageMode.includes("sin") ? "si" : "en"}
                  >
                    {page.cleanText || <em>(Empty page text)</em>}
                  </div>

                  <div>
                    <button
                      type="button"
                      className={styles.toggleRawButton}
                      onClick={() => toggleRawText(page.pageNumber)}
                      data-testid={`toggle-raw-${page.pageNumber}`}
                    >
                      {expandedRawPages[page.pageNumber] ? "▲ Hide Raw OCR Text" : "▼ Show Raw OCR Text"}
                    </button>
                    {expandedRawPages[page.pageNumber] && (
                      <div
                        className={styles.pageRawText}
                        data-testid={`page-raw-${page.pageNumber}`}
                        lang={result.languageMode.includes("sin") ? "si" : "en"}
                      >
                        {page.rawText || <em>(Empty raw text)</em>}
                      </div>
                    )}
                  </div>
                </div>
              ))
            ) : (
              <p style={{ color: "#64748b" }}>No page records available.</p>
            )}
          </div>

          {/* TECHNICAL DETAILS COLLAPSIBLE */}
          <details className={styles.techDetails} data-testid="tech-details">
            <summary className={styles.techSummary}>Technical Pipeline Details</summary>
            <div className={styles.techContent}>
              <div className={styles.techItem}>
                <span className={styles.techKey}>Case ID:</span>
                <span className={styles.techVal}>{result.caseId}</span>
              </div>
              <div className={styles.techItem}>
                <span className={styles.techKey}>Governed Document ID:</span>
                <span className={styles.techVal}>{result.governedDocumentId}</span>
              </div>
              <div className={styles.techItem}>
                <span className={styles.techKey}>Document Version ID:</span>
                <span className={styles.techVal}>{result.documentVersionId}</span>
              </div>
              <div className={styles.techItem}>
                <span className={styles.techKey}>Document Analysis ID:</span>
                <span className={styles.techVal}>{result.documentAnalysisId}</span>
              </div>
              <div className={styles.techItem}>
                <span className={styles.techKey}>Analysis Run ID:</span>
                <span className={styles.techVal}>{result.analysisRunId}</span>
              </div>
              <div className={styles.techItem}>
                <span className={styles.techKey}>Media Type:</span>
                <span className={styles.techVal}>{result.mediaType}</span>
              </div>
              <div className={styles.techItem}>
                <span className={styles.techKey}>Language Mode:</span>
                <span className={styles.techVal}>{result.languageMode}</span>
              </div>
            </div>
          </details>
        </div>
      )}
    </div>
  );
}
