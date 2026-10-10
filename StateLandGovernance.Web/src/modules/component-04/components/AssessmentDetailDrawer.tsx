import React from "react";
import { AlertCircle, CheckCircle, HelpCircle, X, Sparkles } from "lucide-react";
import type { GovernanceCaseAssessment } from "../types/governance";
import { StatusBadge } from "@/shared/components/StatusBadge";
import { Button } from "@/shared/components/Button";
import styles from "./AssessmentDetailDrawer.module.css";

interface Props {
  caseAssessment: GovernanceCaseAssessment;
  onClose: () => void;
}

export function AssessmentDetailDrawer({ caseAssessment, onClose }: Props) {
  const isWarning = caseAssessment.overallStatus === "ReviewRequired";
  const isSuccess = caseAssessment.overallStatus === "Clear";

  return (
    <article className={styles.drawerContainer} aria-label={`Governance Detail for ${caseAssessment.caseId}`}>
      <div className={styles.headerRow}>
        <div className={styles.headerLeft}>
          {isWarning ? (
            <AlertCircle className={styles.statusIconWarning} size={28} aria-hidden="true" />
          ) : isSuccess ? (
            <CheckCircle className={styles.statusIconSuccess} size={28} aria-hidden="true" />
          ) : (
            <HelpCircle className={styles.statusIconNeutral} size={28} aria-hidden="true" />
          )}
          <div className={styles.titleArea}>
            <h2>
              {isWarning
                ? "Governance Review Required"
                : isSuccess
                ? "Governance Findings Clear"
                : "Prerequisite Information Pending"}
            </h2>
            <p className={styles.caseSubline}>
              Assessment {caseAssessment.assessmentId} &bull; Case {caseAssessment.caseId} &bull; Parcel {caseAssessment.parcelReference} &bull; Snapshot {caseAssessment.version}
            </p>
          </div>
        </div>
        <button
          type="button"
          onClick={onClose}
          className={styles.closeButton}
          aria-label="Close case detail"
        >
          <X size={20} />
        </button>
      </div>

      <div className={styles.advisoryNotice} role="note">
        <strong>Decision Support Only:</strong> Outputs support authorized officers in reviewing compliance,
        conflicts, anomalies, and consensus. System recommendations do not constitute legal lease approval or denial.
      </div>

      <div className={styles.section}>
        <h3 className={styles.sectionHeading}>What the system found</h3>
        <div
          className={
            isWarning
              ? styles.findingCardWarning
              : isSuccess
              ? styles.findingCardSuccess
              : styles.findingCardNeutral
          }
        >
          <p className={styles.findingCardTitle}>
            {isWarning
              ? "Potential Discrepancy or Boundary Issue"
              : isSuccess
              ? "Recorded Checks Clear — Other Checks May Be Pending"
              : "Incomplete Prerequisite Records"}
          </p>
          <p className={styles.findingCardBody}>{caseAssessment.summaryFinding}</p>
        </div>
      </div>

      <div className={styles.section}>
        <h3 className={styles.sectionHeading}>Why this result?</h3>
        <p className={styles.sectionText}>{caseAssessment.rationale}</p>
      </div>

      <div className={styles.section}>
        <h3 className={styles.sectionHeading}>Recommended next step</h3>
        <p className={styles.sectionText}>{caseAssessment.recommendedNextStep}</p>
      </div>

      {caseAssessment.complaintAdvisory && caseAssessment.complaintAdvisory.hasComplaint && (
        <div className={styles.mlBox} aria-label="Machine Learning Complaint Advisory Prediction">
          <div className={styles.mlBoxHeader}>
            <span>
              <Sparkles size={14} style={{ display: "inline", marginRight: "4px", verticalAlign: "middle" }} />
              Advisory Complaint Classification (ML Model)
            </span>
            <span className={styles.mlTag}>{caseAssessment.complaintAdvisory.modelVersion ?? "LogisticRegression"}</span>
          </div>
          <div className={styles.mlPredictionRow}>
            <span className={styles.mlCategory}>
              {caseAssessment.complaintAdvisory.predictedCategory}
            </span>
            {caseAssessment.complaintAdvisory.modelProbability !== undefined && (
              <span className={styles.mlProbability}>
                Model probability: {(caseAssessment.complaintAdvisory.modelProbability * 100).toFixed(0)}%
              </span>
            )}
          </div>
          <p className={styles.mlDisclaimer}>
            Illustrative review status: {caseAssessment.complaintAdvisory.reviewStatus} (demo only). {" "}
            Officer review persistence is unavailable. Any demo review status is illustrative only. Machine classification is an advisory prediction to assist case triage. It is not an officer decision,
            verified allegation, or finding of misconduct.
          </p>
        </div>
      )}

      <div className={styles.footerActions}>
        <span className={styles.plannedBadge}>
          Consensus: <StatusBadge tone={caseAssessment.consensusOutcome === "ConsensusReached" ? "success" : "neutral"}>
            {caseAssessment.consensusOutcome}
          </StatusBadge>
        </span>
        <Button variant="ghost" onClick={onClose}>
          Close
        </Button>
        <Button variant="primary" href={`/component-04/cases/assessment/${encodeURIComponent(caseAssessment.assessmentId)}`}>
          Open Case Dossier
        </Button>
      </div>
    </article>
  );
}
