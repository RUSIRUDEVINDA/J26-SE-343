"use client";

import { useEffect, useState } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { EmptyState } from "@/shared/components/EmptyState";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import { PageHeading } from "@/shared/components/PageLayout";
import { listMyLeaseInterestRequests } from "@/modules/component-01-land-intelligence/services/citizenWorkspaceApi";
import type { LeaseInterestRequestSummary } from "@/modules/component-01-land-intelligence/types/landIntelligence";
import { landUseTypeLabels } from "@/modules/component-01-land-intelligence/utils/enumMappings";
import { leaseInterestStatusLabel } from "@/modules/component-01-land-intelligence/utils/leaseInterestStatus";
import styles from "./MyRequests.module.css";

export default function MyRequestsPage() {
  const [items, setItems] = useState<LeaseInterestRequestSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    listMyLeaseInterestRequests()
      .then((rows) => {
        if (!cancelled) {
          setItems(rows);
        }
      })
      .catch((err) => {
        if (!cancelled) {
          if (err instanceof ApiClientError) {
            setError(err.detail || err.title);
          } else {
            setError("Unable to load requests.");
          }
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
  }, []);

  return (
    <>
      <PageHeading
        title="My lease interest requests"
        subtitle="Track preliminary assessment status. Submission does not reserve land."
      />
      {loading ? <LoadingState message="Loading requests…" /> : null}
      {error ? <ErrorMessage message={error} /> : null}
      {!loading && !error && items.length === 0 ? (
        <EmptyState
          title="No requests yet"
          description="Express lease interest from a saved parcel or parcel intelligence record."
          action={
            <Button href="/land-intelligence/shortlist" variant="primary">
              Open shortlist
            </Button>
          }
        />
      ) : null}
      {!loading && !error && items.length > 0 ? (
        <ul className={styles.list}>
          {items.map((item) => (
            <li key={item.id} className={styles.card}>
              <p className={styles.title}>
                {item.acknowledgementReference ?? `Draft ${item.id.slice(0, 8)}`}
              </p>
              <p className={styles.meta}>
                Status: {leaseInterestStatusLabel(item.status)} · Purpose:{" "}
                {landUseTypeLabels[item.requestedPurpose] ?? "Unknown"}
                {item.requestedDurationYears != null
                  ? ` · ${item.requestedDurationYears} years requested`
                  : ""}
              </p>
              <div className={styles.actions}>
                <Button
                  href={`/land-intelligence/my-requests/${item.id}`}
                  variant="primary"
                >
                  View status
                </Button>
                <Button
                  href={`/land-intelligence/parcels/${item.landParcelId}/lease-interest`}
                  variant="secondary"
                >
                  Parcel form
                </Button>
              </div>
            </li>
          ))}
        </ul>
      ) : null}
    </>
  );
}
