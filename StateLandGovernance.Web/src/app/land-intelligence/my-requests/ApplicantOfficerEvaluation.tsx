"use client";

import { useMemo } from "react";
import { Button } from "@/shared/components/Button";
import type { ApplicantValuationLoadState } from "@/modules/component-01-land-intelligence/utils/applicantValuationLoad";
import { buildApplicantEvaluationBlocks } from "@/modules/component-01-land-intelligence/utils/applicantValuationDisplay";
import styles from "./MyRequests.module.css";

type ApplicantOfficerEvaluationProps = {
  state: ApplicantValuationLoadState;
  onRetry?: () => void;
};

export function ApplicantOfficerEvaluation({
  state,
  onRetry,
}: ApplicantOfficerEvaluationProps) {
  const blocks = useMemo(
    () =>
      state.kind === "available"
        ? buildApplicantEvaluationBlocks(state.summary)
        : [],
    [state],
  );

  return (
    <section
      className={styles.evaluationPanel}
      aria-labelledby="officer-evaluation-heading"
    >
      <div className={styles.evaluationPanelHeader}>
        <div>
          <p className={styles.requestEyebrow}>Officer review</p>
          <h2 id="officer-evaluation-heading" className={styles.evaluationTitle}>
            Officer evaluation
          </h2>
        </div>
      </div>

      <p className={styles.evaluationLead} role="note">
        This section shows the officer-recorded land valuation for your
        request.
      </p>

      {state.kind === "loading" ? (
        <p className={styles.evaluationStatusMessage} role="status">
          Loading officer evaluation…
        </p>
      ) : null}

      {state.kind === "none" ? (
        <div className={styles.evaluationEmpty} role="status">
          <strong>No officer evaluation yet</strong>
          <p>
            An officer has not recorded a land valuation for this request. You
            will see assessment details here once a valuation is saved.
          </p>
        </div>
      ) : null}

      {state.kind === "access_denied" || state.kind === "error" ? (
        <>
          <p className={styles.valuationError} role="alert">
            {state.message}
          </p>
          {state.kind === "error" && state.retryable && onRetry ? (
            <div className={styles.detailActions}>
              <Button variant="ghost" onClick={onRetry}>
                Retry
              </Button>
            </div>
          ) : null}
        </>
      ) : null}

      {state.kind === "available" ? (
        <>
          <div className={styles.evaluationBlocks}>
            {blocks.map((block) => (
              <div key={block.id} className={styles.evaluationBlock}>
                <h3 className={styles.evaluationBlockTitle}>{block.title}</h3>
                {block.intro ? (
                  <p className={styles.evaluationBlockIntro}>{block.intro}</p>
                ) : null}
                <dl className={styles.evaluationFieldGrid}>
                  {block.fields.map((field) => (
                    <div key={field.id}>
                      <dt>{field.label}</dt>
                      <dd>{field.value}</dd>
                    </div>
                  ))}
                </dl>
                {block.tables?.map((table) => (
                  <div key={table.id} className={styles.evaluationTableWrap}>
                    <p className={styles.evaluationTableTitle}>{table.title}</p>
                    <table className={styles.evaluationPeriodTable}>
                      <thead>
                        <tr>
                          <th scope="col">Period</th>
                          <th scope="col">Amount (LKR)</th>
                        </tr>
                      </thead>
                      <tbody>
                        {table.rows.map((row, index) => (
                          <tr key={`${table.id}-${index}`}>
                            <td>{row.period}</td>
                            <td>{row.amount}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                ))}
              </div>
            ))}
          </div>
        </>
      ) : null}
    </section>
  );
}

export function ApplicantAuthorityDecision({
  decision,
}: {
  decision: NonNullable<
    import("@/modules/component-01-land-intelligence/types/landIntelligence").LeaseInterestRequestDetail["latestAuthorityDecision"]
  >;
}) {
  return (
    <section
      className={styles.authorityPanel}
      aria-labelledby="authority-decision-heading"
    >
      <div className={styles.evaluationPanelHeader}>
        <div>
          <p className={styles.requestEyebrow}>Authority review</p>
          <h2 id="authority-decision-heading" className={styles.evaluationTitle}>
            Authority decision
          </h2>
        </div>
        <span className={styles.authorityDecisionBadge} role="status">
          {decision.decisionLabel}
        </span>
      </div>
      <p className={styles.evaluationLead}>
        This outcome is recorded by the leasing authority after officer review.
        It is not the same as the officer evaluation figures above.
      </p>
      <dl className={styles.evaluationFieldGrid}>
        {decision.applicantFacingReason.trim() ? (
          <div className={styles.evaluationFieldFullRow}>
            <dt>Reason for applicant</dt>
            <dd>{decision.applicantFacingReason}</dd>
          </div>
        ) : null}
        <div>
          <dt>Decision recorded</dt>
          <dd>
            {new Date(decision.recordedAtUtc).toLocaleString("en-LK", {
              dateStyle: "medium",
              timeStyle: "short",
            })}
          </dd>
        </div>
      </dl>
    </section>
  );
}
