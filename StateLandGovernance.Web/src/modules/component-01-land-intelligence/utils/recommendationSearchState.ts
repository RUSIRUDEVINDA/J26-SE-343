import {
  isSupportedLandUseType,
  LandUseType,
  type FindSuitableLandFormState,
  type LandRecommendationSearchRequest,
} from "../types/landIntelligence";
import {
  convertTargetAreaToHectares,
  parseTargetAreaInputUnit,
  type TargetAreaInputUnit,
} from "./targetAreaUnits";

export type ParsedRecommendationSearch = {
  request: LandRecommendationSearchRequest;
  formState: FindSuitableLandFormState;
  /** Stable fingerprint of URL params for session reuse / duplicate-fetch avoidance. */
  fingerprint: string;
};

const EMPTY_FORM: FindSuitableLandFormState = {
  requiredPurpose: "",
  targetAreaValue: "",
  targetAreaUnit: "hectares",
  preferredProvince: "",
  preferredDistrict: "",
  preferredGramaNiladhariDivision: "",
  sourceReportedLandUse: "",
  maxResults: "30",
};

function optionalTrim(value: string | null | undefined): string {
  return value?.trim() ?? "";
}

function parsePositiveNumber(raw: string | null): number | undefined {
  if (!raw?.trim()) {
    return undefined;
  }
  const value = Number(raw);
  return Number.isFinite(value) && value > 0 ? value : undefined;
}

function parsePurpose(raw: string | null): LandUseType | null {
  if (!raw?.trim()) {
    return null;
  }
  const value = Number(raw);
  return isSupportedLandUseType(value) ? value : null;
}

function parseGramaNiladhariFromUrl(
  searchParams: URLSearchParams | { get(name: string): string | null },
): string {
  const gn = optionalTrim(searchParams.get("gn"));
  if (gn) {
    return gn;
  }
  return optionalTrim(searchParams.get("ds"));
}

/**
 * Canonical URL → request + formState parser for recommendation results.
 * Preserves purpose, target area (+ unit), location (including GN division), land-use filters, max results.
 */
export function parseRecommendationSearchParams(
  searchParams: URLSearchParams | { get(name: string): string | null },
): ParsedRecommendationSearch | null {
  const purpose = parsePurpose(searchParams.get("purpose"));
  if (purpose == null) {
    return null;
  }

  const areaUnit = parseTargetAreaInputUnit(searchParams.get("areaUnit"));
  const areaValue = optionalTrim(searchParams.get("areaValue"));
  const areaHaFromParam = parsePositiveNumber(searchParams.get("area"));
  const areaHaFromValue =
    areaValue !== ""
      ? convertTargetAreaToHectares(Number(areaValue), areaUnit)
      : null;
  const targetAreaHectares = areaHaFromValue ?? areaHaFromParam ?? null;

  const province = optionalTrim(searchParams.get("province"));
  const district = optionalTrim(searchParams.get("district"));
  const gnDivision = parseGramaNiladhariFromUrl(searchParams);

  const landUse = optionalTrim(searchParams.get("landUse"));
  const landCover = optionalTrim(searchParams.get("landCover"));
  const sourceReportedLandUse = landCover
    ? `cover:${landCover}`
    : landUse;

  const maxResultsRaw = optionalTrim(searchParams.get("maxResults"));
  const maxResults = parsePositiveNumber(maxResultsRaw);

  const request: LandRecommendationSearchRequest = {
    requiredPurpose: purpose,
    targetAreaHectares,
    preferredLocation:
      province || district || gnDivision
        ? {
            province: province || null,
            district: district || null,
            gramaNiladhariDivision: gnDivision || null,
          }
        : null,
    sourceReportedLandUse: landCover ? null : landUse || null,
    researchLandCoverGroup: landCover || null,
    maxResults: maxResults ?? 30,
  };

  const formState: FindSuitableLandFormState = {
    requiredPurpose: purpose,
    targetAreaValue:
      areaValue ||
      (areaHaFromParam != null ? String(areaHaFromParam) : ""),
    targetAreaUnit: areaValue ? areaUnit : areaHaFromParam != null ? "hectares" : areaUnit,
    preferredProvince: province,
    preferredDistrict: district,
    preferredGramaNiladhariDivision: gnDivision,
    sourceReportedLandUse,
    maxResults: maxResultsRaw || "30",
  };

  return {
    request,
    formState,
    fingerprint: recommendationSearchFingerprint(request, formState),
  };
}

