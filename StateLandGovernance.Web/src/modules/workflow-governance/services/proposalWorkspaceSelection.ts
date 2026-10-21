import type { CaseDetailResponse, CaseDocumentSummaryDto } from "../types/caseManagement";

/** All review actions use the same explicit binding as the displayed inventory. Never fall back to a Deed. */
export function selectedProposalDocument(detail: CaseDetailResponse): CaseDocumentSummaryDto | undefined {
  const selection = detail.proposalSelection;
  if (!selection?.documentId) return undefined;
  return detail.documents.find(d => d.documentId === selection.documentId && d.logicalCategory.toLowerCase() === "proposal"
    && d.activeVersionId === selection.versionId && d.documentAnalysisId === selection.analysisId
    && d.latestRunResultId === selection.runResultId);
}
