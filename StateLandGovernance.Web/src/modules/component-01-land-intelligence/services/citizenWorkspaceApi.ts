import { apiDelete, apiGet, apiPost, apiPut } from "@/lib/apiClient";
import type {
  LeaseInterestDocumentChecklist,
  LeaseInterestRequestDetail,
  LeaseInterestRequestSummary,
  ParcelShortlistItem,
  UpsertLeaseInterestDraft,
} from "../types/landIntelligence";

export async function listShortlist(): Promise<ParcelShortlistItem[]> {
  return apiGet<ParcelShortlistItem[]>("/api/v1/land/me/shortlist");
}

export async function saveParcelToShortlist(landParcelId: string): Promise<void> {
  await apiPut(`/api/v1/land/me/shortlist/${landParcelId}`, {});
}

export async function removeParcelFromShortlist(
  landParcelId: string,
): Promise<void> {
  await apiDelete(`/api/v1/land/me/shortlist/${landParcelId}`);
}

export async function getOrCreateLeaseInterestDraft(
  landParcelId: string,
): Promise<LeaseInterestRequestDetail> {
  return apiPost<LeaseInterestRequestDetail>(
    `/api/v1/land/me/parcels/${landParcelId}/lease-interest/draft`,
    {},
  );
}

export async function saveLeaseInterestDraft(
  requestId: string,
  draft: UpsertLeaseInterestDraft,
): Promise<LeaseInterestRequestDetail> {
  return apiPut<LeaseInterestRequestDetail>(
    `/api/v1/land/me/lease-interest/requests/${requestId}/draft`,
    draft,
  );
}

export async function submitLeaseInterest(
  requestId: string,
  draft: UpsertLeaseInterestDraft,
  idempotencyKey: string,
): Promise<LeaseInterestRequestDetail> {
  return apiPost<LeaseInterestRequestDetail>(
    `/api/v1/land/me/lease-interest/requests/${requestId}/submit`,
    draft,
    { headers: { "Idempotency-Key": idempotencyKey } },
  );
}

export async function listMyLeaseInterestRequests(): Promise<
  LeaseInterestRequestSummary[]
> {
  return apiGet<LeaseInterestRequestSummary[]>(
    "/api/v1/land/me/lease-interest/requests",
  );
}

export async function getLeaseInterestRequest(
  requestId: string,
): Promise<LeaseInterestRequestDetail> {
  return apiGet<LeaseInterestRequestDetail>(
    `/api/v1/land/me/lease-interest/requests/${requestId}`,
  );
}

export async function getLeaseInterestDocumentChecklist(): Promise<LeaseInterestDocumentChecklist> {
  return apiGet<LeaseInterestDocumentChecklist>(
    "/api/v1/land/me/lease-interest/document-checklist",
  );
}