/** Build shareable / reloadable URL params from request + form state. */
export function buildRecommendationSearchParams(
  request: LandRecommendationSearchRequest,
  formState: FindSuitableLandFormState,
): URLSearchParams {
  const params = new URLSearchParams();
  params.set("purpose", String(request.requiredPurpose));

  const areaValue = formState.targetAreaValue.trim();
  const areaUnit: TargetAreaInputUnit = formState.targetAreaUnit;
  if (areaValue) {
    params.set("areaValue", areaValue);
    params.set("areaUnit", areaUnit);
  }
  if (request.targetAreaHectares != null && request.targetAreaHectares > 0) {
    params.set("area", String(request.targetAreaHectares));
  }

  const province =
    formState.preferredProvince.trim() ||
    request.preferredLocation?.province?.trim() ||
    "";
  const district =
    formState.preferredDistrict.trim() ||
    request.preferredLocation?.district?.trim() ||
    "";
  const gn =
    formState.preferredGramaNiladhariDivision.trim() ||
    request.preferredLocation?.gramaNiladhariDivision?.trim() ||
    "";
  if (province) params.set("province", province);
  if (district) params.set("district", district);
  if (gn) params.set("gn", gn);

  if (request.researchLandCoverGroup) {
    params.set("landCover", request.researchLandCoverGroup);
  } else if (request.sourceReportedLandUse) {
    params.set("landUse", request.sourceReportedLandUse);
  } else if (formState.sourceReportedLandUse.startsWith("cover:")) {
    params.set("landCover", formState.sourceReportedLandUse.slice(6));
  } else if (formState.sourceReportedLandUse.trim()) {
    params.set("landUse", formState.sourceReportedLandUse.trim());
  }

  if (request.maxResults != null) {
    params.set("maxResults", String(request.maxResults));
  } else if (formState.maxResults.trim()) {
    params.set("maxResults", formState.maxResults.trim());
  }

  return params;
}

/** Stable fingerprint for comparing URL vs session payload (avoids duplicate API calls). */
export function recommendationSearchFingerprint(
  request: LandRecommendationSearchRequest,
  formState?: FindSuitableLandFormState | null,
): string {
  const loc = request.preferredLocation;
  const parts = [
    `p=${request.requiredPurpose}`,
    `a=${request.targetAreaHectares ?? ""}`,
    `av=${formState?.targetAreaValue?.trim() ?? ""}`,
    `au=${formState?.targetAreaUnit ?? ""}`,
    `pr=${loc?.province?.trim() ?? ""}`,
    `d=${loc?.district?.trim() ?? ""}`,
    `gn=${loc?.gramaNiladhariDivision?.trim() ?? ""}`,
    `lu=${request.sourceReportedLandUse?.trim() ?? ""}`,
    `lc=${request.researchLandCoverGroup?.trim() ?? ""}`,
    `m=${request.maxResults ?? ""}`,
  ];
  return parts.join("|");
}

/**
 * True when session payload already matches URL criteria — reuse response, skip re-fetch.
 */
export function sessionMatchesUrlFingerprint(
  sessionFingerprint: string | null | undefined,
  urlFingerprint: string,
): boolean {
  return Boolean(sessionFingerprint && sessionFingerprint === urlFingerprint);
}

export function buildRequestFromFormState(
  form: FindSuitableLandFormState,
): LandRecommendationSearchRequest | null {
  if (!isSupportedLandUseType(form.requiredPurpose)) {
    return null;
  }
  const areaValue = form.targetAreaValue.trim();
  const parsedAreaInput =
    areaValue === "" ? undefined : Number.parseFloat(areaValue);
  const parsedTargetArea =
    parsedAreaInput === undefined
      ? undefined
      : convertTargetAreaToHectares(parsedAreaInput, form.targetAreaUnit);
  const parsedMaxResults = Number.parseInt(form.maxResults.trim(), 10);
  const province = form.preferredProvince.trim();
  const district = form.preferredDistrict.trim();
  const gn = form.preferredGramaNiladhariDivision.trim();

  return {
    requiredPurpose: form.requiredPurpose,
    targetAreaHectares: parsedTargetArea ?? null,
    preferredLocation:
      province || district || gn
        ? {
            province: province || null,
            district: district || null,
            gramaNiladhariDivision: gn || null,
          }
        : null,
    sourceReportedLandUse: form.sourceReportedLandUse.startsWith("cover:")
      ? null
      : form.sourceReportedLandUse.trim() || null,
    researchLandCoverGroup: form.sourceReportedLandUse.startsWith("cover:")
      ? form.sourceReportedLandUse.slice(6)
      : null,
    maxResults: Number.isFinite(parsedMaxResults) ? parsedMaxResults : 30,
  };
}

export function emptyFindSuitableLandFormState(): FindSuitableLandFormState {
  return { ...EMPTY_FORM };
}
