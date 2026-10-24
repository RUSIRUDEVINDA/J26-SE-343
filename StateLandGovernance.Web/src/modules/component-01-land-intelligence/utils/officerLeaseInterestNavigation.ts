import type { LeaseInterestRequestStatus } from "../types/landIntelligence";

const LAND_INTELLIGENCE_BASE = "/land-intelligence";

/** Read-only applicant lookup detail (not verification or valuation queues). */
export function officerLeaseInterestLookupViewHref(requestId: string): string {
  return `${LAND_INTELLIGENCE_BASE}/officer/lease-interest-view/${requestId}`;
}

export function officerLeaseInterestReviewHref(
  requestId: string,
  status: LeaseInterestRequestStatus,
  hasOfficerValuation: boolean,
): string {
  if (
    status === 9 ||
    (status === 6 && !hasOfficerValuation)
  ) {
    return `${LAND_INTELLIGENCE_BASE}/officer/lease-interest-valuation/${requestId}`;
  }
  if (status === 3 || status === 4) {
    return `${LAND_INTELLIGENCE_BASE}/officer/lease-interest-review/${requestId}`;
  }
  return `${LAND_INTELLIGENCE_BASE}/officer/lease-interest-view/${requestId}`;
}

export function officerLeaseInterestReviewLinkLabel(
  status: LeaseInterestRequestStatus,
  hasOfficerValuation: boolean,
): string {
  if (status === 3 || status === 4) {
    return "Review";
  }
  if (status === 9 || (status === 6 && !hasOfficerValuation)) {
    return "Record valuation";
  }
  return "View request";
}
