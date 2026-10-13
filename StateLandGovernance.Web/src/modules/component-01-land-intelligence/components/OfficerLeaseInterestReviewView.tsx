"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import {
  getOfficerLeaseInterestRequest,
  updateOfficerLeaseInterestStatus,
} from "../services/officerLeaseInterestApi";
import type {
  LeaseInterestOfficerRequestDetail,
  LeaseInterestRequestStatus,
} from "../types/landIntelligence";
import { landUseTypeLabels } from "../utils/enumMappings";
import {
  leaseInterestApplicantOutcome,
  leaseInterestStatusLabel,
} from "../utils/leaseInterestStatus";
import styles from "./OfficerLeaseInterestLookup.module.css";

const REVIEW_STATUSES: LeaseInterestRequestStatus[] = [4, 5, 6];

export function OfficerLeaseInterestReviewView({
  requestId,
}: {
  requestId: string;
}) {
  const [detail, setDetail] = useState<LeaseInterestOfficerRequestDetail | null>(
    null,
  );
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [note, setNote] = useState("");
  const [newStatus, setNewStatus] = useState<LeaseInterestRequestStatus>(4);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await getOfficerLeaseInterestRequest(requestId);
      setDetail(res);
      setNewStatus(res.status === 4 ? 4 : 4);
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.detail || err.title);
      } else {
        setError("Unable to load request for review.");
      }
    } finally {
      setLoading(false);
    }
  }, [requestId]);

  useEffect(() => {
    void load();
  }, [load]);

  async function handleStatusUpdate() {
    if (!detail || saving || !note.trim()) {
      return;
    }
    setSaving(true);
    setError(null);
    try {
      const updated = await updateOfficerLeaseInterestStatus(requestId, {
        newStatus,
        note: note.trim(),
        visibleToApplicant: true,
      });
      setDetail(updated);
      setNote("");
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.errors?.join(" ") || err.detail || err.title);
      } else {
        setError("Status update failed.");
      }
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return <LoadingState message="Loading request for officer review…" />;
  }

  if (error && !detail) {
    return <ErrorMessage message={error} />;
  }

  if (!detail) {
    return null;
  }

  const canReview = detail.status === 3 || detail.status === 4;

  return (
    <div className={styles.layout}>
      <div className={styles.demoLabel} role="status">
        Preliminary review only — officer actions do not create a lease or
        reserve land. Applicants see visible notes in My Requests.
      </div>

      {error ? <ErrorMessage message={error} /> : null}

      <section className={styles.results}>
        <h2 style={{ marginTop: 0 }}>
          {detail.acknowledgementReference ?? "Lease interest request"}
        </h2>
        <p className={styles.resultSummary}>
          Status: {leaseInterestStatusLabel(detail.status)} ·{" "}
          {detail.cadastralNumber ?? "Cadastral unknown"} ·{" "}
          {detail.district ?? "District unknown"}
        </p>
        <p className={styles.hint}>{detail.applicantOutcomeSummary}</p>

        <p className={styles.hint}>
          Applicant: {detail.applicant.legalName} · Purpose:{" "}
          {landUseTypeLabels[detail.proposal.requestedPurpose] ?? "Unknown"} ·{" "}
          {detail.proposal.requestedDurationYears} years requested (not approved
          term)
        </p>

        <h3>Audit trail</h3>
        <ul className={styles.hint}>
          {detail.statusHistory.map((entry, index) => (
            <li key={`${entry.changedAtUtc}-${index}`}>
              <strong>{leaseInterestStatusLabel(entry.status)}</strong> —{" "}
              {new Date(entry.changedAtUtc).toLocaleString()}
              {entry.note ? `: ${entry.note}` : ""}
              {entry.visibleToApplicant === false ? " (internal)" : ""}
            </li>
          ))}
        </ul>

        <div className={styles.pager}>
          <Link
            href={`/land-intelligence/parcels/${detail.landParcelId}`}
            className={styles.rowLink}
          >
            Parcel intelligence
          </Link>
          <Link
            href="/land-intelligence/officer/lease-interest-lookup"
            className={styles.rowLink}
          >
            Back to NIC lookup
          </Link>
        </div>

        {canReview ? (
          <form
            className={styles.searchForm}
            onSubmit={(event) => {
              event.preventDefault();
              void handleStatusUpdate();
            }}
          >
            <div className={styles.field}>
              <label htmlFor="officer-new-status">Officer outcome</label>
              <select
                id="officer-new-status"
                className={styles.input}
                value={newStatus}
                onChange={(e) =>
                  setNewStatus(Number(e.target.value) as LeaseInterestRequestStatus)
                }
              >
                {REVIEW_STATUSES.map((status) => (
                  <option key={status} value={status}>
                    {leaseInterestStatusLabel(status)} —{" "}
                    {leaseInterestApplicantOutcome(status)}
                  </option>
                ))}
                {detail.status === 4 ? (
                  <option value={3}>
                    {leaseInterestStatusLabel(3)} — return to verification queue
                  </option>
                ) : null}
              </select>
            </div>
            <div className={styles.field}>
              <label htmlFor="officer-note">Note to applicant (required)</label>
              <textarea
                id="officer-note"
                className={styles.input}
                rows={3}
                value={note}
                onChange={(e) => setNote(e.target.value)}
                placeholder="Explain what the applicant should expect next. Visible in their status timeline."
              />
            </div>
            <Button
              type="submit"
              variant="primary"
              disabled={saving || !note.trim()}
            >
              {saving ? "Saving…" : "Record officer decision"}
            </Button>
          </form>
        ) : (
          <p className={styles.hint} role="status">
            This request is in a closed review state ({leaseInterestStatusLabel(detail.status)}).
            No further officer transitions are available in the pilot.
          </p>
        )}
      </section>
    </div>
  );
}
