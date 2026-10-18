"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import { PageHeading } from "@/shared/components/PageLayout";
import {
  getLeaseInterestRequest,
  getMyLeaseInterestValuation,
} from "@/modules/component-01-land-intelligence/services/citizenWorkspaceApi";
import type { LeaseInterestRequestDetail } from "@/modules/component-01-land-intelligence/types/landIntelligence";
import {
  loadApplicantValuationState,
  type ApplicantValuationLoadState,
} from "@/modules/component-01-land-intelligence/utils/applicantValuationLoad";
import { leaseInterestPurposeDisplay } from "@/modules/component-01-land-intelligence/utils/leaseInterestPurposeDisplay";
import {
  isLeaseInterestDraftStatus,
  leaseInterestApplicantOutcome,
  leaseInterestStatusBadgeTone,
  leaseInterestStatusLabel,
} from "@/modules/component-01-land-intelligence/utils/leaseInterestStatus";
import { StatusBadge } from "@/shared/components/StatusBadge";
import styles from "../MyRequests.module.css";

function valuationFormLabel(formType: string): string {
  if (formType === "Residential") {
    return "Residential lease worksheet";
  }
  if (formType === "AgriculturalCommercial") {
    return "Agricultural / commercial lease worksheet";
  }
  return formType;
}

