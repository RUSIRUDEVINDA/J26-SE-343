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
import { formatApplicantDateTime } from "@/modules/component-01-land-intelligence/utils/applicantValuationDisplay";
import { leaseInterestPurposeDisplay } from "@/modules/component-01-land-intelligence/utils/leaseInterestPurposeDisplay";
import {
  isLeaseInterestDraftStatus,
  leaseInterestApplicantOutcome,
  leaseInterestStatusBadgeTone,
  leaseInterestStatusLabel,
} from "@/modules/component-01-land-intelligence/utils/leaseInterestStatus";
import { StatusBadge } from "@/shared/components/StatusBadge";
import {
  ApplicantAuthorityDecision,
  ApplicantOfficerEvaluation,
} from "../ApplicantOfficerEvaluation";
import styles from "../MyRequests.module.css";

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

  const purpose = detail
    ? leaseInterestPurposeDisplay(detail.proposal.requestedPurpose, detail.status)
    : null;

  return (
    <>
      <PageHeading
        title="Request status"
        subtitle="Preliminary expression of interest."
      />
      {loading ? <LoadingState message="Loading request…" /> : null}
      {error ? <ErrorMessage message={error} /> : null}
      {detail ? (
        <div className={styles.requestDetail}>
          <section
            className={styles.requestOverview}
            aria-labelledby="request-reference-heading"
          >
            <div className={styles.requestOverviewTop}>
              <div>
                <p className={styles.requestEyebrow}>Request reference</p>
                <h2
                  id="request-reference-heading"
                  className={styles.requestReference}
                >
                  {detail.acknowledgementReference ?? "Lease interest request"}
                </h2>
              </div>
              <StatusBadge tone={leaseInterestStatusBadgeTone(detail.status)}>
                {leaseInterestStatusLabel(detail.status)}
              </StatusBadge>
            </div>
            <p className={styles.requestOutcome}>
              {leaseInterestApplicantOutcome(detail.status)}
            </p>
            <dl className={styles.requestFacts}>
              <div>
                <dt>Current status</dt>
                <dd>{leaseInterestStatusLabel(detail.status)}</dd>
              </div>
              {purpose ? (
                <div>
                  <dt>Requested purpose</dt>
                  <dd>{purpose}</dd>
                </div>
              ) : null}
              {detail.proposal.requestedDurationYears != null ? (
                <div>
                  <dt>Requested term</dt>
                  <dd>{detail.proposal.requestedDurationYears} years</dd>
                </div>
              ) : null}
              {detail.submittedAtUtc ? (
                <div>
                  <dt>Submitted</dt>
                  <dd>{formatApplicantDateTime(detail.submittedAtUtc)}</dd>
                </div>
              ) : null}
            </dl>
          </section>

          <ApplicantOfficerEvaluation
            state={valuationState}
            onRetry={() => setValuationAttempt((n) => n + 1)}
          />

          {detail.latestAuthorityDecision ? (
            <ApplicantAuthorityDecision
              decision={detail.latestAuthorityDecision}
              statusCode={detail.status}
            />
          ) : null}

          <section
            className={styles.timelinePanel}
            aria-labelledby="request-timeline-heading"
          >
            <p className={styles.requestEyebrow}>History</p>
            <h2 id="request-timeline-heading" className={styles.detailSectionTitle}>
              Activity timeline
            </h2>
            <ol className={styles.detailTimeline}>
              {detail.statusHistory.map((entry, index) => (
                <li key={`${entry.changedAtUtc}-${index}`}>
                  <span className={styles.timelineMarker} aria-hidden="true" />
                  <div className={styles.timelineEntry}>
                    <div className={styles.timelineEntryTop}>
                      <strong>{leaseInterestStatusLabel(entry.status)}</strong>
                      <time dateTime={entry.changedAtUtc}>
                        {formatApplicantDateTime(entry.changedAtUtc)}
                      </time>
                    </div>
                    {entry.note ? <p>{entry.note}</p> : null}
                  </div>
                </li>
              ))}
            </ol>
          </section>

          <div className={styles.detailActions}>
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
