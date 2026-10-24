export interface RegisterCaseIntakeRequest {
  applicationReference: string;
  purpose?: string;
  requestedExtent?: number;
  requestedExtentUnit?: string;
  jurisdictionCode?: string;
  languageMode?: string;
}

export interface RegisterCaseIntakeResponse {
  leaseCaseId: string;
  applicationReference: string;
  status: string;
  governedDocumentId: string;
  documentVersionId: string;
  documentAnalysisId?: string;
  analysisRunId?: string;
  analysisExecutionStatus: string;
  extractedCandidatesCount: number;
  analysisFailureReason?: string;
}

export interface LeaseCaseSummary {
  id: string;
  applicationReference: string;
  createdByActorId: string;
  createdAt: string;
  status: string;
  revision: number;
  currentVerifiedFactSnapshotId?: string;
  proposalIntake?: {
    purpose?: string;
    requestedExtentValue?: number;
    requestedExtentUnit?: string;
    jurisdictionCode?: string;
    region?: string;
    district?: string;
    sourceReference: string;
    sourceVersion: string;
  };
  latestScreening?: {
    screeningId: string;
    performedAt: string;
    overallOutcome: string;
  };
  activeWorkflowPlan?: {
    planId: string;
    version: number;
    state: string;
  };
}

export interface PagedLeaseCasesDto {
  items: LeaseCaseSummary[];
  totalCount: number;
  skip: number;
  take: number;
}

export interface CaseDocumentSummaryDto {
  documentId: string;
  logicalCategory: string;
  activeVersionId: string;
  versionNumber: number;
  originalFileName: string;
  mediaType: string;
  fileSizeBytes: number;
  checksumValue: string;
  createdAt: string;
  documentAnalysisId?: string;
  documentAnalysisRevision: number;
  latestRunResultId?: string;
  analysisRunCount: number;
  latestRunState?: string;
  latestRunCandidatesCount: number;
  latestRunFailureCode?: string;
  latestRunFailureDescription?: string;
}

export interface CaseVerifiedSnapshotSummaryDto {
  snapshotId: string;
  documentAnalysisId: string;
  snapshotOutcome: string;
  publishedAt: string;
  publishingActorId: string;
  publishedFactCount: number;
  confirmedFactCount: number;
  correctedFactCount: number;
}

export interface CaseGovernedFieldRowDto {
  factCode: string;
  displayName: string;
  status: "Extracted" | "Ungrounded" | "NoCandidate" | "Missing" | "Unavailable" | "Confirmed" | "Corrected" | "Rejected" | "Unsupported" | "NotExtracted" | "Verified";
  candidateValue?: string | null;
  preferredValue?: string | null;
  confidenceScore?: number | null;
  pageNumber?: number | null;
  evidenceQuote?: string | null;
  extractedFactId?: string | null;
  verificationId?: string | null;
  officerDecision?: string | null;
  officerReason?: string | null;
  isGrounded?: boolean;
  sourceKind?: "MachineCandidateReview" | "HumanDocumentEvidence";
  humanEvidenceObservationId?: string;
}

export interface CaseDetailResponse {
  canVerifyFacts?: boolean;
  canPublishSnapshot?: boolean;
  proposalSelection?: { documentId?: string; versionId?: string; analysisId?: string; runId?: string;
    runResultId?: string; status: string; notice?: string; linkedSnapshotMatchesSelection: boolean } | null;
  leaseCaseId: string;
  applicationReference: string;
  createdByActorId: string;
  createdAt: string;
  status: string;
  revision: number;
  currentVerifiedFactSnapshotId?: string;
  purpose?: string;
  requestedExtent?: number;
  requestedExtentUnit?: string;
  jurisdictionCode?: string;
  documents: CaseDocumentSummaryDto[];
  snapshot?: CaseVerifiedSnapshotSummaryDto | null;
  fieldInventory: CaseGovernedFieldRowDto[];
}

export interface VerifyFactApiRequest {
  documentAnalysisId: string;
  analysisRunResultId: string;
  extractedFactId: string;
  expectedAnalysisRevision: number;
  decision: "Confirm" | "Correct" | "Reject" | "MarkUnsupported";
  correctedValue?: string;
  valueKind?: "Text" | "Numeric" | "Quantity" | "Monetary" | "Identifier" | "Date" | "Boolean" | "Categorical";
  reason?: string;
}

