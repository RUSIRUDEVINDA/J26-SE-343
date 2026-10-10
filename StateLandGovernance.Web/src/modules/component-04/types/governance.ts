/**
 * Presentation models for Component 4's fictional assessment examples.
 * These are not published HTTP DTOs. A future live adapter must map and validate
 * the actual server contracts rather than treating these shapes as API responses.
 */

// --- Display status vocabulary ---

export type ComplianceStatus =
  | "Compliant"
  | "NonCompliant"
  | "Conditional"
  | "RequiresReview"
  | "InsufficientInformation";

export type EarlyGovernanceScreeningStatus =
  | "Clear"
  | "ReviewRequired"
  | "InsufficientInformation";

export type EarlyGovernanceEvidenceState =
  | "VerifiedPresent"
  | "VerifiedAbsent"
  | "Unverified"
  | "Missing"
  | "Unavailable"
  | "NotApplicable";

export type ConflictStatus =
  | "Clear"
  | "ConflictDetected"
  | "NotAssessed";

export type AnomalyStatus =
  | "NotAssessed"
  | "InsufficientHistory"
  | "UnderReview"
  | "Anomalous";

export type ConsensusOutcome =
  | "ConsensusReached"
  | "ConditionalConsensus"
  | "ConsensusNotReached"
  | "Blocked"
  | "InsufficientQuorum"
  | "Pending";

/**
 * Exact 4-class taxonomy confirmed from ML config.py
 */
export type ComplaintTaxonomyCategory =
  | "Administrative / Procedural / Integrity"
  | "Lease Revenue / Payment / Enforcement"
  | "Unauthorized Allocation / Transfer / Use"
  | "Protected / Environmental Lease Misuse";

export type OverallGovernanceStatus =
  | "Clear"
  | "ReviewRequired"
  | "InsufficientInformation";

// --- Complaint Intelligence View-Model (Advisory prediction) ---

export interface ComplaintAdvisoryPrediction {
  hasComplaint: boolean;
  predictedCategory?: ComplaintTaxonomyCategory;
  /** Advisory model probability between 0.0 and 1.0 (not calibrated legal confidence) */
  modelProbability?: number;
  reviewStatus: "UnderReview" | "FlaggedForSiteInspection" | "AdvisoryNoted";
  /** Model metadata */
  modelVersion?: string;
}

// --- Governance Case Assessment View-Model ---

export interface GovernanceCaseAssessment {
  assessmentId: string;
  workflowRunId: string | null;
  caseId: string;
  parcelReference: string;
  applicantReference: string;
  overallStatus: OverallGovernanceStatus;
  complianceStatus: ComplianceStatus;
  conflictStatus: ConflictStatus;
  earlyScreeningStatus: EarlyGovernanceScreeningStatus;
  anomalyStatus: AnomalyStatus;
  consensusOutcome: ConsensusOutcome;
  evaluatedAt: string;
  version: string;
  summaryFinding: string;
  rationale: string;
  recommendedNextStep: string;
  complaintAdvisory?: ComplaintAdvisoryPrediction;
}

// --- Dashboard Summary Metrics ---

export interface GovernanceDashboardSummary {
  totalCases: number;
  totalAssessments: number;
  clearCount: number;
  reviewRequiredCount: number;
  insufficientInfoCount: number;
  lastUpdated: string;
}

export interface GovernanceDashboardData {
  summary: GovernanceDashboardSummary;
  cases: GovernanceCaseAssessment[];
}

export type GovernanceFilter = "ALL" | "REVIEW_REQUIRED" | "CLEAR" | "INSUFFICIENT";
