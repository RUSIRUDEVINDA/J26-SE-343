"use client";

import { useEffect, useMemo, useState } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { EmptyState } from "@/shared/components/EmptyState";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import { PageHeading } from "@/shared/components/PageLayout";
import { StatusBadge } from "@/shared/components/StatusBadge";
import { listMyLeaseInterestRequests } from "@/modules/component-01-land-intelligence/services/citizenWorkspaceApi";
import type { LeaseInterestRequestSummary } from "@/modules/component-01-land-intelligence/types/landIntelligence";
import { leaseInterestPurposeDisplay } from "@/modules/component-01-land-intelligence/utils/leaseInterestPurposeDisplay";
import {
  isLeaseInterestDraftStatus,
  leaseInterestStatusBadgeTone,
  leaseInterestStatusLabel,
} from "@/modules/component-01-land-intelligence/utils/leaseInterestStatus";
import styles from "./MyRequests.module.css";

function formatShortDate(iso: string | null | undefined): string {
  if (!iso) {
    return "—";
  }
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return "—";
  }
  return date.toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

function RequestRow({ item }: { item: LeaseInterestRequestSummary }) {
  const isDraft = isLeaseInterestDraftStatus(item.status);
  const purpose = leaseInterestPurposeDisplay(
    item.requestedPurpose,
    item.status,
  );
  const reference =
    item.acknowledgementReference ??
    (isDraft ? `Draft · ${item.id.slice(0, 8)}` : item.id.slice(0, 8));
  const parcelLabel = item.cadastralNumber?.trim() || item.landParcelId.slice(0, 8);

  return (
    <li className={styles.row}>
      <div className={styles.rowMain}>
        <div className={styles.rowPrimary}>
          <p className={styles.parcelId}>
            <span className={styles.srOnly}>Parcel </span>
            {parcelLabel}
          </p>
          <StatusBadge tone={leaseInterestStatusBadgeTone(item.status)}>
            {leaseInterestStatusLabel(item.status)}
          </StatusBadge>
        </div>
        <dl className={styles.rowMeta}>
          <div>
            <dt>Reference</dt>
            <dd className={styles.mono}>{reference}</dd>
          </div>
          {purpose ? (
            <div>
              <dt>Purpose</dt>
              <dd>{purpose}</dd>
            </div>
          ) : null}
          <div>
            <dt>{isDraft ? "Updated" : "Submitted"}</dt>
            <dd>
              {formatShortDate(
                isDraft ? item.updatedAtUtc : (item.submittedAtUtc ?? item.updatedAtUtc),
              )}
            </dd>
          </div>
        </dl>
      </div>
      <div className={styles.rowActions}>
        {isDraft ? (
          <Button
            href={`/land-intelligence/parcels/${item.landParcelId}/lease-interest`}
            variant="primary"
          >
            Continue draft
          </Button>
        ) : (
          <Button
            href={`/land-intelligence/my-requests/${item.id}`}
            variant="primary"
          >
            View status
          </Button>
        )}
      </div>
    </li>
  );
}

function RequestSection({
  title,
  items,
  emptyLabel,
}: {
  title: string;
  items: LeaseInterestRequestSummary[];
  emptyLabel: string;
}) {
  return (
    <section className={styles.section} aria-labelledby={`${title}-heading`}>
      <div className={styles.sectionHeader}>
        <h2 id={`${title}-heading`} className={styles.sectionTitle}>
          {title}
        </h2>
        <span className={styles.count}>{items.length}</span>
      </div>
      {items.length === 0 ? (
        <p className={styles.sectionEmpty}>{emptyLabel}</p>
      ) : (
        <ul className={styles.list}>
          {items.map((item) => (
            <RequestRow key={item.id} item={item} />
          ))}
        </ul>
      )}
    </section>
  );
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

  const { drafts, submitted } = useMemo(() => {
    const draftRows: LeaseInterestRequestSummary[] = [];
    const submittedRows: LeaseInterestRequestSummary[] = [];
    for (const item of items) {
      if (isLeaseInterestDraftStatus(item.status)) {
        draftRows.push(item);
      } else {
        submittedRows.push(item);
      }
    }
    return { drafts: draftRows, submitted: submittedRows };
  }, [items]);

  return (
    <>
      <PageHeading
        title="My preliminary interest requests"
        subtitle="Track drafts and preliminary assessment status. Submission does not reserve land."
      />
      {loading ? <LoadingState message="Loading requests…" /> : null}
      {error ? (
        <div className={styles.errorBlock} role="alert">
          <ErrorMessage message={error} />
          <Button
            type="button"
            variant="secondary"
            onClick={() => window.location.reload()}
          >
            Try again
          </Button>
        </div>
      ) : null}
      {!loading && !error && items.length === 0 ? (
        <EmptyState
          title="No requests yet"
          description="Express preliminary interest from a saved parcel or parcel intelligence record. Opening a parcel form alone does not create a draft."
          action={
            <Button href="/land-intelligence/parcels" variant="primary">
              Browse parcels
            </Button>
          }
        />
      ) : null}
      {!loading && !error && items.length > 0 ? (
        <div className={styles.sections}>
          <RequestSection
            title="Drafts"
            items={drafts}
            emptyLabel="No drafts in progress."
          />
          <RequestSection
            title="Submitted"
            items={submitted}
            emptyLabel="No submitted requests yet."
          />
        </div>
      ) : null}
    </>
  );
}
