import { workflowDevelopmentHeaders, workflowAuthRole } from "./workflowDevelopmentHeaders";
import {
  apiGet,
  apiPost,
  apiPostFormData,
  apiDownloadBlob,
  type LandIntelligenceApiAuthRole,
} from "@/lib/apiClient";
import type {
  RegisterCaseIntakeResponse,
  PagedLeaseCasesDto,
  CaseDetailResponse,
  CaseDocumentSummaryDto,
  VerifyFactApiRequest,
  PublishSnapshotApiRequest,
  ApplicantCaseTrackingDto,
  CaseCompletenessSummaryResponse,
  DocumentCompletenessAssessmentDto,
  ProposalContentAssessmentDto,
  CaseCorrectionRequestDto,
  CreateCorrectionApiRequest,
} from "../types/caseManagement";

const BASE_PATH = "/api/workflow-governance/cases";

/**
 * Register a new lease case intake with an attached proposal file.
 */
export async function registerCaseIntake(
  formData: FormData,
  authRole: LandIntelligenceApiAuthRole = "applicant"
): Promise<RegisterCaseIntakeResponse> {
  return await apiPostFormData<RegisterCaseIntakeResponse>(
    `${BASE_PATH}/intake`,
    formData,
    workflowDevelopmentHeaders(authRole),
    workflowAuthRole(authRole)
  );
}

/**
 * List paginated lease cases for authorized officers.
 */
export async function listCases(
  skip: number = 0,
  take: number = 20,
  myCasesOnly: boolean = false,
  authRole: LandIntelligenceApiAuthRole = "officer"
): Promise<PagedLeaseCasesDto> {
  const query = new URLSearchParams({
    skip: skip.toString(),
    take: take.toString(),
    myCasesOnly: myCasesOnly ? "true" : "false",
  });
  return await apiGet<PagedLeaseCasesDto>(`${BASE_PATH}?${query.toString()}`, workflowDevelopmentHeaders(authRole), workflowAuthRole(authRole));
}

/**
 * Get comprehensive workspace details for a specific lease case.
 */
export async function getCaseDetail(
  caseId: string,
    authRole: LandIntelligenceApiAuthRole = "officer",
    proposalDocumentId?: string
): Promise<CaseDetailResponse> {
    const query = proposalDocumentId ? `?proposalDocumentId=${encodeURIComponent(proposalDocumentId)}` : "";
    return await apiGet<CaseDetailResponse>(`${BASE_PATH}/${caseId}${query}`, workflowDevelopmentHeaders(authRole), workflowAuthRole(authRole));
}

/**
 * Add a supporting governed document to an existing case.
 */
export async function addSupportingDocument(
  caseId: string,
  formData: FormData,
  authRole: LandIntelligenceApiAuthRole = "officer"
): Promise<CaseDocumentSummaryDto> {
  return await apiPostFormData<CaseDocumentSummaryDto>(
    `${BASE_PATH}/${caseId}/documents`,
    formData,
    workflowDevelopmentHeaders(authRole),
    workflowAuthRole(authRole)
  );
}

/**
 * Re-run OCR and semantic analysis on an existing document version without creating a duplicate case.
 */
export async function reanalyzeDocument(
  caseId: string,
  docId: string,
  versionId: string,
  options?: { executionMode?: string; semanticProvider?: string },
  authRole: LandIntelligenceApiAuthRole = "officer"
): Promise<{ message: string; analysisId: string; candidatesCount: number }> {
  const query = new URLSearchParams();
  if (options?.executionMode) query.set("executionMode", options.executionMode);
  if (options?.semanticProvider) query.set("semanticProvider", options.semanticProvider);

  const queryString = query.toString() ? `?${query.toString()}` : "";
  return await apiPost<{ message: string; analysisId: string; candidatesCount: number }>(
    `${BASE_PATH}/${caseId}/documents/${docId}/versions/${versionId}/analyze${queryString}`,
    {},
    workflowDevelopmentHeaders(authRole),
    workflowAuthRole(authRole)
  );
}

/**
 * Submit an authorized human officer fact verification decision (Confirm, Correct, MarkUnsupported).
 */
export async function verifyFact(
  caseId: string,
  request: VerifyFactApiRequest,
  authRole: LandIntelligenceApiAuthRole = "officer"
): Promise<unknown> {
  return await apiPost<unknown>(`${BASE_PATH}/${caseId}/facts/verify`, request, workflowDevelopmentHeaders(authRole), workflowAuthRole(authRole));
}

/**
 * Publish an immutable verified fact snapshot and link it to the case.
 */
export async function publishSnapshot(
  caseId: string,
  request: PublishSnapshotApiRequest,
  authRole: LandIntelligenceApiAuthRole = "officer"
): Promise<unknown> {
  return await apiPost<unknown>(`${BASE_PATH}/${caseId}/snapshots`, request, workflowDevelopmentHeaders(authRole), workflowAuthRole(authRole));
}

