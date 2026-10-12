"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import { PageHeading } from "@/shared/components/PageLayout";
import { getLeaseInterestRequest } from "@/modules/component-01-land-intelligence/services/citizenWorkspaceApi";
import type { LeaseInterestRequestDetail } from "@/modules/component-01-land-intelligence/types/landIntelligence";
import { landUseTypeLabels } from "@/modules/component-01-land-intelligence/utils/enumMappings";
import {
  leaseInterestApplicantOutcome,
  leaseInterestStatusLabel,
} from "@/modules/component-01-land-intelligence/utils/leaseInterestStatus";
import styles from "../MyRequests.module.css";

export default function MyRequestDetailPage() {
  const params = useParams<{ requestId: string }>();
  const [detail, setDetail] = useState<LeaseInterestRequestDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    getLeaseInterestRequest(params.requestId)
      .then((res) => {
        if (!cancelled) {
          setDetail(res);
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setError(
            err instanceof ApiClientError
              ? err.detail || err.title
              : "Unable to load request.",
          );
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [params.requestId]);

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
            {detail.acknowledgementReference ?? detail.id}
          </p>
          <p className={styles.statusLine}>
            {leaseInterestStatusLabel(detail.status)}
          </p>
          <p className={styles.outcome}>
            {leaseInterestApplicantOutcome(detail.status)}
          </p>
          <p className={styles.meta}>
            Purpose:{" "}
            {landUseTypeLabels[detail.proposal.requestedPurpose] ?? "Unknown"}
            {detail.proposal.requestedDurationYears != null
              ? ` · ${detail.proposal.requestedDurationYears} years requested`
              : ""}
          </p>
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
          <div className={styles.actions}>
            <Button
              href={`/land-intelligence/parcels/${detail.landParcelId}/lease-interest`}
              variant="secondary"
            >
              Open parcel form
            </Button>
            <Button href="/land-intelligence/my-requests" variant="ghost">
              Back to list
            </Button>
          </div>
        </div>
      ) : null}
    </>
  );
}
