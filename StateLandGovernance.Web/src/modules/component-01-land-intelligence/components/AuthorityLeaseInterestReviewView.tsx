"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import {
  getAuthorityLeaseInterestRequest,
  recordAuthorityDecision,
} from "../services/authorityLeaseInterestApi";
import type {
  LeaseInterestAuthorityDecisionKind,
  LeaseInterestAuthorityRequestDetail,
} from "../types/landIntelligence";
import { StatusBadge } from "@/shared/components/StatusBadge";
import { useModalDialogA11y } from "../hooks/useModalDialogA11y";
import { landUseTypeLabels } from "../utils/enumMappings";
import {
  leaseInterestApplicantOutcome,
  leaseInterestStatusBadgeTone,
  leaseInterestStatusLabel,
} from "../utils/leaseInterestStatus";
import { AuthorityValuationWorksheet } from "./AuthorityValuationWorksheet";
import { HistoricalValuationRecords } from "./HistoricalValuationRecords";
import { LeaseInterestApplicantDetailFields } from "./LeaseInterestApplicantDetailFields";
import { partitionValuationsByLatest } from "../utils/officerClosedCase";
import styles from "./AuthorityLeaseInterestReviewView.module.css";

const formatDate = (value: string) =>
  new Date(value).toLocaleString("en-LK", { dateStyle: "medium", timeStyle: "short" });

const DECISION_OPTIONS: {
  value: LeaseInterestAuthorityDecisionKind;
  label: string;
}[] = [
  { value: 1, label: "Approved" },
  { value: 2, label: "Rejected" },
];

function decisionLabel(value: LeaseInterestAuthorityDecisionKind): string {
  return DECISION_OPTIONS.find((option) => option.value === value)?.label ?? "Decision";
}

