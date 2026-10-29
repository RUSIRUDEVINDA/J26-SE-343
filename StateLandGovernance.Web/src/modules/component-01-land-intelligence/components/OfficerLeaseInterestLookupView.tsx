"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import { lookupLeaseInterestByNic } from "../services/officerLeaseInterestApi";
import { buildOfficerLeaseInterestLookupRequest } from "../utils/leaseInterestOfficerLookup";
import type { LeaseInterestOfficerLookupItem } from "../types/landIntelligence";
import { landUseTypeLabels } from "../utils/enumMappings";
import { officerLeaseInterestLookupViewHref } from "../utils/officerLeaseInterestNavigation";
import { leaseInterestStatusLabel } from "../utils/leaseInterestStatus";
import styles from "./OfficerLeaseInterestLookup.module.css";

const PAGE_SIZES = [10, 20, 30] as const;
type PageSize = (typeof PAGE_SIZES)[number];

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

function maskIdentifier(value: string | null | undefined): string {
  const trimmed = value?.trim();
  if (!trimmed) {
    return "—";
  }
  if (trimmed.length <= 4) {
    return "*".repeat(trimmed.length);
  }
  return `${"*".repeat(trimmed.length - 4)}${trimmed.slice(-4)}`;
}

function applicantIdentifier(
  row: LeaseInterestOfficerLookupItem,
  identifiersRevealed: boolean,
): string {
  if (!identifiersRevealed) {
    return row.applicantKind === 2
      ? maskIdentifier(row.businessRegistrationNumber)
      : maskIdentifier(row.nationalIdentityCardNumber);
  }
  return row.applicantKind === 2
    ? row.businessRegistrationNumber ?? "—"
    : row.nationalIdentityCardNumber ?? "—";
}

export function OfficerLeaseInterestLookupView() {
  const [identifierInput, setIdentifierInput] = useState("");
  const [activeFilter, setActiveFilter] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<PageSize>(10);
  const [items, setItems] = useState<LeaseInterestOfficerLookupItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [loaded, setLoaded] = useState(false);

  const loadPage = useCallback(
    async (targetPage: number, identifier: string, size: PageSize) => {
      setLoading(true);
      setError(null);
      try {
        const response = await lookupLeaseInterestByNic(
          buildOfficerLeaseInterestLookupRequest(identifier, targetPage, size),
        );
        setItems(response.items);
        setTotalCount(response.totalCount);
        setPage(response.page);
        setPageSize(response.pageSize as PageSize);
        setActiveFilter(identifier.trim());
        setLoaded(true);
      } catch (err) {
        setItems([]);
        setTotalCount(0);
        if (err instanceof ApiClientError && err.status === 404) {
          setError(
            "Officer lookup API was not found (HTTP 404). Stop StateLandGovernance.Api, rebuild from your current branch, and start it again. Then open GET /api/v1/land/research-demo/config — it should include officerLeaseInterestNicLookupEnabled.",
          );
        } else if (err instanceof ApiClientError && err.status === 403) {
          setError(
            "Officer NIC lookup is not available in this environment. Enable Land Intelligence research demo in Development or Testing only.",
          );
        } else if (err instanceof ApiClientError) {
          setError(
            err.errors?.length
              ? err.errors.join(" ")
              : err.message || `Request failed (HTTP ${err.status}).`,
          );
        } else {
          setError("Unable to load lease-interest requests. Try again.");
        }
      } finally {
        setLoading(false);
      }
    },
    [],
  );

  useEffect(() => {
    void loadPage(1, "", 10);
  }, [loadPage]);

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const filterActive = activeFilter.length > 0;
  const showEmptyResults =
    loaded && !error && !loading && totalCount === 0;

  return (
    <div className={styles.layoutFullWidth}>
      <form
        className={styles.searchForm}
        onSubmit={(event) => {
          event.preventDefault();
          void loadPage(1, identifierInput, pageSize);
        }}
      >
        <div className={styles.field}>
          <label htmlFor="officer-lookup-id">
            NIC or business registration number (BRN)
          </label>
          <input
            id="officer-lookup-id"
            className={styles.input}
            value={identifierInput}
            onChange={(e) => setIdentifierInput(e.target.value)}
            autoComplete="off"
            spellCheck={false}
            placeholder="Optional — filter by NIC or BRN"
          />
        </div>
        <div className={styles.searchActions}>
          <Button type="submit" variant="primary" disabled={loading}>
            {loading ? "Loading…" : filterActive ? "Search" : "Apply filter"}
          </Button>
          <Button
            type="button"
            variant="secondary"
            disabled={loading || !identifierInput.trim()}
            onClick={() => {
              setIdentifierInput("");
              void loadPage(1, "", pageSize);
            }}
          >
            Clear
          </Button>
        </div>
      </form>

      {loading && items.length === 0 ? (
        <LoadingState message="Loading submitted requests…" />
      ) : null}

      {error ? <ErrorMessage message={error} /> : null}

      {showEmptyResults ? (
        <p className={styles.emptyMessage} role="status">
          No matching requests found.
          {filterActive
            ? " Check the NIC or BRN and try again, or clear the filter to see all submitted requests."
            : " Submitted lease-interest requests will appear here when applicants submit them."}
        </p>
      ) : null}

      {!error && totalCount > 0 ? (
        <div className={styles.results}>
          <div className={styles.listToolbar}>
            <p className={styles.resultSummary}>
              {filterActive
                ? `${totalCount} request${totalCount === 1 ? "" : "s"} matched`
                : `${totalCount} submitted request${totalCount === 1 ? "" : "s"}`}
              {" · "}
              page {page} of {totalPages}
            </p>
            <label className={styles.pageSizeControl}>
              <span>Show</span>
              <select
                className={styles.pageSizeSelect}
                value={pageSize}
                disabled={loading}
                onChange={(event) => {
                  const next = Number(event.target.value) as PageSize;
                  setPageSize(next);
                  void loadPage(1, identifierInput, next);
                }}
              >
                {PAGE_SIZES.map((size) => (
                  <option key={size} value={size}>
                    {size}
                  </option>
                ))}
              </select>
              <span>per page</span>
            </label>
          </div>

          <div className={styles.tableWrap}>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th>Reference</th>
                  <th>Submitted</th>
                  <th>Status</th>
                  <th>Cadastral</th>
                  <th>District</th>
                  <th>Purpose</th>
                  <th>Duration (yrs)</th>
                  <th>NIC / BRN</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {items.map((row) => (
                  <tr key={row.requestId}>
                    <td>{row.acknowledgementReference ?? "—"}</td>
                    <td>{formatDate(row.submittedAtUtc)}</td>
                    <td>{leaseInterestStatusLabel(row.status)}</td>
                    <td className={styles.mono}>{row.cadastralNumber ?? "—"}</td>
                    <td>{row.district ?? "—"}</td>
                    <td>
                      {landUseTypeLabels[row.requestedPurpose] ?? "Unknown"}
                    </td>
                    <td>{row.requestedDurationYears ?? "—"}</td>
                    <td className={styles.mono}>
                      {applicantIdentifier(row, filterActive)}
                    </td>
                    <td>
                      <div className={styles.rowActions}>
                        <Link
                          href={officerLeaseInterestLookupViewHref(row.requestId)}
                          className={styles.rowLink}
                        >
                          View request
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

          <div className={styles.pager}>
            <Button
              type="button"
              variant="secondary"
              disabled={loading || page <= 1}
              onClick={() => void loadPage(page - 1, identifierInput, pageSize)}
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
              onClick={() => void loadPage(page + 1, identifierInput, pageSize)}
            >
              Next
            </Button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
