"use client";

import { ProjectFulfillmentWorkspace } from "@/modules/workflow-governance/components/ProjectFulfillmentWorkspace";
import { ApplicantWorkflowProgress } from "@/modules/workflow-governance/components/ApplicantWorkflowProgress";
import React, { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { PageLayout, PageHeading } from "@/shared/components/PageLayout";
import { StatusBadge } from "@/shared/components/StatusBadge";
import { Button } from "@/shared/components/Button";
import { LoadingState } from "@/shared/components/LoadingState";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import {
  workflowGovernanceNavItems,
  WORKFLOW_GOVERNANCE_BASE,
} from "@/shared/constants/navigation";
import {
  getApplicantTracking,
  respondToCorrection,
} from "@/modules/workflow-governance/services/workflowGovernanceApi";
import type {
  ApplicantCaseTrackingDto,
  ApplicantCorrectionNoticeDto,
} from "@/modules/workflow-governance/types/caseManagement";
import styles from "./ApplicantTrackingPage.module.css";

export default function ApplicantTrackingPage() {
  const params = useParams();
  const rawRef = params.reference as string;
  const applicationReference = decodeURIComponent(rawRef || "");

  const [tracking, setTracking] = useState<ApplicantCaseTrackingDto | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  // Correction fulfillment state
  const [activeRespondingId, setActiveRespondingId] = useState<string | null>(null);
  const [responseFile, setResponseFile] = useState<File | null>(null);
  const [responseNotes, setResponseNotes] = useState<string>("");
  const [isSubmittingResponse, setIsSubmittingResponse] = useState<boolean>(false);
  const [submissionSuccessMessage, setSubmissionSuccessMessage] = useState<string | null>(null);
  const [submissionErrorMessage, setSubmissionErrorMessage] = useState<string | null>(null);

  const fetchTrackingData = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await getApplicantTracking(applicationReference, "applicant");
      setTracking(data);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Unable to load application tracking.";
      setError(msg);
    } finally {
      setIsLoading(false);
    }
  };

  const handleRespondToNotice = async (notice: ApplicantCorrectionNoticeDto, e: React.FormEvent) => {
    e.preventDefault();
    if (!tracking || !responseFile) return;

    setIsSubmittingResponse(true);
    setSubmissionSuccessMessage(null);
    setSubmissionErrorMessage(null);

    try {
      const formData = new FormData();
      formData.append("file", responseFile);
      formData.append("expectedRevision", String(notice.revision));
      if (responseNotes) {
        formData.append("applicantResponseNotes", responseNotes);
      }

      await respondToCorrection(
        tracking.leaseCaseId,
        notice.requestId,
        formData,
        "applicant"
      );

      setSubmissionSuccessMessage(
        `Successfully submitted supporting document for requirement "${notice.requiredClassificationCode || "Document"}". Your response has been linked to the case and queued for officer reassessment.`
      );
      setActiveRespondingId(null);
      setResponseFile(null);
      setResponseNotes("");
      await fetchTrackingData();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Failed to submit document response.";
      setSubmissionErrorMessage(msg);
    } finally {
      setIsSubmittingResponse(false);
    }
  };

  useEffect(() => {
    if (applicationReference) {
      fetchTrackingData();
    }
  }, [applicationReference]);

  return (
    <PageLayout
      navigationItems={workflowGovernanceNavItems}
      navigationBasePath={WORKFLOW_GOVERNANCE_BASE}
    >
      <div className={styles.backLink}>
        <Link href="/workflow-governance/applicant/tracking">← Lookup Another Application</Link>
      </div>

      <PageHeading
        title={`Application Tracking: ${applicationReference}`}
        subtitle="Current administrative status and milestone progression for your state land lease proposal."
        action={
          <Button variant="secondary" onClick={fetchTrackingData} disabled={isLoading}>
            Refresh Status
          </Button>
        }
      />

      {isLoading ? (
        <LoadingState message="Verifying application access and loading status..." />
      ) : error || !tracking ? (
        <ErrorMessage
          title="Tracking Information Unavailable"
          message={
            error ||
            "Could not locate an active application matching this reference number under your authenticated account."
          }
        />
      ) : (
        <div className={styles.trackingContainer}>
          <ApplicantWorkflowProgress caseId={tracking.leaseCaseId} />
          <ProjectFulfillmentWorkspace caseId={tracking.leaseCaseId} assignedOnly />
          {/* Top Status Overview */}
          <div className={styles.overviewCard}>
            <div className={styles.overviewRow}>
              <div>
                <span className={styles.metaLabel}>Current Stage</span>
                <h2 className={styles.stageTitle}>{tracking.currentMilestone}</h2>
                <span className={styles.submittedOn}>
                  Submitted on {new Date(tracking.createdAt).toLocaleDateString(undefined, {
                    year: "numeric",
                    month: "long",
                    day: "numeric",
                  })}
                </span>
              </div>
              <div className={styles.statusBox}>
                <span className={styles.metaLabel}>Overall Case Status</span>
                <div style={{ marginTop: "0.35rem" }}>
                  <StatusBadge
                    tone={
                      tracking.status === "ApprovedWithConditions" || tracking.status === "HandedOff"
                        ? "success"
                        : tracking.status === "InWorkflow"
                        ? "primary"
                        : "neutral"
                    }
                  >
                    {tracking.status}
                  </StatusBadge>
                </div>
              </div>
            </div>

            {/* Progress Bar */}
            <div className={styles.progressSection}>
              <div className={styles.progressBar}>
                <div
                  className={styles.progressFill}
                  style={{
                    width: `${Math.round(
                      (tracking.completedMilestonesCount / tracking.totalMilestonesCount) * 100
                    )}%`,
                  }}
                />
              </div>
              <div className={styles.progressMeta}>
                <span>
                  {tracking.completedMilestonesCount} of {tracking.totalMilestonesCount} Stages Completed
                </span>
                <span>
                  {Math.round(
                    (tracking.completedMilestonesCount / tracking.totalMilestonesCount) * 100
                  )}
                  % Complete
                </span>
              </div>
            </div>
          </div>

          {/* Action Required: Correction Notices */}
          {tracking.correctionNotices && tracking.correctionNotices.length > 0 && (
            <div className={styles.correctionSection}>
              <div className={styles.correctionHeader}>
                <div>
                  <h3>Action Required: Document & Revision Requests</h3>
                  <p>
                    Administrative officers have requested the following documentation or updates to advance your application.
                  </p>
                </div>
                <StatusBadge tone="warning">
                  {tracking.correctionNotices.filter((n) => n.status === "Pending").length} Pending Notice(s)
                </StatusBadge>
              </div>

              {submissionSuccessMessage && (
                <div style={{ background: "#ecfdf5", border: "1px solid #a7f3d0", color: "#065f46", padding: "0.85rem 1rem", borderRadius: "8px", fontSize: "0.875rem" }}>
                  {submissionSuccessMessage}
                </div>
              )}

              {submissionErrorMessage && (
                <div style={{ background: "#fef2f2", border: "1px solid #fecaca", color: "#991b1b", padding: "0.85rem 1rem", borderRadius: "8px", fontSize: "0.875rem" }}>
                  {submissionErrorMessage}
                </div>
              )}

              <div className={styles.noticesList}>
                {tracking.correctionNotices.map((notice) => {
                  const isPending = (notice.status === "Pending" || notice.status === "ChangesRequested");
                  const isResponding = activeRespondingId === notice.requestId;

                  return (
                    <div key={notice.requestId} className={styles.noticeCard}>
                      <div className={styles.noticeTop}>
                        <span className={styles.noticeActionTag}>
                          {notice.requiredAction}: {notice.requiredClassificationCode || notice.requiredSectionId || "Document"}
                        </span>
                        <StatusBadge tone={notice.status === "AcceptedAwaitingReassessment" ? "neutral" : "warning"}>
                          {notice.status}
                        </StatusBadge>
                      </div>

                      <div className={styles.noticeInstructions}>
                        {notice.instructions}
                      </div>

                      <div className={styles.noticeMeta}>
                        Requested on {new Date(notice.requestedAt).toLocaleDateString()}
                        {notice.dueDate && ` | Due Date: ${new Date(notice.dueDate).toLocaleDateString()}`}
                      </div>

                      {isPending && (
                        <div>
                          {!isResponding ? (
                            <Button
                              variant="primary"
                              onClick={() => {
                                setActiveRespondingId(notice.requestId);
                                setResponseFile(null);
                                setResponseNotes("");
                              }}
                            >
                              Upload Requested Document
                            </Button>
                          ) : (
                            <form
                              onSubmit={(e) => handleRespondToNotice(notice, e)}
                              className={styles.respondForm}
                            >
                              <div style={{ fontWeight: 600, fontSize: "0.85rem", color: "#374151" }}>
                                Submit Response for {notice.requiredClassificationCode || "Document"}
                              </div>
                              <div className={styles.respondInputs}>
                                <input
                                  type="file"
                                  required
                                  accept=".pdf,.png,.jpg,.jpeg"
                                  onChange={(e) => setResponseFile(e.target.files?.[0] || null)}
                                  className={styles.fileInput}
                                />
                                <input
                                  type="text"
                                  value={responseNotes}
                                  onChange={(e) => setResponseNotes(e.target.value)}
                                  placeholder="Optional notes or reference numbers"
                                  className={styles.notesInput}
                                />
                              </div>
                              <div style={{ display: "flex", gap: "0.5rem", justifyContent: "flex-end", marginTop: "0.5rem" }}>
                                <Button
                                  variant="secondary"
                                  type="button"
                                  onClick={() => setActiveRespondingId(null)}
                                >
                                  Cancel
                                </Button>
                                <Button
                                  variant="primary"
                                  type="submit"
                                  disabled={!responseFile || isSubmittingResponse}
                                >
                                  {isSubmittingResponse ? "Submitting..." : "Submit Document"}
                                </Button>
                              </div>
                            </form>
                          )}
                        </div>
                      )}

                      {!isPending && (
                        <div style={{ fontSize: "0.85rem", color: "#059669", fontWeight: 600 }}>
                          ✓ Response document submitted. Under officer review.
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            </div>
          )}

          {/* Milestone Stepper */}
          <div className={styles.timelineSection}>
            <h3 className={styles.sectionHeading}>Governance Milestones</h3>
            <div className={styles.stepper}>
              {tracking.milestones.map((m, idx) => {
                const isCompleted = m.state === "Completed";
                const isInProgress = m.state === "InProgress";

                return (
                  <div
                    key={m.milestoneKey}
                    className={`${styles.stepItem} ${
                      isCompleted
                        ? styles.stepCompleted
                        : isInProgress
                        ? styles.stepInProgress
                        : styles.stepPending
                    }`}
                  >
                    <div className={styles.stepIndicator}>
                      {isCompleted ? "✓" : idx + 1}
                    </div>
                    <div className={styles.stepContent}>
                      <div className={styles.stepHeader}>
                        <h4 className={styles.stepTitle}>{m.title}</h4>
                        <StatusBadge
                          tone={
                            isCompleted ? "success" : isInProgress ? "primary" : "neutral"
                          }
                        >
                          {m.state}
                        </StatusBadge>
                      </div>
                      <p className={styles.stepDescription}>{m.description}</p>
                      {m.updatedAt && (
                        <span className={styles.stepTimestamp}>
                          Updated: {new Date(m.updatedAt).toLocaleString()}
                        </span>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>

          {/* Submitted Documents Status */}
          <div className={styles.documentsSection}>
            <h3 className={styles.sectionHeading}>Submitted Application Documents</h3>
            <div className={styles.docTableCard}>
              <table className={styles.docTable}>
                <thead>
                  <tr>
                    <th>Category</th>
                    <th>Filename</th>
                    <th>Status</th>
                    <th>Uploaded At</th>
                  </tr>
                </thead>
                <tbody>
                  {tracking.documents.map((doc) => (
                    <tr key={doc.documentId}>
                      <td>
                        <strong>{doc.logicalCategory}</strong>
                      </td>
                      <td>{doc.originalFileName}</td>
                      <td>
                        <StatusBadge
                          tone={
                            doc.processingStatus === "Verified" || doc.processingStatus === "DataExtracted"
                              ? "success"
                              : doc.processingStatus === "Processing" || doc.processingStatus === "QueuedForExtraction"
                              ? "primary"
                              : "neutral"
                          }
                        >
                          {doc.processingStatus}
                        </StatusBadge>
                      </td>
                      <td>{new Date(doc.uploadedAt).toLocaleString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}
    </PageLayout>
  );
}
