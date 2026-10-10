import React from "react";
import { ShieldAlert, CheckCircle2, Clock, Layers } from "lucide-react";
import type { GovernanceDashboardSummary } from "../types/governance";
import styles from "./AssessmentSummaryCards.module.css";

interface Props {
  summary: GovernanceDashboardSummary;
}

export function AssessmentSummaryCards({ summary }: Props) {
  return (
    <section className={styles.grid} aria-label="Governance Assessment Key Metrics">
      <div className={styles.card}>
        <div className={styles.cardHeader}>
          <span className={styles.cardLabel}>Cases Evaluated</span>
          <div className={styles.iconWrapper} aria-hidden="true">
            <Layers size={18} />
          </div>
        </div>
        <p className={`${styles.value} ${styles.accentPrimary}`}>{summary.totalCases}</p>
        <p className={styles.hint}>{summary.totalAssessments} total snapshot evaluations</p>
      </div>

      <div className={styles.card}>
        <div className={styles.cardHeader}>
          <span className={styles.cardLabel}>Recorded clear status</span>
          <div className={styles.iconWrapper} aria-hidden="true">
            <CheckCircle2 size={18} />
          </div>
        </div>
        <p className={`${styles.value} ${styles.accentSuccess}`}>{summary.clearCount}</p>
        <p className={styles.hint}>Other checks may remain unassessed</p>
      </div>

      <div className={styles.card}>
        <div className={styles.cardHeader}>
          <span className={styles.cardLabel}>Review Required</span>
          <div className={styles.iconWrapper} aria-hidden="true">
            <ShieldAlert size={18} />
          </div>
        </div>
        <p className={`${styles.value} ${styles.accentWarning}`}>{summary.reviewRequiredCount}</p>
        <p className={styles.hint}>Requires administrative officer attention</p>
      </div>

      <div className={styles.card}>
        <div className={styles.cardHeader}>
          <span className={styles.cardLabel}>Insufficient Info</span>
          <div className={styles.iconWrapper} aria-hidden="true">
            <Clock size={18} />
          </div>
        </div>
        <p className={`${styles.value} ${styles.accentNeutral}`}>{summary.insufficientInfoCount}</p>
        <p className={styles.hint}>Prerequisite documentation pending</p>
      </div>
    </section>
  );
}
