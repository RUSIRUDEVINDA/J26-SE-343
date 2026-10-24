"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import {
  listOfficerLeaseInterestQueue,
  listOfficerLeaseInterestValuationQueue,
} from "../services/officerLeaseInterestApi";
import type { LeaseInterestOfficerQueueItem } from "../types/landIntelligence";
import {
  officerLeaseInterestReviewHref,
  officerLeaseInterestReviewLinkLabel,
} from "../utils/officerLeaseInterestNavigation";
import { leaseInterestStatusLabel } from "../utils/leaseInterestStatus";
import styles from "./OfficerLeaseInterestLookup.module.css";

const PAGE_SIZE = 20;

function formatDate(iso: string | null | undefined): string {
  if (!iso) {
    return "—";
  }
  try {
    return new Date(iso).toLocaleString();
  } catch {
    return iso;
  }
}

function applicantIdentifier(
  row: LeaseInterestOfficerQueueItem,
): string {
  return row.applicantKind === 2
    ? row.businessRegistrationNumber ?? "—"
    : row.nationalIdentityCardNumber ?? "—";
}

export function OfficerLeaseInterestQueueView({
  queueKind = "verification",
}: {
  queueKind?: "verification" | "valuation";
}) {
  const [page, setPage] = useState(1);
  const [items, setItems] = useState<LeaseInterestOfficerQueueItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadPage = useCallback(async (targetPage: number) => {
    setLoading(true);
    setError(null);
    try {
      const response =
        queueKind === "valuation"
          ? await listOfficerLeaseInterestValuationQueue(targetPage, PAGE_SIZE)
          : await listOfficerLeaseInterestQueue(targetPage, PAGE_SIZE);
      setItems(response.items);
      setTotalCount(response.totalCount);
      setPage(response.page);
    } catch (err) {
      setItems([]);
      setTotalCount(0);
      if (err instanceof ApiClientError && err.status === 404) {
        setError(
          "Officer queue API was not found (HTTP 404). Rebuild and restart the API from your current branch.",
        );
      } else if (err instanceof ApiClientError && err.status === 403) {
        setError(
          "Officer review queue is not available in this environment. Enable Land Intelligence research demo in Development or Testing only.",
        );
      } else if (err instanceof ApiClientError) {
        setError(
          err.errors?.length
            ? err.errors.join(" ")
            : err.message || `Request failed (HTTP ${err.status}).`,
        );
      } else {
        setError(
          queueKind === "valuation"
            ? "Unable to load valuation queue."
            : "Unable to load verification queue.",
        );
      }
    } finally {
      setLoading(false);
    }
  }, [queueKind]);

  useEffect(() => {
    void loadPage(1);
  }, [loadPage]);

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <div className={styles.layoutFullWidth}>

      {loading && items.length === 0 ? (
        <LoadingState
          message={
            queueKind === "valuation"
              ? "Loading valuation queue…"
              : "Loading verification queue…"
          }
        />
      ) : null}
      {error ? <ErrorMessage message={error} /> : null}

      {!error && !loading && totalCount === 0 ? (
        <section className={styles.queueEmptyState} aria-labelledby="queue-empty-title">
          <div className={styles.queueEmptyIcon} aria-hidden="true">
            ✓
          </div>
          <p className={styles.queueEmptyEyebrow}>OFFICER VERIFICATION</p>
          <h2 id="queue-empty-title">
            {queueKind === "valuation"
              ? "No requests awaiting valuation"
              : "No requests to review right now"}
          </h2>
          <p className={styles.queueEmptyDescription}>
            {queueKind === "valuation"
              ? "Requests will appear here after verification is complete and before an officer valuation is recorded."
              : "New lease-interest requests awaiting officer verification will appear here. You can check again or look up an existing request by NIC or BRN."}
          </p>
          <div className={styles.queueEmptyActions}>
            <Button type="button" onClick={() => void loadPage(1)}>
              Refresh queue
            </Button>
            <Button href="/land-intelligence/officer/lease-interest-lookup" variant="secondary">
              Look up by NIC
            </Button>
          </div>
        </section>
      ) : null}

      {!error && items.length > 0 ? (
        <div className={styles.results}>
          <p className={styles.resultSummary}>
            {`${totalCount} request${totalCount === 1 ? "" : "s"} in queue (page ${page} of ${totalPages}).`}
          </p>

          {items.length > 0 ? (
            <div className={styles.tableWrap}>
              <table className={styles.table}>
                <thead>
                  <tr>
                    <th>Reference</th>
                    <th>Submitted</th>
                    <th>Status</th>
                    <th>NIC / BRN</th>
                    <th>Cadastral</th>
                    <th>District</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((row) => (
                    <tr key={row.requestId}>
                      <td>{row.acknowledgementReference ?? "—"}</td>
                      <td>{formatDate(row.submittedAtUtc)}</td>
                      <td>{leaseInterestStatusLabel(row.status)}</td>
                      <td className={styles.mono}>{applicantIdentifier(row)}</td>
                      <td className={styles.mono}>
                        {row.cadastralNumber ?? "—"}
                      </td>
                      <td>{row.district ?? "—"}</td>
                      <td>
                        <div className={styles.rowActions}>
                          <Link
                            href={officerLeaseInterestReviewHref(
                              row.requestId,
                              row.status,
                              row.hasOfficerValuation ?? false,
                            )}
                            className={styles.rowLink}
                          >
                            {officerLeaseInterestReviewLinkLabel(
                              row.status,
                              row.hasOfficerValuation ?? false,
                            )}
                          </Link>
                          <Link
                            href={`/land-intelligence/parcels/${row.landParcelId}`}
                            className={styles.rowLink}
                          >
                            Parcel
                          </Link>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}

          {totalCount > PAGE_SIZE ? (
            <div className={styles.pager}>
              <Button
                type="button"
                variant="secondary"
                disabled={loading || page <= 1}
                onClick={() => void loadPage(page - 1)}
              >
                Previous
              </Button>
              <span className={styles.pagerLabel}>
                Page {page} of {totalPages}
              </span>
              <Button
                type="button"
                variant="secondary"
                disabled={loading || page >= totalPages}
                onClick={() => void loadPage(page + 1)}
              >
                Next
              </Button>
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
