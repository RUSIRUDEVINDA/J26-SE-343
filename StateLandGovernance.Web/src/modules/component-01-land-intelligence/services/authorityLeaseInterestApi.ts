import { apiGet, apiPost } from "@/lib/apiClient";
import type {
  LeaseInterestAuthorityQueueResponse,
  LeaseInterestAuthorityRequestDetail,
  RecordLeaseInterestAuthorityDecision,
} from "../types/landIntelligence";
import {
  authorityQueueQueryString,
  DEFAULT_AUTHORITY_QUEUE_FILTER,
  type AuthorityQueueFilter,
} from "../utils/authorityQueueFilter";

export async function listAuthorityLeaseInterestQueue(
  page = 1,
  pageSize = 20,
  statusFilter: AuthorityQueueFilter = DEFAULT_AUTHORITY_QUEUE_FILTER,
): Promise<LeaseInterestAuthorityQueueResponse> {
  return apiGet<LeaseInterestAuthorityQueueResponse>(
    `/api/v1/land/authority/lease-interest/queue?${authorityQueueQueryString(page, pageSize, statusFilter)}`,
    undefined,
    "authority",
  );
}

export async function getAuthorityLeaseInterestRequest(
  requestId: string,
): Promise<LeaseInterestAuthorityRequestDetail> {
  return apiGet<LeaseInterestAuthorityRequestDetail>(
    `/api/v1/land/authority/lease-interest/requests/${requestId}`,
    undefined,
    "authority",
  );
}

export async function recordAuthorityDecision(
  requestId: string,
  input: RecordLeaseInterestAuthorityDecision,
): Promise<LeaseInterestAuthorityRequestDetail> {
  return apiPost<LeaseInterestAuthorityRequestDetail>(
    `/api/v1/land/authority/lease-interest/requests/${requestId}/decisions`,
    input,
    undefined,
    "authority",
  );
}
