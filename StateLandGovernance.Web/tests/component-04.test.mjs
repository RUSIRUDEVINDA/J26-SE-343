import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import ts from "typescript";
import { createRequire } from "node:module";
const loadCommonJs = createRequire(import.meta.url);
// Use the installed TypeScript compiler; no additional test framework.
loadCommonJs.extensions[".ts"] = (module, filename) => module._compile(
  ts.transpileModule(fs.readFileSync(filename, "utf8"), { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText, filename);
process.env.NEXT_PUBLIC_API_BASE_URL = "http://governance.test";
const { filterAssessments, conflictTone, formatGovernanceTime } = loadCommonJs("../src/modules/component-04/adapters/presentation.ts");
const {
  getGovernanceService, MockGovernanceService, GovernanceUnavailableError,
  GovernanceMalformedResponseError, GovernanceRequestTimeoutError,
  describeGovernanceRequestError, evaluateCompliance, evaluateConflicts,
  evaluateGovernanceRisk, explainGovernance, evaluateConsensus, verifyConditions,
} = loadCommonJs("../src/modules/component-04/services/governanceService.ts");
const { ApiClientError } = loadCommonJs("../src/lib/apiClient.ts");
const { MOCK_GOVERNANCE_DASHBOARD_DATA: fixture } = loadCommonJs("../src/modules/component-04/services/mockGovernanceData.ts");
const { demoAnomaly } = loadCommonJs("../src/modules/component-04/services/demoEvidence.ts");

test("Live mode never loads demo results or makes speculative API calls", async () => {
  let requests = 0;
  const original = global.fetch;
  global.fetch = async () => { requests++; throw new Error("Unexpected network request"); };
  try {
    await assert.rejects(getGovernanceService("live").getDashboard(), GovernanceUnavailableError);
    assert.equal(requests, 0);
  } finally { global.fetch = original; }
});
test("Explicit demo reads are isolated and use unique assessment identities", async () => {
  const service = new MockGovernanceService(0);
  const data = await service.getDashboard();
  data.cases[0].caseId = "mutated";
  const reread = await service.getDashboard();
  assert.notEqual(reread.cases[0].caseId, "mutated");
  assert.equal(new Set(reread.cases.map(item => item.assessmentId)).size, reread.cases.length);
  assert.ok(reread.cases.every(item => item.caseId.startsWith("DEMO-")));
  assert.equal(reread.summary.totalAssessments, reread.cases.length);
});
test("Search and status filtering intersect, preserve history identity and handle empty results", () => {
  const repeatedCase = { ...fixture.cases[0], assessmentId: "DEMO-HISTORY-OLD" };
  const records = [...fixture.cases, repeatedCase];
  assert.equal(filterAssessments(records, "demo-history-old", "ALL")[0].assessmentId, repeatedCase.assessmentId);
  assert.equal(filterAssessments(records, fixture.cases[0].caseId, "ALL").length, 2);
  assert.equal(filterAssessments(records, fixture.cases[0].caseId, "CLEAR").length, 0);
  assert.equal(filterAssessments(records, "not-a-case", "ALL").length, 0);
  assert.equal(filterAssessments(records, "  fictional  ", "CLEAR").length, 2);
});
test("NotAssessed and unknown conflict results are neutral, not failures", () => {
  assert.equal(conflictTone("NotAssessed"), "neutral");
  assert.equal(conflictTone("Unrecognized"), "neutral");
  assert.equal(conflictTone("NoConflicts"), "success");
  assert.equal(conflictTone("ConflictsDetected"), "danger");
});
test("Officer timestamps use Colombo while naive event timestamps remain unassigned", () => {
  assert.match(formatGovernanceTime("2026-10-01T09:00:00Z"), /14:30.*Asia\/Colombo/);
  assert.equal(formatGovernanceTime("2026-10-01T09:00:00"), "2026-10-01T09:00:00 (timezone not supplied)");
  assert.equal(formatGovernanceTime("invalidZ"), "Timestamp unavailable");
});
test("Fictional workflow anomaly follows higher-score and score-greater-than-threshold semantics", () => {
  assert.ok(demoAnomaly.anomalyScore > demoAnomaly.threshold);
  assert.equal(demoAnomaly.flagged, true);
});
test("Compliance evaluation uses only the published ASP.NET Core route and does not retry", async () => {
  const original = global.fetch;
  const requests = [];
  global.fetch = async (url, init) => {
    requests.push({ url, init });
    return { ok: true, json: async () => ({ status: "Compliant", violations: [], conditions: [], findings: [], deterministicEvaluationId: "evaluation-1" }) };
  };
  try {
    const result = await evaluateCompliance({ actionName: "Review", leaseDurationYears: 30, proposedUse: "Agriculture", leaseAmount: 1000, zoningArea: "Zone A" });
    assert.equal(result.status, "Compliant");
    assert.equal(requests.length, 1);
    assert.equal(requests[0].url, "http://governance.test/api/governance-intelligence/evaluate-compliance");
    assert.equal(requests[0].init.method, "POST");
  } finally { global.fetch = original; }
});
test("Conflict evaluation uses the published route with decision snapshots", async () => {
  const original = global.fetch;
  const decision = { decisionId: "d1", subjectId: "s1", institutionName: "Land Authority", authorityLevel: "National", decisionType: "Allocation", proposedUse: "Agriculture", effectiveFrom: "2026-01-01T00:00:00Z", effectiveTo: "2027-01-01T00:00:00Z", regulatoryReference: "REF-1" };
  global.fetch = async (url, init) => {
    assert.equal(url, "http://governance.test/api/governance-intelligence/detect-conflicts");
    assert.equal(init.method, "POST");
    return { ok: true, json: async () => ({ status: "NoConflicts", conflicts: [] }) };
  };
  try {
    const result = await evaluateConflicts({ actionName: "Review", decisions: [decision] });
    assert.equal(result.status, "NoConflicts");
  } finally { global.fetch = original; }
});
test("Risk, explanation, consensus, and condition evaluations use validated published DTO responses", async () => {
  const original = global.fetch;
  const requests = [];
  const zeroCounts = {
    totalExpectedInstitutions: 0, submittedCount: 0, participatingCount: 0, missingCount: 0,
    approvalCount: 0, rejectionCount: 0, conditionalApprovalCount: 0, abstentionCount: 0,
    pendingCount: 0, blockingInstitutionCount: 0,
  };
  const zeroConditionCounts = {
    totalConditionsCount: 0, mandatoryConditionsCount: 0, satisfiedMandatoryCount: 0,
    unsatisfiedMandatoryCount: 0, satisfiedOptionalCount: 0, pendingConditionsCount: 0,
    missingConditionsCount: 0, expiredConditionsCount: 0,
  };
  const responses = [
    ["evaluate-risk", { subjectId: "subject-1", overallRiskScore: 0, severity: "Low", requiresHumanReview: false, disclaimer: "Advisory.", triggeredIndicators: [], evaluationTimestamp: "2026-10-01T00:00:00Z" }],
    ["explain", { explanationId: "explain-1", subjectId: "subject-1", overallSeverity: "Low", requiresHumanReview: false, explanations: [], evaluationTimestamp: "2026-10-01T00:00:00Z", disclaimer: "Advisory." }],
    ["evaluate-consensus", { consensusEvaluationId: "consensus-1", subjectId: "subject-1", outcome: "InsufficientQuorum", summaryExplanation: "No positions.", recommendedAction: "Review.", ...zeroCounts, quorumSatisfied: false, mandatoryInstitutionsSatisfied: true, consensusThresholdSatisfied: false, evaluationTimestamp: "2026-10-01T00:00:00Z" }],
    ["verify-conditions", { verificationId: "verification-1", subjectId: "subject-1", outcome: "PendingEvidence", summaryExplanation: "Evidence pending.", recommendedAction: "Review.", ...zeroConditionCounts, conditionStatuses: [], evaluationTimestamp: "2026-10-01T00:00:00Z" }],
  ];
  global.fetch = async (url, init) => {
    const entry = responses.find(([path]) => url.endsWith(`/${path}`));
    assert.ok(entry, `unexpected route ${url}`);
    requests.push({ url, init });
    return { ok: true, json: async () => entry[1] };
  };
  try {
    const request = { actionName: "Demonstration", input: { subjectId: "subject-1" } };
    assert.equal((await evaluateGovernanceRisk(request)).subjectId, "subject-1");
    assert.equal((await explainGovernance(request)).explanationId, "explain-1");
    assert.equal((await evaluateConsensus(request)).consensusEvaluationId, "consensus-1");
    assert.equal((await verifyConditions(request)).verificationId, "verification-1");
    assert.deepEqual(requests.map(({ url }) => url), responses.map(([path]) => `http://governance.test/api/governance-intelligence/${path}`));
    assert.ok(requests.every(({ init }) => init.method === "POST"));
    assert.ok(requests.every(({ init }) => init.body === JSON.stringify(request)));
  } finally { global.fetch = original; }
});
test("Malformed nested evaluation responses fail closed", async () => {
  const original = global.fetch;
  global.fetch = async () => ({ ok: true, json: async () => ({
    subjectId: "subject-1", overallRiskScore: "not-a-score", severity: "Low", requiresHumanReview: false,
    disclaimer: "", triggeredIndicators: [], evaluationTimestamp: "2026-10-01T00:00:00Z",
  }) });
  try {
    await assert.rejects(
      evaluateGovernanceRisk({ actionName: "Review", input: { subjectId: "subject-1" } }),
      GovernanceMalformedResponseError,
    );
  } finally { global.fetch = original; }
});
test("Evaluation errors distinguish validation, missing routes, conflicts, authorization, and server outage", () => {
  const responseError = (status, detail = "Private response detail") => new ApiClientError({ status, title: "Request failed", detail });
  assert.match(describeGovernanceRequestError(responseError(400)), /Check the supplied values/);
  assert.match(describeGovernanceRequestError(responseError(404)), /endpoint is not available/);
  assert.match(describeGovernanceRequestError(responseError(409)), /conflicts with the current service state/);
  assert.match(describeGovernanceRequestError(responseError(401)), /authentication or authorization/);
  assert.match(describeGovernanceRequestError(responseError(500)), /currently unavailable/);
  assert.doesNotMatch(describeGovernanceRequestError(responseError(500)), /Private response detail/);
});
test("Evaluation timeout aborts the request and reports timeout instead of success", async () => {
  const originalFetch = global.fetch;
  const originalSetTimeout = global.setTimeout;
  global.fetch = async (_url, init) => new Promise((_resolve, reject) => {
    init.signal.addEventListener("abort", () => reject(new Error("aborted")));
  });
  global.setTimeout = callback => {
    queueMicrotask(callback);
    return 0;
  };
  try {
    await assert.rejects(
      evaluateGovernanceRisk({ actionName: "Review", input: { subjectId: "subject-1" } }),
      GovernanceRequestTimeoutError,
    );
  } finally {
    global.fetch = originalFetch;
    global.setTimeout = originalSetTimeout;
  }
});