export default function MyRequestDetailPage() {
  const params = useParams<{ requestId: string }>();
  const [requestResult, setRequestResult] = useState<{
    requestId: string;
    detail: LeaseInterestRequestDetail | null;
    error: string | null;
  } | null>(null);
  const [valuationResult, setValuationResult] = useState<{
    key: string;
    state: ApplicantValuationLoadState;
  } | null>(null);
  const [valuationAttempt, setValuationAttempt] = useState(0);

  const requestId = params.requestId;
  const valuationKey = `${requestId}:${valuationAttempt}`;
  const requestSettled = requestResult?.requestId === requestId;
  const loading = !requestSettled;
  const detail = requestSettled ? requestResult.detail : null;
  const error = requestSettled ? requestResult.error : null;
  const valuationState: ApplicantValuationLoadState =
    valuationResult?.key === valuationKey
      ? valuationResult.state
      : { kind: "loading" };

  useEffect(() => {
    let cancelled = false;

    getLeaseInterestRequest(requestId)
      .then((request) => {
        if (!cancelled) {
          setRequestResult({ requestId, detail: request, error: null });
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setRequestResult({
            requestId,
            detail: null,
            error:
              err instanceof ApiClientError
                ? err.detail || err.title
                : "Unable to load request.",
          });
        }
      });

    return () => {
      cancelled = true;
    };
  }, [requestId]);

  // Valuation loads independently so a valuation failure never hides the request.
  useEffect(() => {
    let cancelled = false;

    loadApplicantValuationState(() =>
      getMyLeaseInterestValuation(requestId),
    ).then((state) => {
      if (!cancelled) {
        setValuationResult({ key: valuationKey, state });
      }
    });

    return () => {
      cancelled = true;
    };
  }, [requestId, valuationKey]);

  const valuation =
    valuationState.kind === "available" ? valuationState.summary : null;

  return (
    <>
      <PageHeading
        title="Request status"
        subtitle="Preliminary expression of interest — not a reservation or lease."
      />
      {loading ? <LoadingState message="Loading request…" /> : null}
      {error ? <ErrorMessage message={error} /> : null}
      {detail ? (
        <div className={styles.card}>
          <p className={styles.title}>
            {detail.acknowledgementReference ?? "Lease interest request"}
          </p>
          <div className={styles.statusLine}>
            <StatusBadge tone={leaseInterestStatusBadgeTone(detail.status)}>
              {leaseInterestStatusLabel(detail.status)}
            </StatusBadge>
          </div>
          <p className={styles.outcome}>
            {leaseInterestApplicantOutcome(detail.status)}
          </p>
          {(() => {
            const purpose = leaseInterestPurposeDisplay(
              detail.proposal.requestedPurpose,
              detail.status,
            );
            if (!purpose && detail.proposal.requestedDurationYears == null) {
              return null;
            }
            return (
              <p className={styles.meta}>
                {purpose ? `Purpose: ${purpose}` : null}
                {purpose && detail.proposal.requestedDurationYears != null
                  ? " · "
                  : null}
                {detail.proposal.requestedDurationYears != null
                  ? `${detail.proposal.requestedDurationYears} years requested`
                  : null}
              </p>
            );
          })()}
          <h3 className={styles.sectionTitle}>Status timeline</h3>
          <ul className={styles.timeline}>
            {detail.statusHistory.map((entry, index) => (
              <li key={`${entry.changedAtUtc}-${index}`}>
                <strong>{leaseInterestStatusLabel(entry.status)}</strong> —{" "}
                {new Date(entry.changedAtUtc).toLocaleString()}
                {entry.note ? `: ${entry.note}` : ""}
              </li>
            ))}
          </ul>

          {valuationState.kind === "loading" ? (
            <section
              className={styles.valuationSection}
              aria-labelledby="officer-valuation-heading"
            >
              <h3 id="officer-valuation-heading" className={styles.sectionTitle}>
                Officer valuation
              </h3>
              <p className={styles.valuationNote} role="status">
                Loading valuation summary…
              </p>
            </section>
          ) : null}

          {valuationState.kind === "none" ? (
            <section
              className={styles.valuationSection}
              aria-labelledby="officer-valuation-heading"
            >
              <h3 id="officer-valuation-heading" className={styles.sectionTitle}>
                Officer valuation
              </h3>
              <p className={styles.valuationNote} role="status">
                No valuation recorded yet.
              </p>
            </section>
          ) : null}

          {valuationState.kind === "access_denied" ||
          valuationState.kind === "error" ? (
            <section
              className={styles.valuationSection}
              aria-labelledby="officer-valuation-heading"
            >
              <h3 id="officer-valuation-heading" className={styles.sectionTitle}>
                Officer valuation
              </h3>
              <p className={styles.valuationError} role="alert">
                {valuationState.message}
              </p>
              {valuationState.kind === "error" && valuationState.retryable ? (
                <div className={styles.actions}>
                  <Button
                    variant="ghost"
                    onClick={() => setValuationAttempt((n) => n + 1)}
                  >
                    Retry
                  </Button>
                </div>
              ) : null}
            </section>
          ) : null}

          {valuation ? (
            <section
              className={styles.valuationSection}
              aria-labelledby="officer-valuation-heading"
            >
              <h3 id="officer-valuation-heading" className={styles.sectionTitle}>
                Officer valuation
              </h3>
              <p className={styles.valuationBadge} role="status">
                Valuation recorded
              </p>
              <p className={styles.valuationNote}>
                An officer recorded a manual land valuation for this request.
                Recording a valuation does not change the request status by
                itself and is not a lease approval or reservation.
              </p>
              <dl className={styles.valuationDl}>
                <div>
                  <dt>Latest revision</dt>
                  <dd>{valuation.revision}</dd>
                </div>
                <div>
                  <dt>Recorded</dt>
                  <dd>
                    {new Date(valuation.recordedAtUtc).toLocaleString()}
                  </dd>
                </div>
                <div>
                  <dt>Worksheet type</dt>
                  <dd>{valuationFormLabel(valuation.formType)}</dd>
                </div>
                <div>
                  <dt>Assessed land value</dt>
                  <dd>
                    LKR{" "}
                    {valuation.assessedLandValueLkr.toLocaleString("en-LK", {
                      minimumFractionDigits: 2,
                    })}
                  </dd>
                </div>
                {valuation.leaseFileNumber ? (
                  <div>
                    <dt>Lease file number</dt>
                    <dd>{valuation.leaseFileNumber}</dd>
                  </div>
                ) : null}
                {valuation.divisionalSecretariat ? (
                  <div>
                    <dt>Divisional Secretariat</dt>
                    <dd>{valuation.divisionalSecretariat}</dd>
                  </div>
                ) : null}
              </dl>
            </section>
          ) : null}

          {detail.latestAuthorityDecision ? (
            <section
              className={styles.valuationSection}
              aria-labelledby="authority-decision-heading"
            >
              <h3
                id="authority-decision-heading"
                className={styles.sectionTitle}
              >
                Authority decision
              </h3>
              <p className={styles.valuationBadge} role="status">
                {detail.latestAuthorityDecision.decisionLabel}
              </p>
              <p className={styles.valuationNote}>
                {detail.latestAuthorityDecision.applicantFacingReason}
              </p>
              <dl className={styles.valuationDl}>
                <div>
                  <dt>Recorded</dt>
                  <dd>
                    {new Date(
                      detail.latestAuthorityDecision.recordedAtUtc,
                    ).toLocaleString()}
                  </dd>
                </div>
              </dl>
              {detail.latestAuthorityDecision.nextActionHint ? (
                <p className={styles.nextAction} role="status">
                  {detail.latestAuthorityDecision.nextActionHint}
                </p>
              ) : null}
              {detail.latestAuthorityDecision.decision === 1 ||
              detail.status === 7 ? (
                <p className={styles.formalLeaseNote} role="note">
                  Formal lease processing is still required. This is not a lease,
                  land reservation, or official government approval.
                </p>
              ) : null}
            </section>
          ) : null}

          <details className={styles.technicalDetails}>
            <summary>Technical details</summary>
            <dl className={styles.technicalDl}>
              <div>
                <dt>Request ID</dt>
                <dd className={styles.monoValue}>{detail.id}</dd>
              </div>
              <div>
                <dt>Parcel record ID</dt>
                <dd className={styles.monoValue}>{detail.landParcelId}</dd>
              </div>
              <div>
                <dt>Status code</dt>
                <dd className={styles.monoValue}>{detail.status}</dd>
              </div>
            </dl>
          </details>

          <div className={styles.actions}>
            {isLeaseInterestDraftStatus(detail.status) ? (
              <Button
                href={`/land-intelligence/parcels/${detail.landParcelId}/lease-interest`}
                variant="primary"
              >
                Continue draft
              </Button>
            ) : null}
            <Button href="/land-intelligence/my-requests" variant="ghost">
              Back to list
            </Button>
          </div>
        </div>
      ) : null}
    </>
  );
}