export interface PublishSnapshotApiRequest {
  documentAnalysisId: string;
  analysisRunResultId: string;
  expectedAnalysisRevision: number;
  expectedLeaseCaseRevision: number;
}

export interface ApplicantMilestoneDto {
  milestoneKey: string;
  title: string;
  description: string;
  state: "Completed" | "InProgress" | "Pending" | "AttentionRequired";
  updatedAt?: string | null;
}

export interface ApplicantDocumentStatusDto {
  documentId: string;
  logicalCategory: string;
  originalFileName: string;
  processingStatus: "Uploaded" | "Processing" | "Verified" | "Rejected" | "DataExtracted" | "QueuedForExtraction";
  uploadedAt: string;
}

export interface ApplicantCorrectionNoticeDto {
  revision: number;
  requestId: string;
  requiredAction: string;
  requiredClassificationCode?: string | null;
  requiredSectionId?: string | null;
  instructions: string;
  requestedAt: string;
  dueDate?: string | null;
  status: string; // Pending, Fulfilled, Waived, Cancelled
}

export interface ApplicantCaseTrackingDto {
  leaseCaseId: string;
  applicationReference: string;
  status: string;
  createdAt: string;
  currentMilestone: string;
  completedMilestonesCount: number;
  totalMilestonesCount: number;
  milestones: ApplicantMilestoneDto[];
  documents: ApplicantDocumentStatusDto[];
  correctionNotices: ApplicantCorrectionNoticeDto[];
}

export interface DocumentRequirementSnapshotDto {
  id: string;
  requiredClassificationCode: string;
  minimumRequiredCount: number;
  applicability: string;
  criticality: string;
  reasonCode: string;
  description: string;
}

export interface MissingRequiredDocumentDto {
  requirementId: string;
  requiredClassificationCode: string;
  requiredCount: number;
  satisfiedCount: number;
  description: string;
}

export interface DocumentCompletenessAssessmentDto {
  id: string;
  leaseCaseId: string;
  requirementSetIdentifier: string;
  requirementSetVersion: string;
  assessedAt: string;
  outcome: string; // Satisfied, Deficit, Inconclusive
  revision: number;
  requirements: DocumentRequirementSnapshotDto[];
  missingRequirements: MissingRequiredDocumentDto[];
}

export interface ProposalContentRequirementDto {
  id: string;
  kind: string;
  displayName: string;
  isMandatory: boolean;
  templateId: string;
  templateVersion: string;
  authoritativeSourceReference: string;
  orderIndex: number;
}

export interface ProposalContentAssessmentDto {
  isConfirmed: boolean;
  id: string;
  leaseCaseId: string;
  outcome: string; // Complete, Incomplete, AttentionRequired
  satisfiedMandatory: ProposalContentRequirementDto[];
  missingMandatory: ProposalContentRequirementDto[];
  reviewRequiredMandatory: ProposalContentRequirementDto[];
  satisfiedOptional: ProposalContentRequirementDto[];
  missingOptional: ProposalContentRequirementDto[];
  explanations: string[];
}

export interface CaseCorrectionRequestDto {
  revision: number;
  id: string;
  leaseCaseId: string;
  requestedByRole: string;
  createdAtUtc: string;
  dueDateUtc?: string | null;
  status: string; // Pending, Fulfilled, Waived, Cancelled
  requiredAction: string; // UploadSupportingDocument, ReviseProposal
  requiredClassificationCode?: string | null;
  requiredSectionId?: string | null;
  officerInstructions: string;
  respondedDocumentId?: string | null;
  respondedAtUtc?: string | null;
}

export interface CaseCompletenessSummaryResponse {
  documentRequirementsConfigured: boolean;
  proposalTemplateConfigured: boolean;
  leaseCaseId: string;
  documentCompleteness?: DocumentCompletenessAssessmentDto | null;
  proposalContent?: ProposalContentAssessmentDto | null;
  isDocumentCompletenessCurrent: boolean;
  isProposalContentCurrent: boolean;
  activeCorrectionRequests: CaseCorrectionRequestDto[];
}

export interface CreateCorrectionApiRequest {
  requiredAction: string;
  requiredClassificationCode?: string;
  requiredSectionId?: string;
  officerInstructions: string;
  dueDateUtc?: string;
}
