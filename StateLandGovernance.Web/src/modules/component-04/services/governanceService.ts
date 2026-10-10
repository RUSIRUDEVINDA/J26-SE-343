import type { GovernanceDashboardData } from "../types/governance";
import { ApiClientError, apiPost } from "../../../lib/apiClient";
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
 * Explicit unavailable state until authorized read endpoints are published.
 * Evaluation POST endpoints must never be used as a retrieval fallback.
 */
export class GovernanceUnavailableError extends Error {}
class UnavailableGovernanceService implements IGovernanceService {
  async getDashboard(): Promise<GovernanceDashboardData> {
    throw new GovernanceUnavailableError("Live governance assessments are not available yet. No records have been loaded or evaluated.");
  }
}
const demoService = new MockGovernanceService();
const liveService = new UnavailableGovernanceService();
// No guessed GET route and no automatic fallback to fictional data.
export function getGovernanceService(mode: "live" | "demo"): IGovernanceService {
  return mode === "demo" ? demoService : liveService;
}
