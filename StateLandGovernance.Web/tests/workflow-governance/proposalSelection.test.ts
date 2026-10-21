import assert from "node:assert/strict";
import test from "node:test";
import { selectedProposalDocument } from "../../src/modules/workflow-governance/services/proposalWorkspaceSelection";
import type { CaseDetailResponse } from "../../src/modules/workflow-governance/types/caseManagement";

function fixture(): CaseDetailResponse {
  return { documents: [
    { documentId: "deed", logicalCategory: "Deed", activeVersionId: "dv", documentAnalysisId: "da", latestRunResultId: "dr" },
    { documentId: "proposal", logicalCategory: "Proposal", activeVersionId: "pv", documentAnalysisId: "pa", latestRunResultId: "pr" },
  ], proposalSelection: { documentId: "proposal", versionId: "pv", analysisId: "pa", runResultId: "pr", status: "Completed" } } as CaseDetailResponse;
}
test("Proposal review targets the selected exact version/result despite an earlier Deed analysis", () => {
  assert.equal(selectedProposalDocument(fixture())?.documentAnalysisId, "pa");
});
test("An ambiguous or absent selection never falls back to the first analysis", () => {
  const state = fixture(); state.proposalSelection = null;
  assert.equal(selectedProposalDocument(state), undefined);
});
test("A mismatched version or result cannot be submitted as the displayed Proposal", () => {
  const state = fixture(); state.proposalSelection!.versionId = "old";
  assert.equal(selectedProposalDocument(state), undefined);
  state.proposalSelection!.versionId = "pv"; state.proposalSelection!.runResultId = "old-result";
  assert.equal(selectedProposalDocument(state), undefined);
});
test("A Deed cannot be made the Proposal selection by changing its identifier alone", () => {
  const state = fixture(); state.proposalSelection = { documentId: "deed", versionId: "dv", analysisId: "da", runResultId: "dr", status: "Completed", linkedSnapshotMatchesSelection: false };
  assert.equal(selectedProposalDocument(state), undefined);
});
