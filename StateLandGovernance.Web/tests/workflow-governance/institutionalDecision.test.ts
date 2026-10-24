import test from "node:test";
import assert from "node:assert/strict";
import {
  isInstitutionalDecisionSubmittable,
  buildStageDecisionPayload,
} from "../../src/modules/workflow-governance/services/institutionalDecision";

test("1. Decision can be submitted without selecting a file (evidence is optional)", () => {
  // Reviewer has outcome, reason, and stage is InProgress. No evidence file attached.
  const canSubmit = isInstitutionalDecisionSubmittable({
    busy: false,
    outcome: "Approved",
    reason: "Statutory review conducted and approved based on proposal context.",
    conditions: "",
    stageStatus: "InProgress",
    hasOwnDecision: false,
  });

  assert.equal(canSubmit, true, "Decision submission must not be blocked when no evidence file is provided");
});

test("2. Decision outcome must still be explicitly selected", () => {
  const missingOutcome = isInstitutionalDecisionSubmittable({
    busy: false,
    outcome: "",
    reason: "Reason without chosen outcome",
    conditions: "",
    stageStatus: "InProgress",
    hasOwnDecision: false,
  });

  assert.equal(missingOutcome, false, "Submission must be blocked when outcome is not selected");
});

test("3. Optional file can still be selected and sent alongside valid decision", () => {
  const withoutEvidence = buildStageDecisionPayload({
    expectedExecutionRevision: 1,
    outcome: "Approved",
    reason: "Clearance granted",
    conditions: "",
  });
  assert.deepEqual(withoutEvidence.documents, [], "Empty array when no evidence file is attached");

  const withEvidence = buildStageDecisionPayload({
    expectedExecutionRevision: 1,
    outcome: "Approved",
    reason: "Clearance granted with formal gazette",
    conditions: "",
    evidence: {
      documentId: "doc-123",
      versionId: "ver-456",
    },
  });
  assert.deepEqual(
    withEvidence.documents,
    [{ documentId: "doc-123", versionId: "ver-456" }],
    "Optional evidence is retained and forwarded when supplied"
  );
});

test("4. Required reason and condition guards still operate", () => {
  // Empty reason blocks submission
  const emptyReason = isInstitutionalDecisionSubmittable({
    busy: false,
    outcome: "Approved",
    reason: "   ",
    conditions: "",
    stageStatus: "InProgress",
    hasOwnDecision: false,
  });
  assert.equal(emptyReason, false, "Submission must be blocked when reason is blank");

  // Rejection requires reason
  const rejectionWithoutReason = isInstitutionalDecisionSubmittable({
    busy: false,
    outcome: "Rejected",
    reason: "",
    conditions: "",
    stageStatus: "InProgress",
    hasOwnDecision: false,
  });
  assert.equal(rejectionWithoutReason, false, "Rejection must have a reason");

  // ApprovedWithConditions requires at least one condition
  const conditionsMissing = isInstitutionalDecisionSubmittable({
    busy: false,
    outcome: "ApprovedWithConditions",
    reason: "Approved provided terms are met",
    conditions: "   \n  ",
    stageStatus: "InProgress",
    hasOwnDecision: false,
  });
  assert.equal(conditionsMissing, false, "ApprovedWithConditions must provide at least one condition line");

  const conditionsProvided = isInstitutionalDecisionSubmittable({
    busy: false,
    outcome: "ApprovedWithConditions",
    reason: "Approved subject to environmental mitigation",
    conditions: "Mitigation report required within 30 days",
    stageStatus: "InProgress",
    hasOwnDecision: false,
  });
  assert.equal(conditionsProvided, true, "ApprovedWithConditions is submittable when conditions are supplied");

  // Decision revision requires revisionReason
  const revisionMissingReason = isInstitutionalDecisionSubmittable({
    busy: false,
    outcome: "Approved",
    reason: "Updated decision",
    conditions: "",
    stageStatus: "InProgress",
    hasOwnDecision: true,
    revisionReason: "",
  });
  assert.equal(revisionMissingReason, false, "Decision revision must provide a revision reason");

  // Inactive/not InProgress stage blocks submission
  const stageBlocked = isInstitutionalDecisionSubmittable({
    busy: false,
    outcome: "Approved",
    reason: "Valid reason",
    conditions: "",
    stageStatus: "Blocked",
    hasOwnDecision: false,
  });
  assert.equal(stageBlocked, false, "Stage not in progress must be blocked from submission");
});

test("5. Busy/submitting state prevents duplicate clicks", () => {
  const isBusy = isInstitutionalDecisionSubmittable({
    busy: true,
    outcome: "Approved",
    reason: "Valid reason",
    conditions: "",
    stageStatus: "InProgress",
    hasOwnDecision: false,
  });

  assert.equal(isBusy, false, "Submission must be disabled while a request is in flight");
});

