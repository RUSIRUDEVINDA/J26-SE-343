/**
 * Helpers for the officer valuation panel. The valuation form is editable only while the
 * request is referred to the authority (status 6); every other status is read-only.
 */

const REFERRED_TO_AUTHORITY = 6;

export type OfficerValuationPanelMode =
  /** Referred to authority: officer can record a (revised) valuation. */
  | "editable"
  /** Not editable, but at least one valuation exists: show the read-only worksheet. */
  | "readonly"
  /** Not editable and no valuation exists. */
  | "unavailable";

export function isValuationFormEditable(status: number): boolean {
  return status === REFERRED_TO_AUTHORITY;
}

export function officerValuationPanelMode(
  status: number,
  valuationCount: number,
): OfficerValuationPanelMode {
  if (isValuationFormEditable(status)) {
    return "editable";
  }
  return valuationCount > 0 ? "readonly" : "unavailable";
}

/** Closed = decided by the authority or rejected (approved in principle / declined). */
export function isClosedLeaseInterestStatus(status: number): boolean {
  return status === 5 || status === 7;
}

/** Picks the revision to display: the selected one, otherwise the latest (first, newest-first list). */
export function selectReadOnlyValuation<T extends { id: string }>(
  valuations: T[],
  selectedId: string | null,
): T | null {
  if (valuations.length === 0) {
    return null;
  }
  return valuations.find((item) => item.id === selectedId) ?? valuations[0];
}

export function unavailableValuationMessage(status: number): string {
  if (isClosedLeaseInterestStatus(status)) {
    return "This case is closed and no officer valuation was recorded.";
  }
  return "This form opens after the request is referred to the authority.";
}
