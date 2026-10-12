import type { GovernanceDashboardData } from "../types/governance";
import { ApiClientError, apiGet, apiPost } from "../../../lib/apiClient";
import { MOCK_GOVERNANCE_DASHBOARD_DATA } from "./mockGovernanceData";

const governanceApiBasePath = "/api/governance-intelligence";
const evaluationTimeoutMs = 15_000;

export interface ComplianceEvaluationRequest {
  actionName: string;
  leaseDurationYears: number;
  proposedUse: string;
  leaseAmount: number;
  zoningArea: string;
}

export interface ComplianceFinding {
  ruleCode: string;
  ruleVersion: string;
  category: string;
  applicability: string;
  status: string;
  severity: string;
  isBlocking: boolean;
  requiresHumanReview: boolean;
  observedValueSummary: string;
  expectedRequirement: string;
  evidenceStatus: string;
  calculationStatus: string;
  sourceAuthority: string;
  sourceDocument: string;
  sourceSection: string;
  recommendedAction: string;
}

export interface ComplianceEvaluationResponse {
  status: string;
  violations: Array<{ ruleCode: string; message: string }>;
  conditions: Array<{ description: string; requiredByDate: string | null }>;
  findings?: ComplianceFinding[] | null;
  deterministicEvaluationId?: string | null;
}

export interface GovernanceDecisionSnapshot {
  decisionId: string;
  subjectId: string;
  institutionName: string;
  authorityLevel: string;
  decisionType: string;
  proposedUse: string;
  effectiveFrom: string;
  effectiveTo: string;
  regulatoryReference: string;
  incompatibleRegulatoryReferences?: string[] | null;
  mandateKey?: string | null;
  mandateMode?: string | null;
  landUseCode?: string | null;
  incompatibleLandUseCodes?: string[] | null;
  applicantId?: string | null;
  parcelGeometry?: string | null;
  recordStatus?: string | null;
}

export interface ConflictEvaluationRequest {
  actionName: string;
  decisions: GovernanceDecisionSnapshot[];
}

export interface DetectedConflict {
  conflictId: string;
  conflictType: string;
  severity: string;
  detectionStatus: string;
  involvedDecisionIds: string[];
  involvedInstitutions: string[];
  subjectId: string;
  explanation: string;
  evidenceRule: string;
  recommendedAction: string;
  detectionTimestamp: string;
}

export interface ConflictEvaluationResponse {
  status: string;
  conflicts: DetectedConflict[];
}

export interface GovernanceEvaluationRequest {
  actionName: string;
  input: Record<string, unknown>;
}

export interface GovernanceRiskIndicator {
  indicatorId: string;
  category: string;
  severity: string;
  subjectId: string;
  evidenceSummary: string;
  triggeredRule: string;
  explanation: string;
  recommendedAction: string;
  scoreContribution: number;
  involvedActors: string[];
}

export interface GovernanceRiskResponse {
  subjectId: string;
  overallRiskScore: number;
  severity: string;
  requiresHumanReview: boolean;
  disclaimer: string;
  triggeredIndicators: GovernanceRiskIndicator[];
  evaluationTimestamp: string;
}

export interface GovernanceExplanationItem {
  itemId: string;
  sourceEngine: string;
  outcomeStatus: string;
  severity: string;
  reasonCode: string;
  title: string;
  plainLanguageExplanation: string;
  evidenceSummaries: string[];
  recommendedAction: string;
  requiresHumanAttention: boolean;
}

export interface GovernanceExplanationResponse {
  explanationId: string;
  subjectId: string;
  overallSeverity: string;
  requiresHumanReview: boolean;
  explanations: GovernanceExplanationItem[];
  evaluationTimestamp: string;
  disclaimer: string;
}

export interface GovernanceConsensusResponse {
  consensusEvaluationId: string;
  subjectId: string;
  outcome: string;
  summaryExplanation: string;
  recommendedAction: string;
  totalExpectedInstitutions: number;
  submittedCount: number;
  participatingCount: number;
  missingCount: number;
  approvalCount: number;
  rejectionCount: number;
  conditionalApprovalCount: number;
  abstentionCount: number;
  pendingCount: number;
  blockingInstitutionCount: number;
  quorumSatisfied: boolean;
  mandatoryInstitutionsSatisfied: boolean;
  consensusThresholdSatisfied: boolean;
  evaluationTimestamp: string;
}

export interface ConditionalVerificationStatus {
  conditionId: string;
  status: string;
  failureReason: string;
  notes: string;
  isMandatory: boolean;
  isSatisfied: boolean;
}

