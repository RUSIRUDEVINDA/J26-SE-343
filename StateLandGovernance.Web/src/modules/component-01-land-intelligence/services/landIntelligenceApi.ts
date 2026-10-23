import { apiGet, apiPost } from "@/lib/apiClient";
import { COLOMBO_GRAMA_NILADHARI_DIVISIONS } from "../data/colomboGramaNiladhariDivisions";
import type {
  CanonicalLandUseOptionResponse,
  LandIntelligenceGisCoverageResponse,
  LandParcelMetricsResponse,
  LandParcelResponse,
  LandRecommendationSearchRequest,
  LandRecommendationSearchResultsResponse,
  LandRelationshipsResponse,
  LandSearchResultsResponse,
  ApprovedPreliminaryParcelListResponse,
  ResearchSupplementParcelResponse,
  SourceReportedLandUseOptionResponse,
  SpatialConstraintResponse,
} from "../types/landIntelligence";

export async function searchRecommendations(
  request: LandRecommendationSearchRequest,
): Promise<LandRecommendationSearchResultsResponse> {
  return apiPost<LandRecommendationSearchResultsResponse>(
    "/api/v1/land/recommendations",
    request,
  );
}

export async function getParcelById(id: string): Promise<LandParcelResponse> {
  return apiGet<LandParcelResponse>(`/api/v1/land/parcels/${id}`);
}

export async function getParcelConstraints(
  id: string,
): Promise<SpatialConstraintResponse[]> {
  return apiGet<SpatialConstraintResponse[]>(
    `/api/v1/land/parcels/${id}/constraints`,
  );
}

export async function getParcelRelationships(
  id: string,
): Promise<LandRelationshipsResponse> {
  return apiGet<LandRelationshipsResponse>(
    `/api/v1/land/parcels/${id}/relationships`,
  );
}

export type ListParcelsParams = {
  page?: number;
  pageSize?: number;
  district?: string;
  province?: string;
  cadastralNumber?: string;
  sourceReportedLandUse?: string;
  /** Canonical land-use label or key; matches cadastral and/or project GIS evidence. */
  researchLandCoverGroup?: string;
};

export async function listApprovedPreliminaryParcels(params: {
  page?: number;
  pageSize?: number;
} = {}): Promise<ApprovedPreliminaryParcelListResponse> {
  const search = new URLSearchParams({
    page: String(params.page ?? 1),
    pageSize: String(params.pageSize ?? 20),
  });
  return apiGet<ApprovedPreliminaryParcelListResponse>(
    `/api/v1/land/parcels/approved-preliminary?${search.toString()}`,
  );
}

export async function listParcels(
  params: ListParcelsParams = {},
): Promise<LandSearchResultsResponse> {
  const search = new URLSearchParams({
    page: String(params.page ?? 1),
    pageSize: String(params.pageSize ?? 100),
  });
  if (params.district) {
    search.set("district", params.district);
  }
  if (params.province) {
    search.set("province", params.province);
  }
  if (params.cadastralNumber) {
    search.set("cadastralNumber", params.cadastralNumber);
  }
  if (params.sourceReportedLandUse) {
    search.set("sourceReportedLandUse", params.sourceReportedLandUse);
  }
  if (params.researchLandCoverGroup) {
    search.set("researchLandCoverGroup", params.researchLandCoverGroup);
  }
  return apiGet<LandSearchResultsResponse>(
    `/api/v1/land/parcels?${search.toString()}`,
  );
}

/** Exact cadastral lookup (trimmed, case-insensitive). Returns 0–N matching parcels. */
export async function findParcelsByCadastralNumber(
  cadastralNumber: string,
): Promise<LandSearchResultsResponse> {
  const trimmed = cadastralNumber.trim();
  return listParcels({
    page: 1,
    pageSize: 100,
    cadastralNumber: trimmed,
  });
}

export async function listSourceReportedLandUseOptions(): Promise<
  SourceReportedLandUseOptionResponse[]
> {
  return apiGet<SourceReportedLandUseOptionResponse[]>(
    "/api/v1/land/parcels/source-reported-land-use-options",
  );
}

export async function listResearchSupplement(): Promise<ResearchSupplementParcelResponse[]> {
  return apiGet<ResearchSupplementParcelResponse[]>(
    "/api/v1/land/parcels/research-supplement",
  );
}

export async function listCanonicalLandUseOptions(
  district?: string,
): Promise<CanonicalLandUseOptionResponse[]> {
  const search = new URLSearchParams();
  if (district?.trim()) {
    search.set("district", district.trim());
  }
  const suffix = search.toString() ? `?${search.toString()}` : "";
  return apiGet<CanonicalLandUseOptionResponse[]>(
    `/api/v1/land/parcels/canonical-land-use-options${suffix}`,
  );
}

function colomboWorkbookGnDivisionOptions(district?: string): string[] {
  const trimmed = district?.trim();
  if (trimmed && trimmed.toLowerCase() !== "colombo") {
    return [];
  }
  return [...COLOMBO_GRAMA_NILADHARI_DIVISIONS];
}

export async function listGramaNiladhariDivisionOptions(
  district?: string,
): Promise<string[]> {
  const search = new URLSearchParams();
  if (district?.trim()) {
    search.set("district", district.trim());
  }
  const suffix = search.toString() ? `?${search.toString()}` : "";
  try {
    const response = await apiGet<{ options: string[] }>(
      `/api/v1/land/parcels/grama-niladhari-division-options${suffix}`,
    );
    if (response.options && response.options.length > 0) {
      return response.options;
    }
  } catch {
    // API may be unavailable until the host is rebuilt/restarted.
  }
  return colomboWorkbookGnDivisionOptions(district);
}

export async function listDerivedSoilGroupOptions(
  district?: string,
): Promise<string[]> {
  const search = new URLSearchParams();
  if (district?.trim()) {
    search.set("district", district.trim());
  }
  const suffix = search.toString() ? `?${search.toString()}` : "";
  const response = await apiGet<{ options: string[] }>(
    `/api/v1/land/parcels/derived-soil-group-options${suffix}`,
  );
  return response.options ?? [];
}

/** Loads every parcel page (API page size max 100). */
export async function listAllParcels(
  params: Omit<ListParcelsParams, "page" | "pageSize"> = {},
): Promise<LandSearchResultsResponse> {
  const first = await listParcels({ ...params, page: 1, pageSize: 100 });
  if (first.totalCount <= first.results.length) {
    return first;
  }

  const combined = [...first.results];
  let page = 2;
  while (combined.length < first.totalCount) {
    const next = await listParcels({ ...params, page, pageSize: 100 });
    combined.push(...next.results);
    if (next.results.length === 0) {
      break;
    }
    page += 1;
  }

  return {
    ...first,
    results: combined,
    page: 1,
    pageSize: combined.length,
  };
}

export async function listParcelsSummary(): Promise<LandSearchResultsResponse> {
  return listParcels({ page: 1, pageSize: 1 });
}

export async function getLandParcelMetrics(): Promise<LandParcelMetricsResponse> {
  return apiGet<LandParcelMetricsResponse>("/api/v1/land/parcels/metrics");
}

export async function getLandIntelligenceGisCoverage(): Promise<LandIntelligenceGisCoverageResponse> {
  return apiGet<LandIntelligenceGisCoverageResponse>(
    "/api/v1/land/parcels/gis-coverage",
  );
}
