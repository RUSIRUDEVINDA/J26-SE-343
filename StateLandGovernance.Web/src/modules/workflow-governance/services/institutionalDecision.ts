export interface InstitutionalDecisionFormState {
  busy: boolean;
  outcome: string;
  reason: string;
  conditions: string;
  stageStatus: string;
  hasOwnDecision: boolean;
  revisionReason?: string;
}

/**
 * Validates whether the institutional decision submit button should be enabled.
 *
 * Rules:
 * - Evidence file is OPTIONAL (does not block submission).
 * - Decision outcome must be chosen.
 * - Explicit reason is required.
 * - ApprovedWithConditions requires at least one condition.
 * - Submitting / busy state disables to prevent duplicate submissions.
 * - New decisions require stage to be InProgress.
 * - Decision revisions require revisionReason.
 */
export function isInstitutionalDecisionSubmittable(params: InstitutionalDecisionFormState): boolean {
  if (params.busy) return false;
  if (!params.outcome || !params.outcome.trim()) return false;
  if (!params.reason || !params.reason.trim()) return false;
  if (params.outcome === "ApprovedWithConditions") {
    const hasValidCondition = params.conditions.split("\n").some((c) => c.trim().length > 0);
    if (!hasValidCondition) return false;
  }
  if (params.hasOwnDecision) {
    if (!params.revisionReason || !params.revisionReason.trim()) return false;
  } else {
    if (params.stageStatus !== "InProgress") return false;
  }
  return true;
}

/**
 * Builds the stage decision submission payload, ensuring optional documents are handled cleanly.
 */
export function buildStageDecisionPayload(params: {
  expectedExecutionRevision: number;
  outcome: string;
  reason: string;
  conditions: string;
  evidence?: { documentId: string; versionId: string };
  priorDecisionId?: string;
  revisionReason?: string;
}) {
  return {
    expectedExecutionRevision: params.expectedExecutionRevision,
    outcome: params.outcome,
    reason: params.reason,
    conditions: params.conditions.split("\n").map((x) => x.trim()).filter(Boolean),
    documents: params.evidence ? [params.evidence] : [],
    priorDecisionId: params.priorDecisionId,
    revisionReason: params.revisionReason || null,
  };
}