export interface ConditionalVerificationResponse {
  verificationId: string;
  subjectId: string;
  outcome: string;
  summaryExplanation: string;
  recommendedAction: string;
  totalConditionsCount: number;
  mandatoryConditionsCount: number;
  satisfiedMandatoryCount: number;
  unsatisfiedMandatoryCount: number;
  satisfiedOptionalCount: number;
  pendingConditionsCount: number;
  missingConditionsCount: number;
  expiredConditionsCount: number;
  conditionStatuses: ConditionalVerificationStatus[];
  evaluationTimestamp: string;
}

export class GovernanceRequestTimeoutError extends Error {
  constructor() {
    super("The governance service did not respond in time. No result was loaded.");
    this.name = "GovernanceRequestTimeoutError";
  }
}

export class GovernanceMalformedResponseError extends Error {
  constructor() {
    super("The governance service returned an unexpected response. No result was loaded.");
    this.name = "GovernanceMalformedResponseError";
  }
}

export function describeGovernanceRequestError(error: unknown): string {
  if (error instanceof GovernanceRequestTimeoutError || error instanceof GovernanceMalformedResponseError) return error.message;
  if (error instanceof ApiClientError) {
    if (error.status === 400) return "The service rejected the submitted evaluation. Check the supplied values and try again.";
    if (error.status === 404) return "The governance evaluation endpoint is not available.";
    if (error.status === 409) return "The evaluation could not be accepted because it conflicts with the current service state.";
    if (error.status === 401 || error.status === 403) return "This evaluation requires authentication or authorization that is not configured in this frontend.";
    if (error.status >= 500) return "The governance service is currently unavailable. No result was loaded.";
    return error.detail || "The governance evaluation could not be completed.";
  }
  if (error instanceof Error) return error.message;
  return "The governance evaluation could not be completed.";
}