export function AuthorityLeaseInterestReviewView({
  requestId,
}: {
  requestId: string;
}) {
  const [detail, setDetail] = useState<LeaseInterestAuthorityRequestDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [decision, setDecision] = useState<LeaseInterestAuthorityDecisionKind>(1);
  const [reason, setReason] = useState("");
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [successOpen, setSuccessOpen] = useState(false);
  const [successLabel, setSuccessLabel] = useState<string | null>(null);
  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await getAuthorityLeaseInterestRequest(requestId);
      setDetail(result);
    } catch (err) {
      setError(
        err instanceof ApiClientError
          ? err.detail || err.title
          : "Unable to load request for authority review.",
      );
    } finally {
      setLoading(false);
    }
  }, [requestId]);

  // Load on mount / request change (async fetch — same pattern as other C1 review pages).
  useEffect(() => {
    let cancelled = false;
    void (async () => {
      setLoading(true);
      setError(null);
      try {
        const result = await getAuthorityLeaseInterestRequest(requestId);
        if (cancelled) {
          return;
        }
        setDetail(result);
      } catch (err) {
        if (cancelled) {
          return;
        }
        setError(
          err instanceof ApiClientError
            ? err.detail || err.title
            : "Unable to load request for authority review.",
        );
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [requestId]);

  const { dialogRef: confirmDialogRef } = useModalDialogA11y({
    open: confirmOpen,
    onClose: () => setConfirmOpen(false),
    busy: saving,
  });
  const { dialogRef: successDialogRef } = useModalDialogA11y({
    open: successOpen,
    onClose: () => setSuccessOpen(false),
  });

  function openConfirm() {
    setFormError(null);
    if (!detail?.canRecordDecision) {
      setFormError("This request is not open for an authority decision.");
      return;
    }
    if (!detail.latestValuation) {
      setFormError("An officer valuation is required before recording a decision.");
      return;
    }
    if (!reason.trim()) {
      setFormError("Enter a reason for the applicant.");
      return;
    }
    setConfirmOpen(true);
  }

  async function submitDecision() {
    if (!detail?.latestValuation || saving) {
      return;
    }
    setSaving(true);
    setFormError(null);
    try {
      const updated = await recordAuthorityDecision(requestId, {
        decision,
        reason: reason.trim(),
        expectedUpdatedAtUtc: detail.updatedAtUtc,
        reviewedValuationId: detail.latestValuation.id,
        reviewedValuationRevision: detail.latestValuation.revision,
      });
      setDetail(updated);
      setReason("");
      setConfirmOpen(false);
      setSuccessLabel(decisionLabel(decision));
      setSuccessOpen(true);
    } catch (err) {
      setConfirmOpen(false);
      setSuccessOpen(false);
      setFormError(
        err instanceof ApiClientError
          ? err.errors?.join(" ") || err.detail || err.title
          : "Unable to record authority decision.",
      );
      if (err instanceof ApiClientError && err.status === 409) {
        void load();
      }
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return <LoadingState message="Loading request for authority review…" />;
  }
  if (error && !detail) {
    return <ErrorMessage message={error} />;
  }
  if (!detail) {
    return null;
  }

  const purposeLabel =
    landUseTypeLabels[detail.proposal.requestedPurpose] ??
    String(detail.proposal.requestedPurpose);
  const { historical: historicalValuations } = partitionValuationsByLatest(
    detail.valuations,
  );
  const valuationForReview = detail.latestValuation ?? detail.valuations[0] ?? null;

  return (
    <div className={styles.page}>
      {confirmOpen ? (
        <div
          className={styles.modalOverlay}
          role="presentation"
          onClick={() => !saving && setConfirmOpen(false)}
        >
          <div
            ref={confirmDialogRef}
            className={styles.modalDialog}
            role="dialog"
            aria-modal="true"
            aria-labelledby="authority-confirm-title"
            aria-describedby="authority-confirm-body"
            aria-busy={saving}
            onClick={(event) => event.stopPropagation()}
          >
            <h2 id="authority-confirm-title" className={styles.modalTitle}>
              Confirm authority decision
            </h2>
            <p id="authority-confirm-body" className={styles.modalBody}>
              This updates the request status to{" "}
              <strong>{decision === 1 ? "Approved" : "Rejected"}</strong> and is
              visible to the applicant.
            </p>
            <div className={styles.modalActions}>
              <Button
                type="button"
                variant="primary"
                disabled={saving}
                onClick={() => void submitDecision()}
              >
                {saving ? "Recording…" : "Confirm decision"}
              </Button>
              <Button
                type="button"
                variant="ghost"
                disabled={saving}
                onClick={() => setConfirmOpen(false)}
              >
                Cancel
              </Button>
            </div>
          </div>
        </div>
      ) : null}

      {successOpen && successLabel ? (
        <div
          className={styles.modalOverlay}
          role="presentation"
          onClick={() => setSuccessOpen(false)}
        >
          <div
            ref={successDialogRef}
            className={`${styles.modalDialog} ${styles.modalDialogSuccess}`}
            role="dialog"
            aria-modal="true"
            aria-labelledby="authority-success-title"
            aria-describedby="authority-success-body"
            onClick={(event) => event.stopPropagation()}
          >
            <h2 id="authority-success-title" className={styles.modalTitle}>
              Authority decision recorded
            </h2>
            <p id="authority-success-body" className={styles.modalBody}>
              Decision: <strong>{successLabel}</strong>. Current status:{" "}
              <strong>{leaseInterestStatusLabel(detail.status)}</strong>.
              {detail.status === 7
                ? " Formal lease processing is still required."
                : null}
            </p>
            <div className={styles.modalActions}>
              <Button
                type="button"
                variant="primary"
                onClick={() => setSuccessOpen(false)}
              >
                Close
              </Button>
            </div>
          </div>
        </div>
      ) : null}

      <Link
        href="/land-intelligence/authority/lease-interest-queue"
        className={styles.backLink}
      >
        ← Back to authority queue
      </Link>

      <header className={styles.caseHeader}>
        <div>
          <p className={styles.eyebrow}>Authority review</p>
          <h2 className={styles.reference}>
            {detail.acknowledgementReference ?? "Lease interest request"}
          </h2>
          {leaseInterestApplicantOutcome(detail.status).trim() ? (
            <p className={styles.headerText}>
              {leaseInterestApplicantOutcome(detail.status)}
            </p>
          ) : null}
        </div>
        <StatusBadge tone={leaseInterestStatusBadgeTone(detail.status)}>
          {leaseInterestStatusLabel(detail.status)}
        </StatusBadge>
      </header>

      {error ? <ErrorMessage message={error} /> : null}

      <div className={styles.contentGrid}>
        <div className={styles.mainColumn}>
          <section className={styles.card} aria-labelledby="applicant-title">
            <div className={styles.sectionHeading}>
              <div>
                <p className={styles.eyebrow}>Case</p>
                <h3 id="applicant-title">Applicant, parcel and proposal</h3>
              </div>
              <Link
                href={`/land-intelligence/parcels/${detail.landParcelId}`}
                className={styles.textLink}
              >
                Open parcel intelligence
              </Link>
            </div>
            <dl className={styles.detailsGrid}>
              <LeaseInterestApplicantDetailFields applicant={detail.applicant} />
              <div>
                <dt>Cadastre / district</dt>
                <dd>
                  {detail.cadastralNumber ?? "Not entered"}
                  {detail.district ? ` · ${detail.district}` : ""}
                </dd>
              </div>
              <div>
                <dt>Requested purpose</dt>
                <dd>{purposeLabel}</dd>
              </div>
              <div>
                <dt>Requested duration</dt>
                <dd>{detail.proposal.requestedDurationYears} years</dd>
              </div>
              <div>
                <dt>Requested area</dt>
                <dd>
                  {detail.proposal.requestedAreaHectares != null
                    ? `${detail.proposal.requestedAreaHectares} ha`
                    : "Not entered"}
                </dd>
              </div>
              <div>
                <dt>Desired start period</dt>
                <dd>{detail.proposal.desiredStartPeriod || "Not entered"}</dd>
              </div>
              <div>
                <dt>Submitted</dt>
                <dd>
                  {detail.submittedAtUtc
                    ? formatDate(detail.submittedAtUtc)
                    : "Not submitted"}
                </dd>
              </div>
            </dl>
            <div className={styles.proposalBlock}>
              <p>
                <strong>Project summary:</strong>{" "}
                {detail.proposal.projectSummary || "Not entered"}
              </p>
              <p>
                <strong>Planned activities:</strong>{" "}
                {detail.proposal.plannedActivitiesDescription || "Not entered"}
              </p>
              <p>
                <strong>Supporting notes:</strong>{" "}
                {detail.supporting.supportingNotes?.trim()
                  ? detail.supporting.supportingNotes
                  : "Not entered"}
              </p>
            </div>
          </section>

          <section className={styles.card} aria-labelledby="valuation-title">
            <div className={styles.sectionHeading}>
              <div>
                <p className={styles.eyebrow}>Officer assessment</p>
                <h3 id="valuation-title">Complete valuation worksheet</h3>
              </div>
            </div>
            <p className={styles.fieldHelp}>
              Review every manually entered field before deciding. Values are
              shown exactly as recorded by the officer. The system does not
              calculate lease rates or totals.
            </p>

            {valuationForReview ? (
              <>
                <AuthorityValuationWorksheet valuation={valuationForReview} />
                <HistoricalValuationRecords
                  valuations={historicalValuations}
                  formatRecordedAt={formatDate}
                />
              </>
            ) : (
              <p className={styles.closedMessage}>
                No officer valuation is available for this request.
              </p>
            )}
          </section>

          <section className={styles.card} aria-labelledby="history-title">
            <div className={styles.sectionHeading}>
              <div>
                <p className={styles.eyebrow}>History</p>
                <h3 id="history-title">Request and review timeline</h3>
              </div>
            </div>
            <ol className={styles.timeline}>
              {detail.statusHistory.map((entry, index) => (
                <li key={`${entry.changedAtUtc}-${index}`}>
                  <strong>{leaseInterestStatusLabel(entry.status)}</strong> —{" "}
                  {formatDate(entry.changedAtUtc)}
                  {entry.note ? `: ${entry.note}` : ""}
                  {!entry.visibleToApplicant ? " (internal)" : ""}
                </li>
              ))}
            </ol>
          </section>

          {detail.authorityDecisions.length > 0 ? (
            <section
              className={styles.card}
              aria-labelledby="decision-history-title"
            >
              <div className={styles.sectionHeading}>
                <div>
                  <p className={styles.eyebrow}>Authority</p>
                  <h3 id="decision-history-title">Decision history</h3>
                </div>
              </div>
              <ol className={styles.decisionHistory}>
                {detail.authorityDecisions.map((item) => (
                  <li key={item.id}>
                    <strong>{decisionLabel(item.decision)}</strong> —{" "}
                    {formatDate(item.recordedAtUtc)}
                    {` · against valuation revision ${item.reviewedValuationRevision}`}
                    {`: ${item.reason}`}
                  </li>
                ))}
              </ol>
            </section>
          ) : null}
        </div>

        <aside
          className={styles.decisionCard}
          aria-labelledby="decision-form-title"
        >
          <h3 id="decision-form-title">Record decision</h3>
          {formError ? (
            <ErrorMessage id="authority-form-error" message={formError} />
          ) : null}
          {detail.canRecordDecision && detail.latestValuation ? (
            <form
              onSubmit={(event) => {
                event.preventDefault();
                openConfirm();
              }}
            >
              <p className={styles.fieldHelp}>
                Your decision applies to the officer valuation worksheet shown
                on this page.
              </p>
              <div className={styles.field}>
                <label htmlFor="authority-decision">Decision *</label>
                <select
                  id="authority-decision"
                  className={styles.input}
                  value={decision}
                  onChange={(event) =>
                    setDecision(
                      Number(
                        event.target.value,
                      ) as LeaseInterestAuthorityDecisionKind,
                    )
                  }
                >
                  {DECISION_OPTIONS.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </div>
              <div className={styles.field}>
                <label htmlFor="authority-reason">Reason for applicant *</label>
                <textarea
                  id="authority-reason"
                  className={styles.input}
                  rows={5}
                  maxLength={4000}
                  required
                  aria-invalid={formError ? true : undefined}
                  aria-describedby={
                    formError
                      ? "authority-reason-help authority-form-error"
                      : "authority-reason-help"
                  }
                  value={reason}
                  onChange={(event) => setReason(event.target.value)}
                />
                <p id="authority-reason-help" className={styles.fieldHelp}>
                  This text is shown to the applicant. Do not include internal
                  officer notes that should remain hidden.
                </p>
              </div>
              
              <Button
                type="submit"
                variant="primary"
                className={styles.submitButton}
                disabled={saving}
              >
                Review and submit decision
              </Button>
            </form>
          ) : (
            <p className={styles.closedMessage}>
              This request is not awaiting an authority decision. Status:{" "}
              {leaseInterestStatusLabel(detail.status)}.
            </p>
          )}
        </aside>
      </div>
    </div>
  );
}
