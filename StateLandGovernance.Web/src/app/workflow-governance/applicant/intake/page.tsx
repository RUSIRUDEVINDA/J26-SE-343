"use client";

import { apiGet } from "@/lib/apiClient";
import React, { useState, useId, useRef, useEffect } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { PageLayout, PageHeading } from "@/shared/components/PageLayout";
import { Button } from "@/shared/components/Button";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import {
  workflowGovernanceNavItems,
  WORKFLOW_GOVERNANCE_BASE,
} from "@/shared/constants/navigation";
import { registerCaseIntake } from "@/modules/workflow-governance/services/workflowGovernanceApi";
import type { RegisterCaseIntakeResponse } from "@/modules/workflow-governance/types/caseManagement";
import styles from "./ApplicantIntakePage.module.css";

const ACCEPTED_EXTENSIONS = [".pdf", ".png", ".jpg", ".jpeg"];

export default function ApplicantIntakePage() {
  const router = useRouter();

  // Form State
  const [appRef, setAppRef] = useState<string>(
    () => `SL-LMS-${new Date().getFullYear()}-${Math.floor(100000 + Math.random() * 900000)}`
  );
  const [purpose, setPurpose] = useState<string>("Commercial Agricultural Project");
  const [extentValue, setExtentValue] = useState<string>("10.5");
  const [extentUnit, setExtentUnit] = useState<string>("Acre");
  const [jurisdiction, setJurisdiction] = useState<string>("");
  const [languageMode, setLanguageMode] = useState<string>("sin+eng");
  const [jurisdictions,setJurisdictions]=useState<{jurisdictionCode:string;identifier:string;version:string}[]>([]);
  useEffect(()=>{void apiGet<{jurisdictionCode:string;identifier:string;version:string}[]>("/api/workflow-governance/intake-assignments/jurisdictions").then(setJurisdictions).catch(()=>setJurisdictions([]));},[]);
  const [file, setFile] = useState<File | null>(null);

  // Submission State
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [submissionError, setSubmissionError] = useState<string | null>(null);
  const [result, setResult] = useState<RegisterCaseIntakeResponse | null>(null);
  const [isDragging, setIsDragging] = useState<boolean>(false);

  const fileInputRef = useRef<HTMLInputElement>(null);
  const fileInputId = useId();

  const handleFileChange = (incomingFile: File | null) => {
    if (!incomingFile) return;

    const lowerName = incomingFile.name.toLowerCase();
    const isAllowed = ACCEPTED_EXTENSIONS.some((ext) => lowerName.endsWith(ext));
    if (!isAllowed) {
      setSubmissionError("Please select a PDF document or a valid image (PNG/JPG).");
      setFile(null);
      return;
    }

    setFile(incomingFile);
    setSubmissionError(null);
  };

  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setIsDragging(false);
    if (isSubmitting) return;
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      handleFileChange(e.dataTransfer.files[0]);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!file) {
      setSubmissionError("Please attach your state land lease proposal document.");
      return;
    }

    setIsSubmitting(true);
    setSubmissionError(null);

    const formData = new FormData();
    formData.append("file", file);
    formData.append("applicationReference", appRef.trim());
    formData.append("purpose", purpose);
    formData.append("requestedExtent", extentValue);
    formData.append("requestedExtentUnit", extentUnit);
    formData.append("jurisdictionCode", jurisdiction);
    formData.append("languageMode", languageMode);

    try {
      const response = await registerCaseIntake(formData, "applicant");
      setResult(response);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Failed to submit case intake.";
      setSubmissionError(msg);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <PageLayout
      navigationItems={workflowGovernanceNavItems}
      navigationBasePath={WORKFLOW_GOVERNANCE_BASE}
    >
      <PageHeading
        title="State Land Lease Proposal Intake"
        subtitle="Submit a formal lease application. Documents undergo automatic OCR text extraction and structured candidate extraction."
      />

      {result ? (
        <div className={styles.successCard}>
          <div className={styles.successIcon}>✓</div>
          <h2>Application Successfully Registered</h2>
          <p className={styles.successRef}>
            Reference Number: <strong>{result.applicationReference}</strong>
          </p>
          <div className={styles.successDetailsGrid}>
            <div>
              <span className={styles.subLabel}>Case Aggregate ID</span>
              <p className={styles.subVal}>{result.leaseCaseId}</p>
            </div>
            <div>
              <span className={styles.subLabel}>Initial Status</span>
              <p className={styles.subVal}>{result.status}</p>
            </div>
            <div>
              <span className={styles.subLabel}>Extracted Facts</span>
              <p className={styles.subVal}>{result.extractedCandidatesCount} facts parsed</p>
            </div>
            <div>
              <span className={styles.subLabel}>Analysis State</span>
              <p className={styles.subVal}>{result.analysisExecutionStatus}</p>
            </div>
          </div>

          {result.analysisExecutionStatus === "Failed" && (
            <div style={{ background: "#fffbeb", border: "1px solid #fde68a", color: "#92400e", padding: "0.85rem 1.25rem", borderRadius: "10px", margin: "1rem 0", fontSize: "0.875rem" }}>
              <strong>Notice:</strong> Your application reference <strong>{result.applicationReference}</strong> and proposal document have been successfully submitted and preserved. Automatic text intelligence encountered a temporary issue and will be processed or retried by the administrative case officer.
            </div>
          )}

          <div className={styles.successActions}>
            <Link href={`/workflow-governance/applicant/tracking/${encodeURIComponent(result.applicationReference)}`}>
              <Button variant="primary">Track Application Milestones →</Button>
            </Link>
            <Button
              variant="secondary"
              onClick={() => {
                setResult(null);
                setFile(null);
                setAppRef(`SL-LMS-${new Date().getFullYear()}-${Math.floor(100000 + Math.random() * 900000)}`);
              }}
            >
              Submit Another Application
            </Button>
          </div>
        </div>
      ) : (
        <div className={styles.formContainer}>
          {submissionError && (
            <div className={styles.errorNotice}>{submissionError}</div>
          )}

          <form onSubmit={handleSubmit} className={styles.intakeForm}>
            <div className={styles.sectionHeader}>
              <h3>1. Application Details</h3>
              <p>State land administration reference and purpose description.</p>
            </div>

            <div className={styles.formRow}>
              <div className={styles.formGroup}>
                <label htmlFor="intake-reference">Application Reference *</label>
                <input id="intake-reference"
                  type="text"
                  required
                  value={appRef}
                  onChange={(e) => setAppRef(e.target.value)}
                  className={styles.textInput}
                />
                <span className={styles.fieldHint}>Unique application reference (no statutory authority implied)</span>
              </div>

              <div className={styles.formGroup}>
                <label htmlFor="intake-jurisdiction">Target Jurisdiction District *</label>
                <select id="intake-jurisdiction"
                  value={jurisdiction}
                  onChange={(e) => setJurisdiction(e.target.value)}
                  className={styles.selectInput}
                >
                  <option value="">Not supplied ? authorized intake review needed</option>
                  {jurisdictions.filter(x=>x.jurisdictionCode.startsWith("DEVELOPMENT-")).map(x=><option key={x.identifier+":"+x.version} value={x.jurisdictionCode}>SYNTHETIC DEVELOPMENT TEST ? {x.jurisdictionCode}</option>)}
                  <option value="Hambantota">Hambantota District</option>
                  <option value="Colombo">Colombo District</option>
                  <option value="Gampaha">Gampaha District</option>
                  <option value="Kalutara">Kalutara District</option>
                  <option value="Kandy">Kandy District</option>
                  <option value="Galle">Galle District</option>
                  <option value="Matara">Matara District</option>
                  <option value="Kurunegala">Kurunegala District</option>
                  <option value="Anuradhapura">Anuradhapura District</option>
                </select>
              </div>
            </div>

            <div className={styles.formGroup}>
              <label htmlFor="intake-purpose">Lease Purpose & Intended Use *</label>
              <textarea id="intake-purpose"
                required
                rows={2}
                value={purpose}
                onChange={(e) => setPurpose(e.target.value)}
                className={styles.textareaInput}
                placeholder="e.g. Commercial Organic Coconut Cultivation & Agro-tourism Project"
              />
            </div>

            <div className={styles.formRow}>
              <div className={styles.formGroup}>
                <label htmlFor="intake-extent">Requested Extent *</label>
                <input id="intake-extent"
                  type="number"
                  step="0.01"
                  required
                  min="0.1"
                  value={extentValue}
                  onChange={(e) => setExtentValue(e.target.value)}
                  className={styles.textInput}
                />
              </div>

              <div className={styles.formGroup}>
                <label htmlFor="intake-unit">Measurement Unit</label>
                <select id="intake-unit"
                  value={extentUnit}
                  onChange={(e) => setExtentUnit(e.target.value)}
                  className={styles.selectInput}
                >
                  <option value="Acre">Acre(s)</option>
                  <option value="Rood">Rood(s)</option>
                  <option value="Perch">Perch(es)</option>
                  <option value="Hectare">Hectare(s)</option>
                </select>
              </div>

              <div className={styles.formGroup}>
                <label htmlFor="intake-language">Primary Proposal Language</label>
                <select id="intake-language"
                  value={languageMode}
                  onChange={(e) => setLanguageMode(e.target.value)}
                  className={styles.selectInput}
                >
                  <option value="sin+eng">Sinhala & English (Bilingual)</option>
                  <option value="sin">Sinhala (සිංහල)</option>
                  <option value="eng">English Only</option>
                </select>
              </div>
            </div>

            <div className={styles.sectionHeader} style={{ marginTop: "1rem" }}>
              <h3>2. Attach Lease Proposal Document</h3>
              <p>Upload the signed lease proposal document (PDF, PNG, JPG). Maximum size 25 MB.</p>
            </div>

            <div
              className={`${styles.dropZone} ${isDragging ? styles.dropZoneActive : ""} ${file ? styles.dropZoneFilled : ""}`}
              onDragOver={(e) => {
                e.preventDefault();
                setIsDragging(true);
              }}
              onDragLeave={() => setIsDragging(false)}
              onDrop={handleDrop}
              onClick={() => fileInputRef.current?.click()}
            >
              <input
                ref={fileInputRef}
                id={fileInputId}
                type="file"
                accept=".pdf,.png,.jpg,.jpeg"
                style={{ display: "none" }}
                onChange={(e) => handleFileChange(e.target.files?.[0] || null)}
              />

              {file ? (
                <div className={styles.fileSelectedInfo}>
                  <span className={styles.fileIcon}>📄</span>
                  <div>
                    <strong className={styles.fileName}>{file.name}</strong>
                    <div className={styles.fileMeta}>
                      {(file.size / (1024 * 1024)).toFixed(2)} MB • {file.type || "Document"}
                    </div>
                  </div>
                  <button
                    type="button"
                    className={styles.changeFileButton}
                    onClick={(e) => {
                      e.stopPropagation();
                      setFile(null);
                    }}
                  >
                    Change
                  </button>
                </div>
              ) : (
                <div className={styles.dropZonePrompt}>
                  <span className={styles.cloudIcon}>☁</span>
                  <p>
                    <strong>Click to browse</strong> or drag & drop proposal document here
                  </p>
                  <span className={styles.dropHint}>
                    Supports PDF, PNG, JPG (Sinhala/English official lease documents)
                  </span>
                </div>
              )}
            </div>

            <div className={styles.formActions}>
              <Button
                variant="primary"
                type="submit"
                disabled={isSubmitting || !file}
              >
                {isSubmitting ? "Processing Ingestion & Intake..." : "Submit Lease Application"}
              </Button>
            </div>
          </form>
        </div>
      )}
    </PageLayout>
  );
}