async function postEvaluation<T>(path: string, payload: unknown): Promise<T> {
  const controller = new AbortController();
  const timeout = globalThis.setTimeout(() => controller.abort(), evaluationTimeoutMs);
  try {
    return await apiPost<T>(path, payload, { signal: controller.signal });
  } catch (error) {
    if (controller.signal.aborted) throw new GovernanceRequestTimeoutError();
    throw error;
  } finally {
    globalThis.clearTimeout(timeout);
  }
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function isFiniteNumber(value: unknown): value is number {
  return typeof value === "number" && Number.isFinite(value);
}

function isNullableString(value: unknown): value is string | null {
  return value === null || typeof value === "string";
}

function hasStringFields(value: unknown, fields: string[]): value is Record<string, unknown> {
  return isObject(value) && fields.every(field => typeof value[field] === "string");
}

function hasStringArray(value: unknown, field: string): value is Record<string, unknown> {
  return isObject(value) && Array.isArray(value[field]) && value[field].every(item => typeof item === "string");
}

function isRiskIndicator(value: unknown): value is GovernanceRiskIndicator {
  return hasStringFields(value, ["indicatorId", "category", "severity", "subjectId", "evidenceSummary", "triggeredRule", "explanation", "recommendedAction"])
    && typeof value.scoreContribution === "number" && Number.isFinite(value.scoreContribution)
    && hasStringArray(value, "involvedActors");
}

function isGovernanceRiskResponse(value: unknown): value is GovernanceRiskResponse {
  return hasStringFields(value, ["subjectId", "severity", "disclaimer", "evaluationTimestamp"])
    && typeof value.overallRiskScore === "number" && Number.isFinite(value.overallRiskScore)
    && typeof value.requiresHumanReview === "boolean"
    && Array.isArray(value.triggeredIndicators) && value.triggeredIndicators.every(isRiskIndicator);
}

function isExplanationItem(value: unknown): value is GovernanceExplanationItem {
  return hasStringFields(value, ["itemId", "sourceEngine", "outcomeStatus", "severity", "reasonCode", "title", "plainLanguageExplanation", "recommendedAction"])
    && hasStringArray(value, "evidenceSummaries")
    && typeof value.requiresHumanAttention === "boolean";
}

function isGovernanceExplanationResponse(value: unknown): value is GovernanceExplanationResponse {
  return hasStringFields(value, ["explanationId", "subjectId", "overallSeverity", "evaluationTimestamp", "disclaimer"])
    && typeof value.requiresHumanReview === "boolean"
    && Array.isArray(value.explanations) && value.explanations.every(isExplanationItem);
}

const consensusNumberFields = [
  "totalExpectedInstitutions", "submittedCount", "participatingCount", "missingCount",
  "approvalCount", "rejectionCount", "conditionalApprovalCount", "abstentionCount",
  "pendingCount", "blockingInstitutionCount",
];
const consensusBooleanFields = ["quorumSatisfied", "mandatoryInstitutionsSatisfied", "consensusThresholdSatisfied"];
function isGovernanceConsensusResponse(value: unknown): value is GovernanceConsensusResponse {
  return hasStringFields(value, ["consensusEvaluationId", "subjectId", "outcome", "summaryExplanation", "recommendedAction", "evaluationTimestamp"])
    && consensusNumberFields.every(field => typeof value[field] === "number" && Number.isInteger(value[field]))
    && consensusBooleanFields.every(field => typeof value[field] === "boolean");
}

function isConditionalVerificationStatus(value: unknown): value is ConditionalVerificationStatus {
  return hasStringFields(value, ["conditionId", "status", "failureReason", "notes"])
    && typeof value.isMandatory === "boolean" && typeof value.isSatisfied === "boolean";
}

const conditionCountFields = [
  "totalConditionsCount", "mandatoryConditionsCount", "satisfiedMandatoryCount",
  "unsatisfiedMandatoryCount", "satisfiedOptionalCount", "pendingConditionsCount",
  "missingConditionsCount", "expiredConditionsCount",
];
function isConditionalVerificationResponse(value: unknown): value is ConditionalVerificationResponse {
  return hasStringFields(value, ["verificationId", "subjectId", "outcome", "summaryExplanation", "recommendedAction", "evaluationTimestamp"])
    && conditionCountFields.every(field => typeof value[field] === "number" && Number.isInteger(value[field]))
    && Array.isArray(value.conditionStatuses) && value.conditionStatuses.every(isConditionalVerificationStatus);
}

function isComplianceResponse(value: unknown): value is ComplianceEvaluationResponse {
  const isViolation = (item: unknown) => hasStringFields(item, ["ruleCode", "message"]);
  const isCondition = (item: unknown) => hasStringFields(item, ["description"])
    && (item.requiredByDate === null || typeof item.requiredByDate === "string");
  const isFinding = (item: unknown) => hasStringFields(item, [
    "ruleCode", "ruleVersion", "category", "applicability", "status", "severity", "observedValueSummary",
    "expectedRequirement", "evidenceStatus", "calculationStatus", "sourceAuthority", "sourceDocument",
    "sourceSection", "recommendedAction",
  ]) && typeof item.isBlocking === "boolean" && typeof item.requiresHumanReview === "boolean";
  return isObject(value) && typeof value.status === "string"
    && Array.isArray(value.violations) && value.violations.every(isViolation)
    && Array.isArray(value.conditions) && value.conditions.every(isCondition)
    && (value.findings === undefined || value.findings === null || (Array.isArray(value.findings) && value.findings.every(isFinding)))
    && (value.deterministicEvaluationId === undefined || value.deterministicEvaluationId === null || typeof value.deterministicEvaluationId === "string");
}

function isConflictResponse(value: unknown): value is ConflictEvaluationResponse {
  const isConflict = (item: unknown) => hasStringFields(item, [
    "conflictId", "conflictType", "severity", "detectionStatus", "subjectId", "explanation",
    "evidenceRule", "recommendedAction", "detectionTimestamp",
  ]) && hasStringArray(item, "involvedDecisionIds") && hasStringArray(item, "involvedInstitutions");
  return isObject(value) && typeof value.status === "string"
    && Array.isArray(value.conflicts) && value.conflicts.every(isConflict);
}

/**
 * Runs the published legacy compliance evaluation contract. This is a user-initiated
 * mutation that persists an evaluation; callers must not retry it automatically.
 */
export async function evaluateCompliance(request: ComplianceEvaluationRequest): Promise<ComplianceEvaluationResponse> {
  const response = await postEvaluation<unknown>(`${governanceApiBasePath}/evaluate-compliance`, request);
  if (!isComplianceResponse(response)) throw new GovernanceMalformedResponseError();
  return response;
}

/**
 * Runs the published conflict evaluation contract for caller-supplied decision snapshots.
 * This is a user-initiated mutation that persists an evaluation; callers must not retry it automatically.
 */
export async function evaluateConflicts(request: ConflictEvaluationRequest): Promise<ConflictEvaluationResponse> {
  const response = await postEvaluation<unknown>(`${governanceApiBasePath}/detect-conflicts`, request);
  if (!isConflictResponse(response)) throw new GovernanceMalformedResponseError();
  return response;
}

async function evaluateStructured<T>(path: string, request: GovernanceEvaluationRequest, isResponse: (value: unknown) => value is T): Promise<T> {
  const response = await postEvaluation<unknown>(`${governanceApiBasePath}/${path}`, request);
  if (!isResponse(response)) throw new GovernanceMalformedResponseError();
  return response;
}

export function evaluateGovernanceRisk(request: GovernanceEvaluationRequest): Promise<GovernanceRiskResponse> {
  return evaluateStructured("evaluate-risk", request, isGovernanceRiskResponse);
}

export function explainGovernance(request: GovernanceEvaluationRequest): Promise<GovernanceExplanationResponse> {
  return evaluateStructured("explain", request, isGovernanceExplanationResponse);
}

export function evaluateConsensus(request: GovernanceEvaluationRequest): Promise<GovernanceConsensusResponse> {
  return evaluateStructured("evaluate-consensus", request, isGovernanceConsensusResponse);
}

export function verifyConditions(request: GovernanceEvaluationRequest): Promise<ConditionalVerificationResponse> {
  return evaluateStructured("verify-conditions", request, isConditionalVerificationResponse);
}

export interface ComplaintClassificationResponse {
  assessmentId: string;
  caseId: string;
  complaintText: string;
  modelVersion: string;
  predictedCategory: string;
  classProbabilities: Record<string, number>;
  advisoryNote: string;
  closedSetNote: string;
  assessedAtUtc: string;
}

const complaintCategories = [
  "Administrative / Procedural / Integrity",
  "Lease Revenue / Payment / Enforcement",
  "Unauthorized Allocation / Transfer / Use",
  "Protected / Environmental Lease Misuse",
] as const;

function isComplaintClassificationResponse(value: unknown): value is ComplaintClassificationResponse {
  if (!hasStringFields(value, [
    "assessmentId", "caseId", "complaintText", "modelVersion", "predictedCategory",
    "advisoryNote", "closedSetNote", "assessedAtUtc",
  ]) || !isObject(value.classProbabilities)) return false;

  const probabilities = value.classProbabilities as Record<string, unknown>;
  const probabilityKeys = Object.keys(probabilities);
  return complaintCategories.includes(value.predictedCategory as typeof complaintCategories[number])
    && probabilityKeys.length === complaintCategories.length
    && complaintCategories.every(category => {
      const probability = probabilities[category];
      return isFiniteNumber(probability) && probability >= 0 && probability <= 1;
    });
}

export interface WorkflowAnomalyResponse {
  caseId: string;
  modelVersion: string;
  anomalyScore: number;
  threshold: number;
  flagged: boolean;
  featureValues: {
    eventCount: number;
    elapsedDays: number;
    maxGapHours: number;
    meanGapHours: number;
    uniqueActivities: number;
    repeatedActivityCount: number;
    resourceHandoffs: number;
    institutionSwitches: number;
    distinctResources: number;
  };
  advisoryNote: string;
}

export interface WorkflowAnomalyRequest {
  caseId: string;
  events: Array<{ eventSeq: number; activity: string; institution: string; resource: string; timestamp: string }>;
}

function isWorkflowAnomalyResponse(value: unknown): value is WorkflowAnomalyResponse {
  if (!hasStringFields(value, ["caseId", "modelVersion", "advisoryNote"])
    || !isFiniteNumber(value.anomalyScore)
    || !isFiniteNumber(value.threshold)
    || typeof value.flagged !== "boolean"
    || !isObject(value.featureValues)) return false;

  const featureValues = value.featureValues as Record<string, unknown>;
  const featureFields = [
    "eventCount", "elapsedDays", "maxGapHours", "meanGapHours", "uniqueActivities",
    "repeatedActivityCount", "resourceHandoffs", "institutionSwitches", "distinctResources",
  ];
  return featureFields.every(field => isFiniteNumber(featureValues[field]));
}

export function parseWorkflowAnomalyRequest(value: unknown): WorkflowAnomalyRequest {
  if (!isObject(value) || typeof value.caseId !== "string" || value.caseId.trim().length === 0) {
    throw new Error("A nonblank caseId is required. No anomaly evaluation was submitted.");
  }
  if (value.caseId.length > 100) throw new Error("caseId must not exceed 100 characters. No anomaly evaluation was submitted.");
  if (!Array.isArray(value.events) || value.events.length === 0) {
    throw new Error("A completed trace must contain at least one event. No anomaly evaluation was submitted.");
  }
  if (value.events.length > 500) throw new Error("A trace may contain at most 500 events. No anomaly evaluation was submitted.");

  const events = value.events.map((event, index) => {
    if (!isObject(event)) throw new Error(`Event ${index + 1} is incomplete. No anomaly evaluation was submitted.`);
    const expectedSequence = index + 1;
    if (!Number.isInteger(event.eventSeq) || event.eventSeq !== expectedSequence) {
      throw new Error(`Events must be ordered with eventSeq ${expectedSequence} at position ${expectedSequence}. No anomaly evaluation was submitted.`);
    }
    for (const field of ["activity", "institution", "resource", "timestamp"] as const) {
      const fieldValue = event[field];
      if (typeof fieldValue !== "string" || fieldValue.trim().length === 0) {
        throw new Error(`Event ${expectedSequence} requires a nonblank ${field}. No anomaly evaluation was submitted.`);
      }
    }
    const activity = event.activity as string;
    const institution = event.institution as string;
    const resource = event.resource as string;
    const timestamp = event.timestamp as string;
    if (/z$/i.test(timestamp) || /[+-]\d{2}:?\d{2}$/.test(timestamp)) {
      throw new Error(`Event ${expectedSequence} timestamp must be a naive ISO-8601 value without a timezone. No anomaly evaluation was submitted.`);
    }
    if (Number.isNaN(Date.parse(timestamp))) {
      throw new Error(`Event ${expectedSequence} timestamp is invalid. No anomaly evaluation was submitted.`);
    }
    return {
      eventSeq: event.eventSeq,
      activity: activity.trim(),
      institution: institution.trim(),
      resource: resource.trim(),
      timestamp,
    };
  });
  return { caseId: value.caseId.trim(), events };
}

export interface EarlyScreeningIndicatorResult {
  indicatorType: string;
  evidenceState: string;
  requiresReview: boolean;
  needsEvidence: boolean;
  reasonCode: string;
  message: string;
  evidenceReference: string | null;
  recordedAtUtc: string | null;
}

export interface EarlyScreeningResponse {
  caseId: string;
  inputVersion: string;
  overallStatus: string;
  hasIncompleteEvidence: boolean;
  indicatorResults: EarlyScreeningIndicatorResult[];
}

export interface EarlyScreeningSeedInput {
  caseId: string;
  inputVersion: string;
  indicators: Array<{ indicatorType: string; evidenceState: string; evidenceReference?: string | null; recordedAtUtc?: string | null }>;
}

const screeningIndicatorTypes = [
  "LegalDispute", "UnauthorizedOccupation", "UnauthorizedConstruction", "FamilyOrInheritanceClaim",
  "MultipleClaimants", "UnresolvedObjection", "PreviousIllegalLandActivity",
];
const screeningEvidenceStates = ["VerifiedPresent", "VerifiedAbsent", "Unverified", "Missing", "Unavailable", "NotApplicable"];

export function parseEarlyScreeningSeedInput(value: unknown): EarlyScreeningSeedInput {
  if (!isObject(value) || typeof value.caseId !== "string" || value.caseId.trim().length === 0
    || typeof value.inputVersion !== "string" || value.inputVersion.trim().length === 0
    || !Array.isArray(value.indicators)) {
    throw new Error("Screening input requires nonblank caseId and inputVersion values plus an indicators array. No screening was submitted.");
  }
  const seen = new Set<string>();
  const indicators = value.indicators.map((indicator, index) => {
    if (!isObject(indicator)
      || typeof indicator.indicatorType !== "string" || !screeningIndicatorTypes.includes(indicator.indicatorType)
      || typeof indicator.evidenceState !== "string" || !screeningEvidenceStates.includes(indicator.evidenceState)) {
      throw new Error(`Screening indicator ${index + 1} has an invalid type or evidence state. No screening was submitted.`);
    }
    if (seen.has(indicator.indicatorType)) throw new Error(`Screening indicator ${indicator.indicatorType} is duplicated. No screening was submitted.`);
    seen.add(indicator.indicatorType);
    if (indicator.evidenceReference !== undefined && !isNullableString(indicator.evidenceReference)) {
      throw new Error(`Screening indicator ${index + 1} has an invalid evidence reference. No screening was submitted.`);
    }
    if (indicator.recordedAtUtc !== undefined && !isNullableString(indicator.recordedAtUtc)) {
      throw new Error(`Screening indicator ${index + 1} has an invalid recorded timestamp. No screening was submitted.`);
    }
    return {
      indicatorType: indicator.indicatorType,
      evidenceState: indicator.evidenceState,
      evidenceReference: indicator.evidenceReference as string | null | undefined,
      recordedAtUtc: indicator.recordedAtUtc as string | null | undefined,
    };
  });
  return { caseId: value.caseId.trim(), inputVersion: value.inputVersion.trim(), indicators };
}

function isEarlyScreeningResponse(value: unknown): value is EarlyScreeningResponse {
  const validStatuses = ["Clear", "ReviewRequired", "InsufficientInformation"];
  const isIndicator = (item: unknown) => hasStringFields(item, [
    "indicatorType", "evidenceState", "reasonCode", "message",
  ]) && typeof item.requiresReview === "boolean"
    && typeof item.needsEvidence === "boolean"
    && isNullableString(item.evidenceReference)
    && isNullableString(item.recordedAtUtc);
  return hasStringFields(value, ["caseId", "inputVersion", "overallStatus"])
    && validStatuses.includes(value.overallStatus as string)
    && typeof value.hasIncompleteEvidence === "boolean"
    && Array.isArray(value.indicatorResults)
    && value.indicatorResults.every(isIndicator);
}

export interface AuditVerificationReceiptResponse {
  recordId: string;
  engineType: string;
  actionName: string;
  governanceStatus: string;
  decisionRecordedAt: string;
  recordHash: string;
  verificationStatus: string;
  anchorStatus: string;
  ledgerTransactionReference: string | null;
  blockNumber: number | null;
  anchoredAtUtc: string | null;
  verifiedAtUtc: string | null;
  explanation: string;
  recordVersion: string;
}

export interface AuditCaseHistoryItem {
  auditRecordId: string;
  caseId: string;
  operationType: string;
  hashVersion: string;
  createdAtUtc: string;
  anchorStatus: string;
  transactionReference: string | null;
  blockNumber: number | null;
  lastVerificationStatus: string;
  lastVerifiedAtUtc: string | null;
}

export type PersistedHistoryItem = Readonly<Record<string, unknown>>;

export interface WorkflowProjectionJourney {
  projectionId: string;
  caseId: string;
  workflowRunId: string;
  workflowState: string;
  traceCompleteness: string;
  assessmentEligibility: string;
  legalRouteLabel: { availability: string; value: string | null };
  isSynthetic: boolean;
  provenanceKind: string;
  purpose: string;
  syntheticSeedId: string | null;
  syntheticSeedVersion: string | null;
  events: Array<{
    eventSequence: number;
    activity: string;
    institution: string;
    resource: string;
    actorRole: string;
    timestamp: string;
    status: string;
    evidenceReference: string | null;
  }>;
  routeApplicability: {
    requiredClearances: { availability: string; value: string[] | null };
    higherApprovalApplicable: { availability: string; value: boolean | null };
    registrationApplicable: { availability: string; value: boolean | null };
  };
  completeProposalBoundary: WorkflowJourneyBoundary;
  applicantHandoverBoundary: WorkflowJourneyBoundary;
  ingestedAtUtc: string;
}

export interface WorkflowJourneyBoundary {
  availability: string;
  occurredAt: string | null;
  source: string;
  sourceReference: string;
}

export interface PersistedWorkflowAnomalyAssessment {
  assessmentId: string;
  workflowRunId: string;
  caseId: string;
  workflowProjectionId: string | null;
  modelVersion: string;
  anomalyScore: number;
  threshold: number;
  flagged: boolean;
  eventCount: number;
  elapsedDays: number;
  maxGapHours: number;
  meanGapHours: number;
  uniqueActivities: number;
  repeatedActivityCount: number;
  resourceHandoffs: number;
  institutionSwitches: number;
  distinctResources: number;
  advisoryNote: string;
  assessedAtUtc: string;
}

function isWorkflowProjectionJourney(value: unknown): value is WorkflowProjectionJourney {
  return hasStringFields(value, [
    "projectionId", "caseId", "workflowRunId", "workflowState", "traceCompleteness",
    "assessmentEligibility", "provenanceKind", "purpose", "ingestedAtUtc",
  ]) && typeof value.isSynthetic === "boolean"
    && Array.isArray(value.events)
    && value.events.every(event => hasStringFields(event, ["activity", "institution", "resource", "actorRole", "timestamp", "status"])
      && typeof event.eventSequence === "number")
    && isObject(value.routeApplicability)
    && isObject(value.completeProposalBoundary)
    && isObject(value.applicantHandoverBoundary);
}

function isPersistedWorkflowAnomalyAssessment(value: unknown): value is PersistedWorkflowAnomalyAssessment {
  const numericFields = [
    "anomalyScore", "threshold", "eventCount", "elapsedDays", "maxGapHours", "meanGapHours",
    "uniqueActivities", "repeatedActivityCount", "resourceHandoffs", "institutionSwitches", "distinctResources",
  ];
  return hasStringFields(value, ["assessmentId", "workflowRunId", "caseId", "modelVersion", "advisoryNote", "assessedAtUtc"])
    && typeof value.flagged === "boolean"
    && numericFields.every(field => isFiniteNumber(value[field]));
}

function isHistory(value: unknown): value is PersistedHistoryItem[] {
  return Array.isArray(value) && value.every(isObject);
}

async function getHistory(path: string, caseId: string, limit = 100): Promise<PersistedHistoryItem[]> {
  const response = await apiGet<unknown>(`${governanceApiBasePath}/${path}/${encodeURIComponent(caseId)}?limit=${limit}`);
  if (!isHistory(response)) throw new GovernanceMalformedResponseError();
  return response;
}

export const getScreeningHistoryByCase = (caseId: string, limit = 100) => getHistory("screening/history/cases", caseId, limit);
export const getComplaintHistoryByCase = (caseId: string, limit = 100) => getHistory("complaints/history/cases", caseId, limit);
export const getWorkflowAnomalyHistoryByCase = (caseId: string, limit = 100) => getHistory("workflow-anomalies/history/cases", caseId, limit);
export const getLeaseTimingHistoryByCase = (caseId: string, limit = 100) => getHistory("lease-timing/history/cases", caseId, limit);
export const getReferralHistoryByCase = (caseId: string, limit = 100) => getHistory("referrals/history/cases", caseId, limit);

export async function getWorkflowProjectionJourney(caseId: string): Promise<WorkflowProjectionJourney> {
  const response = await apiGet<unknown>(`${governanceApiBasePath}/workflow-projections/cases/${encodeURIComponent(caseId)}`);
  if (!isWorkflowProjectionJourney(response)) throw new GovernanceMalformedResponseError();
  return response;
}

export async function getPersistedWorkflowAnomalyHistory(caseId: string): Promise<PersistedWorkflowAnomalyAssessment[]> {
  const response = await apiGet<unknown>(`${governanceApiBasePath}/workflow-anomalies/history/cases/${encodeURIComponent(caseId)}?limit=100`);
  if (!Array.isArray(response) || !response.every(isPersistedWorkflowAnomalyAssessment)) {
    throw new GovernanceMalformedResponseError();
  }
  return response;
}

export async function assessPersistedWorkflowProjection(
  projection: Pick<WorkflowProjectionJourney, "projectionId" | "caseId" | "workflowRunId">,
): Promise<PersistedWorkflowAnomalyAssessment> {
  const response = await postEvaluation<unknown>(`${governanceApiBasePath}/workflow-anomalies/assess`, {
    workflowProjectionId: projection.projectionId,
    caseId: projection.caseId,
    workflowRunId: projection.workflowRunId,
  });
  if (!isPersistedWorkflowAnomalyAssessment(response)) throw new GovernanceMalformedResponseError();
  return response;
}

export async function getAuditHistoryByCase(caseId: string, limit = 100): Promise<AuditCaseHistoryItem[]> {
  const response = await getHistory("audits/history/cases", caseId, limit);
  if (!response.every(item => hasStringFields(item, ["auditRecordId", "caseId", "operationType", "hashVersion", "createdAtUtc", "anchorStatus", "lastVerificationStatus"])))
    throw new GovernanceMalformedResponseError();
  return response as unknown as AuditCaseHistoryItem[];
}

function isAuditVerificationReceiptResponse(value: unknown): value is AuditVerificationReceiptResponse {
  return hasStringFields(value, [
    "recordId", "engineType", "actionName", "governanceStatus", "decisionRecordedAt",
    "recordHash", "verificationStatus", "anchorStatus", "explanation", "recordVersion",
  ]) && isNullableString(value.ledgerTransactionReference)
    && (value.blockNumber === null || (typeof value.blockNumber === "number" && Number.isInteger(value.blockNumber)))
    && isNullableString(value.anchoredAtUtc)
    && isNullableString(value.verifiedAtUtc);
}

export async function classifyComplaint(request: { caseId: string; applicantId?: string; complaintText: string }): Promise<ComplaintClassificationResponse> {
  const response = await postEvaluation<unknown>(`${governanceApiBasePath}/classify-complaint`, request);
  if (!isComplaintClassificationResponse(response)) throw new GovernanceMalformedResponseError();
  return response;
}

export async function predictWorkflowAnomaly(request: WorkflowAnomalyRequest): Promise<WorkflowAnomalyResponse> {
  const response = await postEvaluation<unknown>(`${governanceApiBasePath}/predict-workflow-anomaly`, request);
  if (!isWorkflowAnomalyResponse(response)) throw new GovernanceMalformedResponseError();
  return response;
}

export async function screenEarlyGovernance(request: {
  assessmentId: string;
  workflowRunId: string;
  caseId: string;
  inputVersion: string;
  indicators: Array<{
    indicatorType: string;
    evidenceState: string;
    evidenceReference?: string | null;
    recordedAtUtc?: string | null;
  }>;
}): Promise<EarlyScreeningResponse> {
  const response = await postEvaluation<unknown>(`${governanceApiBasePath}/screen-early-governance`, request);
  if (!isEarlyScreeningResponse(response)) throw new GovernanceMalformedResponseError();
  return response;
}

export async function getAuditReceipt(auditRecordId: string): Promise<AuditVerificationReceiptResponse> {
  const response = await apiGet<unknown>(`${governanceApiBasePath}/audits/${auditRecordId}/receipt`);
  if (!isAuditVerificationReceiptResponse(response)) throw new GovernanceMalformedResponseError();
  return response;
}

export async function verifyAuditIntegrity(auditRecordId: string): Promise<AuditVerificationReceiptResponse> {
  const response = await postEvaluation<unknown>(`${governanceApiBasePath}/audits/${auditRecordId}/verify`, {});
  if (!isAuditVerificationReceiptResponse(response)) throw new GovernanceMalformedResponseError();
  return response;
}

export interface IGovernanceService {
  /**
   * Fetches the summary metrics and latest case assessments for the dashboard.
   */
  getDashboard(): Promise<GovernanceDashboardData>;
}

export class MockGovernanceService implements IGovernanceService {
  private readonly delayMs: number;

  constructor(delayMs = 150) {
    this.delayMs = delayMs;
  }

  async getDashboard(): Promise<GovernanceDashboardData> {
    if (this.delayMs > 0) {
      await new Promise((resolve) => setTimeout(resolve, this.delayMs));
    }
    // Return deep copy to prevent mutation
    return JSON.parse(JSON.stringify(MOCK_GOVERNANCE_DASHBOARD_DATA));
  }
}

/**
 * Explicit unavailable state until authorized read endpoints are published or when unconfigured.
 * Evaluation POST endpoints must never be used as a retrieval fallback.
 */
export class GovernanceUnavailableError extends Error {}

function isGovernanceDashboardData(value: unknown): value is GovernanceDashboardData {
  if (!isObject(value) || !isObject(value.summary) || !Array.isArray(value.cases)) return false;
  const summary = value.summary;
  const countFields = ["totalCases", "totalAssessments", "clearCount", "reviewRequiredCount", "insufficientInfoCount"];
  const summaryIsValid = countFields.every(field => typeof summary[field] === "number" && Number.isInteger(summary[field]) && summary[field] >= 0)
    && typeof summary.lastUpdated === "string";
  const caseStringFields = [
    "assessmentId", "caseId", "parcelReference", "applicantReference", "overallStatus",
    "complianceStatus", "conflictStatus", "earlyScreeningStatus", "anomalyStatus",
    "consensusOutcome", "evaluatedAt", "version", "summaryFinding", "rationale", "recommendedNextStep",
  ];
  return summaryIsValid && value.cases.every(item => hasStringFields(item, caseStringFields)
    && (item.workflowRunId === null || typeof item.workflowRunId === "string"));
}

export class LiveGovernanceService implements IGovernanceService {
  async getDashboard(): Promise<GovernanceDashboardData> {
    try {
      const response = await apiGet<unknown>(`${governanceApiBasePath}/dashboard`);
      if (!isGovernanceDashboardData(response)) throw new GovernanceMalformedResponseError();
      return response;
    } catch (error) {
      if (error instanceof ApiClientError && error.status === 404) {
        throw new GovernanceUnavailableError("Live governance dashboard endpoint is not configured.");
      }
      throw error;
    }
  }
}

const demoService = new MockGovernanceService();
const liveService = new LiveGovernanceService();

export function getGovernanceService(mode: "live" | "demo"): IGovernanceService {
  return mode === "demo" ? demoService : liveService;
}


