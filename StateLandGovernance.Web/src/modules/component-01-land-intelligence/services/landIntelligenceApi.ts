import { apiGet, apiPost } from "@/lib/apiClient";
import type {
  LandIntelligenceGisCoverageResponse,
  LandParcelMetricsResponse,
  LandParcelResponse,
  LandRecommendationSearchRequest,
  LandRecommendationSearchResultsResponse,
  LandRelationshipsResponse,
  LandSearchResultsResponse,
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
  sourceReportedLandUse?: string;
};

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
  if (params.sourceReportedLandUse) {
    search.set("sourceReportedLandUse", params.sourceReportedLandUse);
  }
  return apiGet<LandSearchResultsResponse>(
    `/api/v1/land/parcels?${search.toString()}`,
  );
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
