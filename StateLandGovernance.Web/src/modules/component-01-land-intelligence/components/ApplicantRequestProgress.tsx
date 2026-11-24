import {
  buildApplicantProgressSteps,
  shouldShowApplicantProgressTracker,
  type ApplicantWorkflowContext,
} from "../utils/leaseInterestApplicantProgress";
import { formatApplicantDateTime } from "../utils/applicantValuationDisplay";
import styles from "@/app/land-intelligence/my-requests/MyRequests.module.css";

type ApplicantRequestProgressProps = {
  context: ApplicantWorkflowContext;
};

export function ApplicantRequestProgress({
  context,
}: ApplicantRequestProgressProps) {
  if (!shouldShowApplicantProgressTracker(context)) {
    return null;
  }

  const steps = buildApplicantProgressSteps(context);
  if (steps.length === 0) {
    return null;
  }

  return (
    <section
      className={styles.progressPanel}
      aria-labelledby="request-progress-heading"
    >
      <p className={styles.requestEyebrow}>Progress</p>
      <h2 id="request-progress-heading" className={styles.detailSectionTitle}>
        Request stages
      </h2>
      <ol className={styles.progressSteps}>
        {steps.map((step, index) => (
          <li
            key={step.id}
            className={
              step.state === "complete"
                ? styles.progressStepComplete
                : step.state === "current"
                  ? styles.progressStepCurrent
                  : styles.progressStepUpcoming
            }
          >
            <span className={styles.progressMarker} aria-hidden="true">
              {step.state === "complete" ? "✓" : index + 1}
            </span>
            <div className={styles.progressStepBody}>
              <span className={styles.progressStepLabel}>{step.label}</span>
              {step.state === "complete" && step.completedAtUtc ? (
                <time
                  className={styles.progressStepTime}
                  dateTime={step.completedAtUtc}
                >
                  {formatApplicantDateTime(step.completedAtUtc)}
                </time>
              ) : null}
              {step.state === "current" ? (
                <span className={styles.progressStepCurrentHint}>Current step</span>
              ) : null}
            </div>
          </li>
        ))}
      </ol>
    </section>
  );
}
