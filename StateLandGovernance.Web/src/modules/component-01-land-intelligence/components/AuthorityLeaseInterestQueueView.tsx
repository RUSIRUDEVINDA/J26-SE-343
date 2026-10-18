"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";
import type { KeyboardEvent } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import { StatusBadge } from "@/shared/components/StatusBadge";
import { listAuthorityLeaseInterestQueue } from "../services/authorityLeaseInterestApi";
import type { LeaseInterestAuthorityQueueItem } from "../types/landIntelligence";
import {
  AUTHORITY_QUEUE_FILTERS,
  authorityQueueRowActionLabel,
  authorityQueueSummary,
  DEFAULT_AUTHORITY_QUEUE_FILTER,
  type AuthorityQueueFilter,
} from "../utils/authorityQueueFilter";
import {
  leaseInterestStatusBadgeTone,
  leaseInterestStatusLabel,
} from "../utils/leaseInterestStatus";
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

export function AuthorityLeaseInterestQueueView() {
  const [filter, setFilter] = useState<AuthorityQueueFilter>(
    DEFAULT_AUTHORITY_QUEUE_FILTER,
  );
  const [page, setPage] = useState(1);
  const [items, setItems] = useState<LeaseInterestAuthorityQueueItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const requestSeq = useRef(0);
  const tabRefs = useRef<Record<string, HTMLButtonElement | null>>({});

  const loadPage = useCallback(
    async (targetPage: number, targetFilter: AuthorityQueueFilter) => {
      const seq = ++requestSeq.current;
      setLoading(true);
      setError(null);
      try {
        const response = await listAuthorityLeaseInterestQueue(
          targetPage,
          PAGE_SIZE,
          targetFilter,
        );
        if (seq !== requestSeq.current) {
          return;
        }
        setItems(response.items);
        setTotalCount(response.totalCount);
        setPage(response.page);
      } catch (err) {
        if (seq !== requestSeq.current) {
          return;
        }
        setItems([]);
        setTotalCount(0);
        if (err instanceof ApiClientError && err.status === 403) {
          setError(
            "Authority review queue is not available in this environment. Enable Land Intelligence research demo in Development or Testing only.",
          );
        } else if (err instanceof ApiClientError) {
          setError(
            err.errors?.length
              ? err.errors.join(" ")
              : err.message || `Request failed (HTTP ${err.status}).`,
          );
        } else {
          setError("Unable to load authority review queue.");
        }
      } finally {
        if (seq === requestSeq.current) {
          setLoading(false);
        }
      }
    },
    [],
  );

  useEffect(() => {
    void loadPage(1, filter);
  }, [loadPage, filter]);

  function selectFilter(next: AuthorityQueueFilter) {
    if (next === filter) {
      return;
    }
    setItems([]);
    setTotalCount(0);
    setPage(1);
    setFilter(next);
  }

  function onTabKeyDown(event: KeyboardEvent<HTMLButtonElement>) {
    const index = AUTHORITY_QUEUE_FILTERS.findIndex(
      (option) => option.value === filter,
    );
    let nextIndex = index;
    if (event.key === "ArrowRight") {
      nextIndex = (index + 1) % AUTHORITY_QUEUE_FILTERS.length;
    } else if (event.key === "ArrowLeft") {
      nextIndex =
        (index - 1 + AUTHORITY_QUEUE_FILTERS.length) %
        AUTHORITY_QUEUE_FILTERS.length;
    } else if (event.key === "Home") {
      nextIndex = 0;
    } else if (event.key === "End") {
      nextIndex = AUTHORITY_QUEUE_FILTERS.length - 1;
    } else {
      return;
    }
    event.preventDefault();
    const next = AUTHORITY_QUEUE_FILTERS[nextIndex].value;
    selectFilter(next);
    tabRefs.current[next]?.focus();
  }

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));
  const panelId = "authority-queue-panel";

  return (
    <div className={styles.layout}>
      <div className={styles.demoLabel} role="status">
        Research demo — authority review after officer valuation. Decisions are
        preliminary project outcomes only and do not create a lease or reserve
        land.
      </div>

      <div
        className={styles.tabList}
        role="tablist"
        aria-label="Authority queue status"
      >
        {AUTHORITY_QUEUE_FILTERS.map((option) => {
          const selected = option.value === filter;
          return (
            <button
              key={option.value}
              ref={(node) => {
                tabRefs.current[option.value] = node;
              }}
              type="button"
              role="tab"
              id={`authority-queue-tab-${option.value}`}
              aria-selected={selected}
              aria-controls={panelId}
              tabIndex={selected ? 0 : -1}
              className={`${styles.tab} ${selected ? styles.tabActive : ""}`}
              onClick={() => selectFilter(option.value)}
              onKeyDown={onTabKeyDown}
            >
              {option.label}
            </button>
          );
        })}
      </div>

      <div
        id={panelId}
        role="tabpanel"
        aria-labelledby={`authority-queue-tab-${filter}`}
        aria-busy={loading}
        className={styles.results}
      >
        {loading && items.length === 0 ? (
          <LoadingState message="Loading authority review queue…" />
        ) : null}
        {error ? <ErrorMessage message={error} /> : null}

        {!error && (items.length > 0 || (!loading && totalCount === 0)) ? (
          <>
            <p className={styles.resultSummary} role="status">
              {authorityQueueSummary(filter, totalCount, page, totalPages)}
            </p>

            {items.length > 0 ? (
              <div className={styles.tableWrap}>
                <table className={styles.table}>
                  <thead>
                    <tr>
                      <th scope="col">Reference</th>
                      <th scope="col">Valuation</th>
                      <th scope="col">Status</th>
                      <th scope="col">Cadastral</th>
                      <th scope="col">District</th>
                      <th scope="col">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {items.map((row) => {
                      const action = authorityQueueRowActionLabel(row.status);
                      return (
                        <tr key={row.requestId}>
                          <td>{row.acknowledgementReference ?? "—"}</td>
                          <td>
                            Rev {row.latestValuationRevision}
                            <br />
                            <span className={styles.mono}>
                              {formatDate(row.latestValuationAtUtc)}
                            </span>
                          </td>
                          <td>
                            <StatusBadge
                              tone={leaseInterestStatusBadgeTone(row.status)}
                            >
                              {leaseInterestStatusLabel(row.status)}
                            </StatusBadge>
                          </td>
                          <td className={styles.mono}>
                            {row.cadastralNumber ?? "—"}
                          </td>
                          <td>{row.district ?? "—"}</td>
                          <td>
                            <Link
                              href={`/land-intelligence/authority/lease-interest-review/${row.requestId}`}
                              className={styles.rowLink}
                              aria-label={`${action} request ${row.acknowledgementReference ?? row.requestId}`}
                            >
                              {action}
                            </Link>
                          </td>
                        </tr>
                      );
                    })}
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
                  onClick={() => void loadPage(page - 1, filter)}
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
                  onClick={() => void loadPage(page + 1, filter)}
                >
                  Next
                </Button>
              </div>
            ) : null}
          </>
        ) : null}
      </div>
    </div>
  );
}
