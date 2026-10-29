import type { LeaseInterestApplicantValuationSummary } from "../types/landIntelligence";

export type ApplicantValuationLoadState =
  | { kind: "loading" }
  | { kind: "none" }
  | { kind: "available"; summary: LeaseInterestApplicantValuationSummary }
  | { kind: "access_denied"; message: string }
  | { kind: "error"; message: string; retryable: boolean };

export const VALUATION_ACCESS_DENIED_MESSAGE =
  "You do not have access to the valuation summary for this request. Your request status above is unaffected.";
export const VALUATION_LOAD_ERROR_MESSAGE =
  "We could not load the valuation summary right now. Your request status above is unaffected. Please try again.";

/** Reads an HTTP status from ApiClientError-like errors without importing the client. */
function errorStatus(err: unknown): number | null {
  if (
    typeof err === "object" &&
    err !== null &&
    "status" in err &&
    typeof (err as { status: unknown }).status === "number"
  ) {
    return (err as { status: number }).status;
  }
  return null;
}

/**
 * Maps a failed valuation fetch to a UI state. A real "no valuation" answer
 * (HTTP 204 -> null) is not an error and must be handled by
 * {@link applicantValuationStateFromResult}, never by this function.
 */
export function classifyApplicantValuationLoadError(
  err: unknown,
): Extract<ApplicantValuationLoadState, { kind: "access_denied" | "error" }> {
  const status = errorStatus(err);
  if (status === 401 || status === 403) {
    return { kind: "access_denied", message: VALUATION_ACCESS_DENIED_MESSAGE };
  }
  return {
    kind: "error",
    message: VALUATION_LOAD_ERROR_MESSAGE,
    retryable: true,
  };
}

/** Maps a successful fetch result (null = HTTP 204, no valuation recorded). */
export function applicantValuationStateFromResult(
  summary: LeaseInterestApplicantValuationSummary | null | undefined,
): ApplicantValuationLoadState {
  return summary ? { kind: "available", summary } : { kind: "none" };
}

/** Runs a loader and always resolves to a classified state (never throws). */
export async function loadApplicantValuationState(
  load: () => Promise<LeaseInterestApplicantValuationSummary | null>,
): Promise<ApplicantValuationLoadState> {
  try {
    return applicantValuationStateFromResult(await load());
  } catch (err) {
    return classifyApplicantValuationLoadError(err);
  }
}
