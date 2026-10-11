"use client";

import React, { useState, useRef, useId } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { processOcrDemoDocument } from "../services/ocrDemoApi";
import type { OcrDemoResponse, OcrLanguageMode } from "../types/ocrDemo";
import styles from "./OcrDemoPanel.module.css";

const ACCEPTED_EXTENSIONS = [".pdf", ".png", ".jpg", ".jpeg"];
const ACCEPTED_MIME_TYPES = "application/pdf,image/png,image/jpeg,.pdf,.png,.jpg,.jpeg";

export interface OcrDemoPanelProps {
  onProcessOverride?: (file: File, languageMode: OcrLanguageMode) => Promise<OcrDemoResponse>;
}

export function OcrDemoPanel({ onProcessOverride }: OcrDemoPanelProps) {
  const [file, setFile] = useState<File | null>(null);
  const [languageMode, setLanguageMode] = useState<OcrLanguageMode>("sin+eng");
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
      const response = await runner(file, languageMode);
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

  return (
    <div className={styles.container}>
      <div className={styles.devBadge}>
        <span>⚠️ Development Only — Component 3 OCR Slice</span>
      </div>

      <h1 className={styles.title}>Development OCR Demo</h1>
      <p className={styles.subtitle}>
        Upload a PDF, PNG, or JPEG document to validate the end-to-end governed OCR pipeline (ASP.NET Core → FastAPI → Tesseract). Machine-generated text only; not human verified.
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

          {/* LANGUAGE SELECTOR */}
          <div style={{ marginTop: "1.5rem" }}>
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
              {isLoading ? "Running OCR..." : "Run OCR"}
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
              <div className={styles.metaItem}>
                <span className={styles.metaLabel}>Status</span>
                <span className={styles.metaValue} data-testid="meta-status">
                  {result.status}
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
