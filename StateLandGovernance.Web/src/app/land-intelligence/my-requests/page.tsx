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

function statusText(status: number): string {
  switch (status) {
    case 1:
      return "Draft";
    case 3:
      return "Pending verification";
    case 4:
      return "Needs information";
    case 5:
      return "Declined";
    case 6:
      return "Referred to authority";
    default:
      return "Submitted";
  }
}

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
        <ul className="stack">
          {items.map((item) => (
            <li
              key={item.id}
              className="card"
              style={{ padding: "1rem", listStyle: "none" }}
            >
              <p style={{ margin: 0, fontWeight: 700 }}>
                {item.acknowledgementReference ?? `Draft ${item.id.slice(0, 8)}`}
              </p>
              <p style={{ margin: "0.35rem 0", fontSize: "0.85rem" }}>
                Status: {statusText(item.status)} · Purpose:{" "}
                {landUseTypeLabels[item.requestedPurpose] ?? "Unknown"}
                {item.requestedDurationYears != null
                  ? ` · ${item.requestedDurationYears} years requested`
                  : ""}
              </p>
              <Button
                href={`/land-intelligence/parcels/${item.landParcelId}/lease-interest`}
                variant="secondary"
              >
                View parcel / form
              </Button>
            </li>
          ))}
        </ul>
      ) : null}
    </>
  );
}
