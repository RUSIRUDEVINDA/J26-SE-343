import { apiGet, apiPost } from "@/lib/apiClient";
import type {
  LandParcelResponse,
  LandRecommendationSearchRequest,
  LandRecommendationSearchResultsResponse,
  LandRelationshipsResponse,
  LandSearchResultsResponse,
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
  return apiGet<LandSearchResultsResponse>(
    `/api/v1/land/parcels?${search.toString()}`,
  );
}

/** Loads every parcel page (API page size max 100). */
export async function listAllParcels(): Promise<LandSearchResultsResponse> {
  const first = await listParcels({ page: 1, pageSize: 100 });
  if (first.totalCount <= first.results.length) {
    return first;
  }

  const combined = [...first.results];
  let page = 2;
  while (combined.length < first.totalCount) {
    const next = await listParcels({ page, pageSize: 100 });
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
