"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import { getOfficerLeaseInterestRequest, updateOfficerLeaseInterestStatus } from "../services/officerLeaseInterestApi";
import type { LeaseInterestOfficerRequestDetail, LeaseInterestRequestStatus } from "../types/landIntelligence";
import { landUseTypeLabels } from "../utils/enumMappings";
import { leaseInterestApplicantOutcome, leaseInterestStatusLabel } from "../utils/leaseInterestStatus";
import styles from "./OfficerLeaseInterestReviewView.module.css";

const REVIEW_STATUSES: LeaseInterestRequestStatus[] = [4, 5, 6];
const formatDate = (value: string) => new Date(value).toLocaleString("en-LK", { dateStyle: "medium", timeStyle: "short" });

export function OfficerLeaseInterestReviewView({ requestId }: { requestId: string }) {
  const [detail, setDetail] = useState<LeaseInterestOfficerRequestDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [note, setNote] = useState("");
  const [newStatus, setNewStatus] = useState<LeaseInterestRequestStatus>(4);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await getOfficerLeaseInterestRequest(requestId);
      setDetail(result);
      setNewStatus(4);
    } catch (err) {
      setError(err instanceof ApiClientError ? err.detail || err.title : "Unable to load request for review.");
    } finally {
      setLoading(false);
    }
  }, [requestId]);

  useEffect(() => { void load(); }, [load]);

  async function handleStatusUpdate() {
    if (!detail || saving || !note.trim()) return;
    setSaving(true);
    setError(null);
    setSuccess(null);
    try {
      const updated = await updateOfficerLeaseInterestStatus(requestId, {
        newStatus, note: note.trim(), visibleToApplicant: true,
      });
      setDetail(updated);
      setNote("");
      setSuccess(`Decision recorded: ${leaseInterestStatusLabel(updated.status)}.`);
    } catch (err) {
      setError(err instanceof ApiClientError ? err.errors?.join(" ") || err.detail || err.title : "Status update failed.");
    } finally {
      setSaving(false);
    }
  }

  if (loading) return <LoadingState message="Loading request for officer review…" />;
  if (error && !detail) return <ErrorMessage message={error} />;
  if (!detail) return null;

  const canReview = detail.status === 3 || detail.status === 4;
  const outcomeOptions = detail.status === 4 ? [...REVIEW_STATUSES, 3 as const] : REVIEW_STATUSES;

  return (
    <div className={styles.page}>
      <Link href="/land-intelligence/officer/lease-interest-lookup" className={styles.backLink}>← Back to NIC lookup</Link>
      <header className={styles.caseHeader}>
        <div>
          <p className={styles.eyebrow}>Lease interest request</p>
          <h2 className={styles.reference}>{detail.acknowledgementReference ?? "Lease interest request"}</h2>
          <p className={styles.headerText}>{detail.applicantOutcomeSummary}</p>
        </div>
        <span className={`${styles.statusBadge} ${styles[`status${detail.status}`]}`}>{leaseInterestStatusLabel(detail.status)}</span>
      </header>
      <p className={styles.notice}><strong>Preliminary review only.</strong> This decision does not reserve land or approve a lease. The applicant can see the note you record below.</p>
      {error ? <ErrorMessage message={error} /> : null}
      {success ? <p className={styles.success} role="status">{success}</p> : null}
      <div className={styles.contentGrid}>
        <div className={styles.mainColumn}>
          <section className={styles.card} aria-labelledby="review-details-title">
            <div className={styles.sectionHeading}>
              <div><p className={styles.eyebrow}>Case details</p><h3 id="review-details-title">Applicant and parcel</h3></div>
              <Link href={`/land-intelligence/parcels/${detail.landParcelId}`} className={styles.textLink}>View parcel →</Link>
            </div>
            <dl className={styles.detailsGrid}>
              <div><dt>Applicant</dt><dd>{detail.applicant.legalName}</dd></div>
              <div><dt>Parcel number</dt><dd>{detail.cadastralNumber ?? "Not recorded"}</dd></div>
              <div><dt>District</dt><dd>{detail.district ?? "Not recorded"}</dd></div>
              <div><dt>Requested purpose</dt><dd>{landUseTypeLabels[detail.proposal.requestedPurpose] ?? "Unknown"}</dd></div>
              <div><dt>Requested term</dt><dd>{detail.proposal.requestedDurationYears} years <span className={styles.secondary}>· not approved</span></dd></div>
              <div><dt>Submitted</dt><dd>{detail.submittedAtUtc ? formatDate(detail.submittedAtUtc) : "Not submitted"}</dd></div>
            </dl>
          </section>
          <section className={styles.card} aria-labelledby="review-history-title">
            <div className={styles.sectionHeading}><div><p className={styles.eyebrow}>History</p><h3 id="review-history-title">Activity timeline</h3></div></div>
            <ol className={styles.timeline}>
              {detail.statusHistory.map((entry, index) => (
                <li key={`${entry.changedAtUtc}-${index}`} className={styles.timelineItem}>
                  <span className={styles.timelineDot} aria-hidden="true" />
                  <div className={styles.timelineContent}>
                    <div className={styles.timelineTop}><strong>{leaseInterestStatusLabel(entry.status)}</strong><time dateTime={entry.changedAtUtc}>{formatDate(entry.changedAtUtc)}</time></div>
                    {entry.note ? <p>{entry.note}</p> : null}
                    {entry.visibleToApplicant === false ? <span className={styles.internalTag}>Internal note</span> : null}
                  </div>
                </li>
              ))}
            </ol>
          </section>
        </div>
        <aside className={styles.decisionCard} aria-labelledby="review-decision-title">
          <p className={styles.eyebrow}>Officer action</p>
          <h3 id="review-decision-title">Record a decision</h3>
          {canReview ? (
            <form onSubmit={(event) => { event.preventDefault(); void handleStatusUpdate(); }}>
              <div className={styles.field}>
                <label htmlFor="officer-new-status">Outcome</label>
                <select id="officer-new-status" className={styles.input} value={newStatus} disabled={saving}
                  onChange={(event) => setNewStatus(Number(event.target.value) as LeaseInterestRequestStatus)}>
                  {outcomeOptions.map((status) => <option key={status} value={status}>{leaseInterestStatusLabel(status)}</option>)}
                </select>
                <p className={styles.fieldHelp}>{newStatus === 3 ? "Return this request to the verification queue." : leaseInterestApplicantOutcome(newStatus)}</p>
              </div>
              <div className={styles.field}>
                <label htmlFor="officer-note">Note to applicant <span aria-hidden="true">*</span></label>
                <textarea id="officer-note" className={styles.input} rows={5} value={note} required disabled={saving}
                  onChange={(event) => setNote(event.target.value)} placeholder="Explain the decision and what happens next." />
                <p className={styles.fieldHelp}>This note will appear in the applicant’s request timeline.</p>
              </div>
              <Button type="submit" variant="primary" className={styles.submitButton} disabled={saving || !note.trim()}>
                {saving ? "Saving decision…" : "Record decision"}
              </Button>
            </form>
          ) : (
            <p className={styles.closedMessage} role="status">This review is closed. No further officer decision is available for this request.</p>
          )}
        </aside>
      </div>
    </div>
  );
}
