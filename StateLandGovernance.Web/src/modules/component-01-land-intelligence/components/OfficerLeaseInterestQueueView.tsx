"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import { listOfficerLeaseInterestQueue } from "../services/officerLeaseInterestApi";
import type { LeaseInterestOfficerQueueItem } from "../types/landIntelligence";
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

export function OfficerLeaseInterestQueueView() {
  const [page, setPage] = useState(1);
  const [items, setItems] = useState<LeaseInterestOfficerQueueItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadPage = useCallback(async (targetPage: number) => {
    setLoading(true);
    setError(null);
    try {
      const response = await listOfficerLeaseInterestQueue(
        targetPage,
        PAGE_SIZE,
      );
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
        setError("Unable to load verification queue.");
      }
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadPage(1);
  }, [loadPage]);

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <div className={styles.layout}>

      {loading && items.length === 0 ? (
        <LoadingState message="Loading verification queue…" />
      ) : null}
      {error ? <ErrorMessage message={error} /> : null}

      {!error && (items.length > 0 || (!loading && totalCount === 0)) ? (
        <div className={styles.results}>
          <p className={styles.resultSummary}>
            {totalCount === 0
              ? "No requests are in the officer verification queue."
              : `${totalCount} request${totalCount === 1 ? "" : "s"} in queue (page ${page} of ${totalPages}).`}
          </p>

          {items.length > 0 ? (
            <div className={styles.tableWrap}>
              <table className={styles.table}>
                <thead>
                  <tr>
                    <th>Reference</th>
                    <th>Submitted</th>
                    <th>Status</th>
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
                      <td className={styles.mono}>
                        {row.cadastralNumber ?? "—"}
                      </td>
                      <td>{row.district ?? "—"}</td>
                      <td>
                        <div className={styles.rowActions}>
                          <Link
                            href={`/land-intelligence/officer/lease-interest-review/${row.requestId}`}
                            className={styles.rowLink}
                          >
                            Review
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
