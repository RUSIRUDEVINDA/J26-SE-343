import { apiDelete, apiGet, apiGetOrNull, apiPost, apiPut } from "@/lib/apiClient";
import type {
  LeaseInterestApplicantValuationSummary,
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

/**
 * Looks up an existing request for the parcel without creating a draft.
 * Returns null when the applicant has not started a request yet (HTTP 204).
 */
export async function findLeaseInterestForParcel(
  landParcelId: string,
): Promise<LeaseInterestRequestDetail | null> {
  return apiGetOrNull<LeaseInterestRequestDetail>(
    `/api/v1/land/me/parcels/${landParcelId}/lease-interest`,
  );
}

/**
 * Explicit Start request — creates a draft only when none exists for this parcel.
 */
export async function startLeaseInterestDraft(
  landParcelId: string,
): Promise<LeaseInterestRequestDetail> {
  return apiPost<LeaseInterestRequestDetail>(
    `/api/v1/land/me/parcels/${landParcelId}/lease-interest/draft`,
    {},
  );
}

/** @deprecated Prefer findLeaseInterestForParcel + startLeaseInterestDraft. */
export async function getOrCreateLeaseInterestDraft(
  landParcelId: string,
): Promise<LeaseInterestRequestDetail> {
  return startLeaseInterestDraft(landParcelId);
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

export async function deleteLeaseInterestDraft(requestId: string): Promise<void> {
  await apiDelete(
    `/api/v1/land/me/lease-interest/requests/${requestId}/draft`,
  );
}

/**
 * Latest officer valuation for a request owned by the current applicant.
 * Returns null when none has been recorded (HTTP 204).
 */
export async function getMyLeaseInterestValuation(
  requestId: string,
): Promise<LeaseInterestApplicantValuationSummary | null> {
  return apiGetOrNull<LeaseInterestApplicantValuationSummary>(
    `/api/v1/land/me/lease-interest/requests/${requestId}/valuation`,
  );
}

export async function getLeaseInterestDocumentChecklist(): Promise<LeaseInterestDocumentChecklist> {
  return apiGet<LeaseInterestDocumentChecklist>(
    "/api/v1/land/me/lease-interest/document-checklist",
  );
}
