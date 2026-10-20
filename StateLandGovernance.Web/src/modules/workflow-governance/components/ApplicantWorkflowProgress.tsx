"use client";
import { useEffect, useState } from "react";
import { apiGet } from "@/lib/apiClient";
import { workflowDevelopmentHeaders, workflowAuthRole } from "../services/workflowDevelopmentHeaders";
type Progress = { status: string; isDevelopmentMock: boolean; detail: string;
  reviewedRegionalNonRecommendations?: { institution: string; reason?: string; detail: string }[];
  milestones: { code: string; status: string }[]; blockingRejections: { institution: string; reason?: string; reportedToCommissioner: boolean }[] };
export function ApplicantWorkflowProgress({ caseId }: { caseId: string }) {
  const [value, setValue] = useState<Progress>(); const [error, setError] = useState("");
  useEffect(() => { setValue(undefined); apiGet<Progress>(`/api/workflow-governance/cases/${caseId}/workflow-progress`,
    workflowDevelopmentHeaders("applicant"), workflowAuthRole("applicant")).then(setValue)
    .catch(e => setError(e instanceof Error ? e.message : "Progress unavailable")); }, [caseId]);
  return <section aria-label="Persisted institutional progress"><h2>Institutional progress</h2>{error && <p role="alert">{error}</p>}
    {value && <><p>{value.status} — {value.detail}</p>{value.isDevelopmentMock && <p>Development mock spatial inputs are in use.</p>}
      <ul>{value.milestones.map(m => <li key={m.code}>{m.code}: {m.status}</li>)}</ul>
      {value.reviewedRegionalNonRecommendations?.map(r => <p key={r.institution}>{r.institution}: {r.reason} {r.detail}</p>)}
      {value.blockingRejections.map(r => <p key={r.institution} role="status">{r.institution}: {r.reason || "Institution recorded a rejection; contact the reviewing officer."}
        {r.reportedToCommissioner && " This rejection is available in the Commissioner report. Required approvals remain blocked."}</p>)}
    </>}
  </section>;
}
