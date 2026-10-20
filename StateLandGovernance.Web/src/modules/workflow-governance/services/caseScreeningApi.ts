import { apiGet, apiPost } from "@/lib/apiClient";
import type { CaseScreeningState } from "../types/caseScreening";
import { workflowDevelopmentHeaders, workflowAuthRole } from "./workflowDevelopmentHeaders";

const path = (caseId: string) => `/api/workflow-governance/cases/${encodeURIComponent(caseId)}/screening`;
export function getCaseScreening(caseId: string, snapshotId?: string, parcelId?: string): Promise<CaseScreeningState> {
  const query = new URLSearchParams();
  if (snapshotId) query.set("snapshotId", snapshotId);
  if (parcelId) query.set("parcelId", parcelId);
  return apiGet(`${path(caseId)}?${query}`, workflowDevelopmentHeaders("officer"), workflowAuthRole("officer"));
}
export function runCaseScreening(state: CaseScreeningState): Promise<CaseScreeningState> {
  return apiPost(path(state.caseId), { verifiedFactSnapshotId: state.verifiedFactSnapshotId,
    expectedCaseRevision: state.caseRevision, parcelId: state.parcelId }, workflowDevelopmentHeaders("officer"), workflowAuthRole("officer"));
}
