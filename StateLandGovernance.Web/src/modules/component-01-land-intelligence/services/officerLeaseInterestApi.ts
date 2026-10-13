import { apiGet, apiPost } from "@/lib/apiClient";

import type {

  LeaseInterestOfficerLookupRequest,

  LeaseInterestOfficerLookupResponse,

  LeaseInterestOfficerQueueResponse,

  LeaseInterestOfficerRequestDetail,

  LeaseInterestOfficerStatusUpdate,

} from "../types/landIntelligence";



export async function lookupLeaseInterestByNic(

  request: LeaseInterestOfficerLookupRequest,

): Promise<LeaseInterestOfficerLookupResponse> {

  return apiPost<LeaseInterestOfficerLookupResponse>(

    "/api/v1/land/officer/lease-interest/lookup",

    request,

    undefined,

    "officer",

  );

}



export async function listOfficerLeaseInterestQueue(

  page = 1,

  pageSize = 20,

): Promise<LeaseInterestOfficerQueueResponse> {

  return apiGet<LeaseInterestOfficerQueueResponse>(

    `/api/v1/land/officer/lease-interest/queue?page=${page}&pageSize=${pageSize}`,

    undefined,

    "officer",

  );

}



export async function getOfficerLeaseInterestRequest(

  requestId: string,

): Promise<LeaseInterestOfficerRequestDetail> {

  return apiGet<LeaseInterestOfficerRequestDetail>(

    `/api/v1/land/officer/lease-interest/requests/${requestId}`,

    undefined,

    "officer",

  );

}



export async function updateOfficerLeaseInterestStatus(

  requestId: string,

  update: LeaseInterestOfficerStatusUpdate,

): Promise<LeaseInterestOfficerRequestDetail> {

  return apiPost<LeaseInterestOfficerRequestDetail>(

    `/api/v1/land/officer/lease-interest/requests/${requestId}/status`,

    update,

    undefined,

    "officer",

  );

}

