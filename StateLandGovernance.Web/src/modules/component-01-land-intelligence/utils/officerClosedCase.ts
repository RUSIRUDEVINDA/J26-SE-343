/**
 * Helpers for the officer valuation panel. The valuation form is editable only while the
 * request is referred to the authority (status 6) and no valuation has been recorded yet.
 */

const REFERRED_TO_AUTHORITY = 6;
const AWAITING_VALUATION = 9;

export type OfficerValuationPanelMode =
  /** Referred to authority with no valuation yet: officer can record once. */
  | "editable"
  /** At least one valuation exists (or case closed): show the read-only worksheet. */
  | "readonly"
  /** Not editable and no valuation exists. */
  | "unavailable";

export function isValuationFormEditable(status: number): boolean {
  return status === AWAITING_VALUATION || status === REFERRED_TO_AUTHORITY;
}

export function officerValuationPanelMode(
  status: number,
  valuationCount: number,
): OfficerValuationPanelMode {
  if (isValuationFormEditable(status) && valuationCount === 0) {
    return "editable";
  }
  if (valuationCount > 0) {
    return "readonly";
  }
  return "unavailable";
}

/** Closed = decided by the authority or rejected (approved in principle / declined). */
export function isClosedLeaseInterestStatus(status: number): boolean {
  return status === 5 || status === 7 || status === 8;
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

/** Valuations are newest-first; index 0 is the current worksheet. */
export function partitionValuationsByLatest<T>(
  valuations: T[],
): { current: T | null; historical: T[] } {
  if (valuations.length === 0) {
    return { current: null, historical: [] };
  }
  return { current: valuations[0], historical: valuations.slice(1) };
}

export function unavailableValuationMessage(status: number): string {
  if (isClosedLeaseInterestStatus(status)) {
    return "This case is closed and no officer valuation was recorded.";
  }
  if (status === AWAITING_VALUATION) {
    return "Open this request from the officer valuation queue to record the worksheet.";
  }
  return "This form opens after preliminary verification is complete and the request is awaiting valuation.";
}

export function readonlyValuationHelpText(
  status: number,
  valuationCount: number,
  statusLabel: string,
): string {
  if (valuationCount > 0 && status === REFERRED_TO_AUTHORITY) {
    return "Valuation recorded. This worksheet is read-only and cannot be changed or submitted again. Values are shown exactly as entered on the paper worksheet.";
  }
  return `This worksheet is read-only because the request is ${statusLabel.toLowerCase()}. Values are shown exactly as recorded by the officer; the system does not calculate lease rates or totals.`;
}
