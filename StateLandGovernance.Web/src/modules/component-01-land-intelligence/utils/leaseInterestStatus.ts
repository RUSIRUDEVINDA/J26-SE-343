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
      return "Approved";
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
      return "warning";
    case 5:
      return "danger";
    case 7:
      return "success";
    default:
      return "neutral";
  }
}

export function isLeaseInterestDraftStatus(
  status: LeaseInterestRequestStatus,
): boolean {
  return status === 1;
}

export function leaseInterestApplicantOutcome(status: LeaseInterestRequestStatus): string {
  switch (status) {
    case 1:
      return "Draft — not submitted. This is not a reservation or lease.";
    case 2:
      return "Submitted — awaiting officer queue (preliminary interest only).";
    case 3:
      return "Pending officer verification. Your expression of interest is not a reservation or lease approval.";
    case 4:
      return "More information is required before officer verification can continue.";
    case 5:
      return "Rejected after review. This is not a court decision and does not reserve land.";
    case 6:
      return "Referred for authority follow-up. An officer valuation may be recorded; this does not create a lease.";
    case 7:
      return "Approved as a preliminary project decision only. Formal lease processing is still required. This does not create a lease, reserve land, or constitute official government approval.";
    default:
      return "Status unknown.";
  }
}
