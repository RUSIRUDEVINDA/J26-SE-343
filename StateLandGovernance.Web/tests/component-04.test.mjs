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
  getGovernanceService, MockGovernanceService,
  GovernanceMalformedResponseError, GovernanceRequestTimeoutError,
  describeGovernanceRequestError, evaluateCompliance, evaluateConflicts,
  evaluateGovernanceRisk, explainGovernance, evaluateConsensus, verifyConditions,
  classifyComplaint, predictWorkflowAnomaly, parseWorkflowAnomalyRequest,
  screenEarlyGovernance, parseEarlyScreeningSeedInput, getAuditReceipt, verifyAuditIntegrity,
} = loadCommonJs("../src/modules/component-04/services/governanceService.ts");
const { ApiClientError } = loadCommonJs("../src/lib/apiClient.ts");
const { MOCK_GOVERNANCE_DASHBOARD_DATA: fixture } = loadCommonJs("../src/modules/component-04/services/mockGovernanceData.ts");
const { demoAnomaly } = loadCommonJs("../src/modules/component-04/services/demoEvidence.ts");
const { getComplianceEvaluationPreset } = loadCommonJs("../src/modules/component-04/services/proposalPresets.ts");

test("Component 4 landing dashboard contains only the business overview", () => {
  const source = fs.readFileSync("src/modules/component-04/components/GovernanceDashboard.tsx", "utf8");
  assert.doesNotMatch(source, /FinalPp1Evidence|PP1 Final Seed Scenarios/);
  for (const section of ["General live governance overview", "Engine Summary", "Recent Cases", "Governance Notice"]) {
    assert.match(source, new RegExp(section));
  }
});

test("Component 4 layout forwards its complete module navigation to the shared sidebar", () => {
  const layoutSource = fs.readFileSync("src/app/component-04/layout.tsx", "utf8");
  const pageLayoutSource = fs.readFileSync("src/shared/components/PageLayout.tsx", "utf8");

  for (const label of [
    "Overview", "Progressive screening", "Assessment history", "Complaint intelligence",
    "Post-workflow analysis", "Governance follow-up", "Integrity and audit",
  ]) {
    assert.match(layoutSource, new RegExp(`label: "${label}"`));
  }

  assert.match(pageLayoutSource, /items=\{navigationItems\}/);
  assert.match(pageLayoutSource, /label=\{navigationLabel\}/);
  assert.match(pageLayoutSource, /basePath=\{navigationBasePath\}/);
  assert.match(pageLayoutSource, /compactMobile=\{navigationCompactMobile\}/);
});

test("NPD compliance presets provide distinct complete, incomplete and adverse proposal JSON", () => {
  const clean = getComplianceEvaluationPreset("proposal-1");
  const incomplete = getComplianceEvaluationPreset("proposal-2");
  const adverse = getComplianceEvaluationPreset("proposal-3");
  const cleanInput = JSON.parse(clean.inputJson);
  const incompleteInput = JSON.parse(incomplete.inputJson);
  const adverseInput = JSON.parse(adverse.inputJson);

  assert.equal(clean.caseId, "PP1-FINAL-CLEAR-001");
  assert.equal(cleanInput.budget.submittedProjectBudget, cleanInput.financing.financingSources[0].amount);
  assert.equal(incompleteInput.location.gramaNiladhariDivision, "");
  assert.equal(incompleteInput.economicAppraisal.selectedMethod, "");
  assert.notEqual(adverseInput.budget.submittedProjectBudget, adverseInput.financing.financingSources[0].amount);
  assert.equal(adverseInput.evidenceReferences[0].evidenceId, adverseInput.evidenceReferences[1].evidenceId);
});

