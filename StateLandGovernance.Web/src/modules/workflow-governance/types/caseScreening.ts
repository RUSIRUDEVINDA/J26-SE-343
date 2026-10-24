export interface ConsolidatedAssessment {
  assessmentId: string;
  caseId: string;
  applicationReference: string;
  proposalVersion: string;
  verifiedFactSnapshotId: string;
  evaluatedAtUtc: string;
  overallStatus: string;
  officerRecommendationSummary: string;
  requiredConditions: string[];
  consolidatedFindings: { dimension: string; itemName: string; status: string; details: string;
    evidenceReference?: string; sourceAuthorityOrRule?: string; requiresHumanReview: boolean; isBlocking: boolean }[];
  regulatoryCheck: Record<string, unknown>;
  conflictCheck: Record<string, unknown>;
  earlyGovernanceCheck: Record<string, unknown>;
  provenance: Record<string, unknown>;
}
export interface CaseScreeningState {
  caseId: string;
  caseRevision: number;
  verifiedFactSnapshotId?: string;
  parcelId?: string;
  status: "BlockedIncomplete" | "ReadyForScreening" | "Assessed" | "Stale" | "Pending";
  isCurrent: boolean;
  isDevelopmentSpatialMock?: boolean;
  spatialProvenanceLabel?: string;
  missingInputs: string[];
  latestAssessment?: { caseId: string; snapshotId: string; parcelId?: string; inputFingerprint: string;
    policyFingerprint: string; assessment: ConsolidatedAssessment };
}
