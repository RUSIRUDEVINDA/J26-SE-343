import {
  parseRecommendationSearchParams,
  buildRecommendationSearchParams,
} from "./recommendationSearchState";
import type {
  FindSuitableLandFormState,
  LandRecommendationSearchRequest,
} from "../types/landIntelligence";

export const SUITABLE_LAND_RESULTS_PATH = "/land-intelligence/recommendations";

export const RESULTS_RETURN_QUERY_KEY = "resultsReturn";

export const RESULTS_FOCUS_PARCEL_QUERY_KEY = "focusParcel";

const FIND_SUITABLE_LAND_PATH = "/land-intelligence/find-suitable-land";

/**
 * Validates an internal return path to Suitable Land Results (must include valid search params).
 * Rejects protocol-relative URLs, external hosts, and paths outside recommendations.
 */
export function resolveValidResultsReturn(
  raw: string | null | undefined,
): string | null {
  if (!raw?.trim()) {
    return null;
  }

  const trimmed = raw.trim();
  if (
    trimmed.includes("://") ||
    trimmed.startsWith("//") ||
    trimmed.includes("..") ||
    !trimmed.startsWith(SUITABLE_LAND_RESULTS_PATH)
  ) {
    return null;
  }

  const question = trimmed.indexOf("?");
  const path = question === -1 ? trimmed : trimmed.slice(0, question);
  if (path !== SUITABLE_LAND_RESULTS_PATH) {
    return null;
  }

  const query = question === -1 ? "" : trimmed.slice(question + 1);
  const params = new URLSearchParams(query);
  if (!parseRecommendationSearchParams(params)) {
    return null;
  }

  return trimmed;
}

export function buildRecommendationsResultsPath(
  request: LandRecommendationSearchRequest,
  formState: FindSuitableLandFormState,
  focusParcelId?: string | null,
): string {
  const params = buildRecommendationSearchParams(request, formState);
  const focus = focusParcelId?.trim();
  if (focus) {
    params.set(RESULTS_FOCUS_PARCEL_QUERY_KEY, focus);
  }
  const qs = params.toString();
  return qs
    ? `${SUITABLE_LAND_RESULTS_PATH}?${qs}`
    : SUITABLE_LAND_RESULTS_PATH;
}

/** Appends a validated results-return path as a query param on a destination URL. */
export function appendResultsReturnQuery(
  destinationPathname: string,
  resultsReturnPath: string,
  extraQuery?: Record<string, string>,
): string {
  const params = new URLSearchParams();
  if (extraQuery) {
    for (const [key, value] of Object.entries(extraQuery)) {
      if (value) {
        params.set(key, value);
      }
    }
  }
  params.set(RESULTS_RETURN_QUERY_KEY, resultsReturnPath);
  const qs = params.toString();
  return qs ? `${destinationPathname}?${qs}` : destinationPathname;
}

export function buildParcelResultsActionHref(
  parcelId: string,
  action: "map" | "parcel" | "lease-interest",
  resultsReturnPath: string,
): string {
  const base = `/land-intelligence/parcels/${encodeURIComponent(parcelId)}${
    action === "map"
      ? "/map"
      : action === "lease-interest"
        ? "/lease-interest"
        : ""
  }`;
  return appendResultsReturnQuery(base, resultsReturnPath);
}

/** Preserves resultsReturn when navigating from a page that already has it. */
export function buildPathPreservingResultsReturn(
  pathname: string,
  sourceSearchParams: URLSearchParams | { get(name: string): string | null },
): string {
  const resolved = resolveValidResultsReturn(
    sourceSearchParams.get(RESULTS_RETURN_QUERY_KEY),
  );
  if (!resolved) {
    return pathname;
  }
  return appendResultsReturnQuery(pathname, resolved);
}

export function suitableLandResultsFallbackHref(): string {
  return FIND_SUITABLE_LAND_PATH;
}

export function confirmLeaveWithUnsavedChanges(isDirty: boolean): boolean {
  if (!isDirty) {
    return true;
  }
  return window.confirm(
    "You have unsaved changes on this form. Leave anyway? If you saved a draft, it remains on the server.",
  );
}