test("Live dashboard failure never falls back to demo success", async () => {
  let requests = 0;
  const original = global.fetch;
  global.fetch = async () => { requests++; throw new Error("Backend offline"); };
  try {
    await assert.rejects(getGovernanceService("live").getDashboard(), /Backend offline/);
    assert.equal(requests, 1);
  } finally { global.fetch = original; }
});
test("Live dashboard success renders only the real response", async () => {
  const original = global.fetch;
  const live = { summary: { totalCases: 0, totalAssessments: 0, clearCount: 0, reviewRequiredCount: 0, insufficientInfoCount: 0, lastUpdated: "2026-10-11T00:00:00Z" }, cases: [] };
  global.fetch = async url => {
    assert.equal(url, "http://governance.test/api/governance-intelligence/dashboard");
    return { ok: true, json: async () => live };
  };
  try {
    assert.deepEqual(await getGovernanceService("live").getDashboard(), live);
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
test("Component 4 browser requests do not create Component 1 authentication sessions", async () => {
  const originalFetch = global.fetch;
  const originalWindow = global.window;
  const requests = [];
  global.window = {
    location: { hostname: "localhost" },
    sessionStorage: {
      getItem: () => null,
      setItem: () => undefined,
      removeItem: () => undefined,
    },
  };
  global.fetch = async (url) => {
    requests.push(url);
    return {
      ok: true,
      json: async () => ({
        status: "Compliant",
        violations: [],
        conditions: [],
        findings: [],
        deterministicEvaluationId: "evaluation-no-land-session",
      }),
    };
  };

  try {
    await evaluateCompliance({
      actionName: "Review",
      leaseDurationYears: 30,
      proposedUse: "Agriculture",
      leaseAmount: 1000,
      zoningArea: "Zone A",
    });
    assert.deepEqual(requests, [
      "http://governance.test/api/governance-intelligence/evaluate-compliance",
    ]);
  } finally {
    global.fetch = originalFetch;
    if (originalWindow === undefined) delete global.window;
    else global.window = originalWindow;
  }
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

test("Complaint v2 response preserves assessment identity, original text, and exactly four probabilities", async () => {
  const original = global.fetch;
  const categories = [
    "Administrative / Procedural / Integrity",
    "Lease Revenue / Payment / Enforcement",
    "Unauthorized Allocation / Transfer / Use",
    "Protected / Environmental Lease Misuse",
  ];
  const response = {
    assessmentId: "0199b9bc-9898-7000-8000-000000000001", caseId: "FICTIONAL-C4-001",
    complaintText: "Fictional complaint text.", modelVersion: "v2", predictedCategory: categories[0],
    classProbabilities: Object.fromEntries(categories.map((category, index) => [category, index === 0 ? 0.7 : 0.1])),
    advisoryNote: "Machine prediction for human review.", closedSetNote: "Closed-set taxonomy.", assessedAtUtc: "2026-10-11T00:00:00Z",
  };
  global.fetch = async (url, init) => {
    assert.equal(url, "http://governance.test/api/governance-intelligence/classify-complaint");
    assert.equal(init.method, "POST");
    return { ok: true, json: async () => response };
  };
  try {
    const result = await classifyComplaint({ caseId: response.caseId, complaintText: response.complaintText });
    assert.equal(result.modelVersion, "v2");
    assert.equal(result.assessmentId, response.assessmentId);
    assert.deepEqual(Object.keys(result.classProbabilities), categories);
  } finally { global.fetch = original; }
});

test("Malformed complaint success response fails closed", async () => {
  const original = global.fetch;
  global.fetch = async () => ({ ok: true, json: async () => ({ modelVersion: "v2", classProbabilities: {} }) });
  try {
    await assert.rejects(classifyComplaint({ caseId: "case", complaintText: "text" }), GovernanceMalformedResponseError);
  } finally { global.fetch = original; }
});

test("Incomplete or out-of-order anomaly traces are rejected before HTTP submission", () => {
  assert.throws(() => parseWorkflowAnomalyRequest({ caseId: "case", events: [] }), /at least one event/);
  assert.throws(() => parseWorkflowAnomalyRequest({ caseId: "case", events: [{ eventSeq: 2, activity: "A", institution: "I", resource: "R", timestamp: "2026-01-01T08:00:00" }] }), /eventSeq 1/);
  assert.throws(() => parseWorkflowAnomalyRequest({ caseId: "case", events: [{ eventSeq: 1, activity: "", institution: "I", resource: "R", timestamp: "2026-01-01T08:00:00" }] }), /nonblank activity/);
});

test("Workflow anomaly response exposes score, threshold, flag, advisory, and nine features", async () => {
  const original = global.fetch;
  const request = parseWorkflowAnomalyRequest({ caseId: "FICTIONAL-TRACE-001", events: [{ eventSeq: 1, activity: "Received", institution: "DS", resource: "Officer", timestamp: "2026-01-01T08:00:00" }] });
  const featureValues = { eventCount: 1, elapsedDays: 0, maxGapHours: 0, meanGapHours: 0, uniqueActivities: 1, repeatedActivityCount: 0, resourceHandoffs: 0, institutionSwitches: 0, distinctResources: 1 };
  const response = { caseId: request.caseId, modelVersion: "workflow_anomaly_model_v1", anomalyScore: 0.4, threshold: 0.5743688923115832, flagged: false, featureValues, advisoryNote: "Advisory statistical result." };
  global.fetch = async () => ({ ok: true, json: async () => response });
  try {
    assert.deepEqual(await predictWorkflowAnomaly(request), response);
    assert.equal(Object.keys(featureValues).length, 9);
  } finally { global.fetch = original; }
});

test("Early-screening status and blockchain receipt responses are validated", async () => {
  const original = global.fetch;
  const screening = { caseId: "FICTIONAL-C4-001", inputVersion: "v1", overallStatus: "ReviewRequired", hasIncompleteEvidence: false, indicatorResults: [{ indicatorType: "LegalDispute", evidenceState: "VerifiedPresent", requiresReview: true, needsEvidence: false, reasonCode: "LEGAL_DISPUTE", message: "Review required.", evidenceReference: "FICTIONAL-EVIDENCE", recordedAtUtc: "2026-10-11T00:00:00Z" }] };
  const receipt = { recordId: "0199b9bc-9898-7000-8000-000000000002", engineType: "Compliance", actionName: "Evaluate", governanceStatus: "ReviewRequired", decisionRecordedAt: "2026-10-11T00:00:00Z", recordHash: "abc", verificationStatus: "Match", anchorStatus: "Anchored", ledgerTransactionReference: "0x123", blockNumber: 1, anchoredAtUtc: "2026-10-11T00:00:01Z", verifiedAtUtc: "2026-10-11T00:00:02Z", explanation: "Digest matched.", recordVersion: "AUDIT-V2" };
  global.fetch = async url => ({ ok: true, json: async () => url.includes("screen-early-governance") ? screening : receipt });
  try {
    const screened = await screenEarlyGovernance({ assessmentId: crypto.randomUUID(), workflowRunId: crypto.randomUUID(), caseId: screening.caseId, inputVersion: "v1", indicators: [] });
    assert.equal(screened.overallStatus, "ReviewRequired");
    assert.equal((await getAuditReceipt(receipt.recordId)).verificationStatus, "Match");
    assert.equal((await verifyAuditIntegrity(receipt.recordId)).ledgerTransactionReference, "0x123");
  } finally { global.fetch = original; }
});

test("Early-screening input rejects unknown and duplicate indicators", () => {
  assert.throws(() => parseEarlyScreeningSeedInput({ caseId: "case", inputVersion: "v1", indicators: [{ indicatorType: "InventedIndicator", evidenceState: "Missing" }] }), /invalid type or evidence state/);
  assert.throws(() => parseEarlyScreeningSeedInput({ caseId: "case", inputVersion: "v1", indicators: [{ indicatorType: "LegalDispute", evidenceState: "Missing" }, { indicatorType: "LegalDispute", evidenceState: "VerifiedAbsent" }] }), /duplicated/);
});
