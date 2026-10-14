"use client";

import Link from "next/link";
import { useState } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { lookupLeaseInterestByNic } from "../services/officerLeaseInterestApi";
import type { LeaseInterestOfficerLookupItem } from "../types/landIntelligence";
import { landUseTypeLabels } from "../utils/enumMappings";
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

export function OfficerLeaseInterestLookupView() {
  const [nicInput, setNicInput] = useState("");
  const [page, setPage] = useState(1);
  const [items, setItems] = useState<LeaseInterestOfficerLookupItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [searched, setSearched] = useState(false);

  async function runSearch(targetPage: number) {
    setLoading(true);
    setError(null);
    try {
      const response = await lookupLeaseInterestByNic({
        nationalIdentityCardNumber: nicInput.trim(),
        page: targetPage,
        pageSize: PAGE_SIZE,
      });
      setItems(response.items);
      setTotalCount(response.totalCount);
      setPage(response.page);
      setSearched(true);
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
        setError("Lookup failed. Try again.");
      }
    } finally {
      setLoading(false);
    }
  }

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <div className={styles.layout}>
      <p className={styles.hint}>
        <Link
          href="/land-intelligence/officer/lease-interest-queue"
          className={styles.rowLink}
        >
          Open verification queue
        </Link>
      </p>

      <form
        className={styles.searchForm}
        onSubmit={(event) => {
          event.preventDefault();
          void runSearch(1);
        }}
      >
        <div className={styles.field}>
          <label htmlFor="officer-nic">National Identity Card (NIC) number</label>
          <input
            id="officer-nic"
            className={styles.input}
            value={nicInput}
            onChange={(e) => setNicInput(e.target.value)}
            autoComplete="off"
            spellCheck={false}
            placeholder="9 digits + V/X or 12-digit new NIC"
          />
          <p className={styles.hint}>
            Exact match after server normalization. NIC is sent in the request body
            only — never in the URL.
          </p>
        </div>
        <Button type="submit" variant="primary" disabled={loading || !nicInput.trim()}>
          {loading ? "Searching…" : "Search requests"}
        </Button>
      </form>

      {error ? <ErrorMessage message={error} /> : null}

      {searched && !error ? (
        <div className={styles.results}>
          <p className={styles.resultSummary}>
            {totalCount === 0
              ? "No submitted lease-interest requests matched this NIC."
              : `${totalCount} request${totalCount === 1 ? "" : "s"} matched (page ${page} of ${totalPages}).`}
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
                    <th>Purpose</th>
                    <th>Duration (yrs)</th>
                    <th>NIC</th>
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
                      <td>{row.maskedNationalIdentityCardNumber}</td>
                      <td>
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
                onClick={() => void runSearch(page - 1)}
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
                onClick={() => void runSearch(page + 1)}
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
