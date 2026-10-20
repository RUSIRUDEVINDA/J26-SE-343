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
      return "Declined (preliminary)";
    case 6:
      return "Referred to authority";
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
      return "warning";
    case 5:
      return "danger";
    case 6:
      return "warning";
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
      return "Officer needs more information before verification can continue.";
    case 5:
      return "Preliminary interest declined after officer review. This is not a court or authority decision.";
    case 6:
      return "Referred for authority follow-up outside this pilot. No lease has been created.";
    default:
      return "Status unknown.";
  }
}