/**
 * Retrieve truthful milestone tracking for an applicant case.
 */
export async function getApplicantTracking(
  reference: string,
  authRole: LandIntelligenceApiAuthRole = "applicant"
): Promise<ApplicantCaseTrackingDto> {
  return await apiGet<ApplicantCaseTrackingDto>(
    `${BASE_PATH}/tracking/${encodeURIComponent(reference)}`,
    workflowDevelopmentHeaders(authRole),
    workflowAuthRole(authRole)
  );
}

/**
 * Download a document version binary stream.
 */
export async function downloadCaseDocument(
  caseId: string,
  docId: string,
  versionId: string,
  fileName: string,
  authRole: LandIntelligenceApiAuthRole = "officer"
): Promise<void> {
  const blob = await apiDownloadBlob(
    `${BASE_PATH}/${caseId}/documents/${docId}/versions/${versionId}/download`,
    workflowAuthRole(authRole),
    workflowDevelopmentHeaders(authRole)
  );
  const blobUrl = window.URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = blobUrl;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  window.URL.revokeObjectURL(blobUrl);
}

/**
 * Get dual completeness summary (Check A: supporting docs, Check B: proposal content, staleness, corrections).
 */
export async function getCaseCompletenessSummary(
  caseId: string,
  authRole: LandIntelligenceApiAuthRole = "officer"
): Promise<CaseCompletenessSummaryResponse> {
  return await apiGet<CaseCompletenessSummaryResponse>(
    `${BASE_PATH}/${caseId}/completeness`,
    workflowDevelopmentHeaders(authRole),
    workflowAuthRole(authRole)
  );
}

/**
 * Evaluates Check A: Required Supporting Document Completeness against governed statutory requirement set.
 */
export async function assessDocumentCompleteness(
  caseId: string,
  requirementSetIdentifier: string = "DOC-REQ-AGRI-V1",
  requirementSetVersion: string = "1.0",
  authRole: LandIntelligenceApiAuthRole = "officer"
): Promise<DocumentCompletenessAssessmentDto> {
  return await apiPost<DocumentCompletenessAssessmentDto>(
    `${BASE_PATH}/${caseId}/completeness/documents`,
    {
      requirementSetIdentifier,
      requirementSetVersion,
    },
    workflowDevelopmentHeaders(authRole),
    workflowAuthRole(authRole)
  );
}

/**
 * Evaluates Check B: Required Proposal Content / Sections against governed statutory proposal template.
 */
export async function assessProposalContent(
  caseId: string,
  templateId: string = "STD-PROP-2026-V1",
  templateVersion: string = "1.0",
  authRole: LandIntelligenceApiAuthRole = "officer",
  proposalDocumentId?: string
): Promise<ProposalContentAssessmentDto> {
  return await apiPost<ProposalContentAssessmentDto>(
    `${BASE_PATH}/${caseId}/completeness/proposal`,
    {
      templateId,
      templateVersion,
      proposalDocumentId,
    },
    workflowDevelopmentHeaders(authRole),
    workflowAuthRole(authRole)
  );
}

/**
 * Officer creates a formal administrative correction request against the case.
 */
export async function createCorrectionRequest(
  caseId: string,
  request: CreateCorrectionApiRequest,
  authRole: LandIntelligenceApiAuthRole = "officer"
): Promise<CaseCorrectionRequestDto> {
  return await apiPost<CaseCorrectionRequestDto>(
    `${BASE_PATH}/${caseId}/corrections`,
    request,
    workflowDevelopmentHeaders(authRole),
    workflowAuthRole(authRole)
  );
}

/**
 * Applicant or Officer responds to an active correction request with an uploaded supporting document.
 */
export async function respondToCorrection(
  caseId: string,
  requestId: string,
  formData: FormData,
  authRole: LandIntelligenceApiAuthRole = "applicant"
): Promise<CaseCorrectionRequestDto> {
  return await apiPostFormData<CaseCorrectionRequestDto>(
    `${BASE_PATH}/${caseId}/corrections/${requestId}/respond`,
    formData,
    workflowDevelopmentHeaders(authRole),
    workflowAuthRole(authRole)
  );
}

export async function reviewCorrection(
  caseId: string,
  requestId: string,
  accept: boolean,
  reason: string,
  expectedRevision: number,
): Promise<CaseCorrectionRequestDto> {
  return await apiPost<CaseCorrectionRequestDto>(
    `${BASE_PATH}/${caseId}/corrections/${requestId}/review`,
    { accept, reason, expectedRevision },
    workflowDevelopmentHeaders("officer"),
    workflowAuthRole("officer"),
  );
}
