import type { LeaseInterestRequestStatus } from "../types/landIntelligence";

export function leaseInterestStatusLabel(status: LeaseInterestRequestStatus): string {
  switch (status) {
    case 1:
      return "Draft";
    case 2:
      return "Submitted";
    case 3:
      return "Pending verification";
    case 4:
      return "Needs information";
    case 5:
      return "Rejected";
    case 6:
      return "Referred to authority";
    case 7:
      return "Approved in principle";
    case 8:
      return "Closed — other request approved";
    case 9:
      return "Awaiting valuation";
    default:
      return "Unknown";
  }
}

export function leaseInterestStatusBadgeTone(
  status: LeaseInterestRequestStatus,
): "neutral" | "success" | "warning" | "danger" | "primary" {
  switch (status) {
    case 1:
      return "neutral";
    case 2:
    case 3:
      return "primary";
    case 4:
    case 6:
    case 9:
      return "warning";
    case 5:
      return "danger";
    case 7:
      return "success";
    case 8:
      return "neutral";
    default:
      return "neutral";
  }
}

export function isLeaseInterestDraftStatus(
  status: LeaseInterestRequestStatus,
): boolean {
  return status === 1;
}

/** Applicant timeline note beside Approved / Rejected (strips legacy authority prefixes). */
export function formatApplicantTimelineNote(
  status: LeaseInterestRequestStatus,
  note: string | null | undefined,
): { label: string; body: string } | null {
  const trimmed = note?.trim();
  if (!trimmed) {
    return null;
  }

  if (status === 7) {
    const body = stripApprovedAuthorityTimelineNote(trimmed);
    return { label: "Officer Notes", body: body || trimmed };
  }

  if (status === 5) {
    const body = trimmed.replace(/^Rejected:?\s*/iu, "").trim();
    return { label: "Officer Notes", body: body || trimmed };
  }

  if (status === 8) {
    return { label: "Outcome", body: trimmed };
  }

  return { label: "Note", body: trimmed };
}

function stripApprovedAuthorityTimelineNote(note: string): string {
  return note
    .replace(
      /^Approved\s*\(preliminary only\s*[—-]\s*formal lease processing is still required\):\s*/iu,
      "",
    )
    .replace(/^Approved\.?\s*Officer Notes:\s*/iu, "")
    .trim();
}

export function leaseInterestApplicantOutcome(status: LeaseInterestRequestStatus): string {
  switch (status) {
    case 1:
      return "Draft — not submitted.";
    case 2:
      return "Submitted — awaiting officer queue (preliminary interest only).";
    case 3:
      return "";
    case 4:
      return "More information is required before officer verification can continue.";
    case 5:
      return "Rejected after review.";
    case 6:
      return "Referred for authority follow-up after officer valuation (preliminary only).";
    case 9:
      return "Preliminary verification is complete. A valuation officer will record the land worksheet next.";
    case 7:
      return "";
    case 8:
      return "Closed — another preliminary request for this parcel was approved in principle. This is not a rejection of your proposal on its merits.";
    default:
      return "Status unknown.";
  }
}
