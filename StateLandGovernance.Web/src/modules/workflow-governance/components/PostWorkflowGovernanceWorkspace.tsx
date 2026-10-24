"use client";
import { useCallback, useEffect, useState } from "react";
import { apiGet, apiPost } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { workflowDevelopmentHeaders, workflowAuthRole } from "../services/workflowDevelopmentHeaders";
type FollowUp = { id: string; revision: number; assignedInstitution: string; instructions: string; status: string; dueDateUtc: string };
type State = { executionRevision?: number; canAssess: boolean; missingInputs: string[]; assessmentCurrent: boolean;
  assessment?: { assessmentId: string; flagged: boolean }; followUps: FollowUp[]; finalizedRecordId?: string; integrityStatus: string };
export function PostWorkflowGovernanceWorkspace({ caseId }: { caseId: string }) {
  const base = `/api/workflow-governance/cases/${caseId}/post-workflow`;
  const [state, setState] = useState<State>(); const [error, setError] = useState(""); const [busy, setBusy] = useState(false);
  const [reason, setReason] = useState(""); const [institution, setInstitution] = useState(""); const [due, setDue] = useState("");
  const refresh = useCallback(async () => { try { setState(await apiGet<State>(base, workflowDevelopmentHeaders("officer"), workflowAuthRole("officer"))); }
    catch (e) { setError(e instanceof Error ? e.message : "Post-workflow review unavailable"); } }, [base]);
  useEffect(() => { void refresh(); }, [refresh]);
  async function act(action: "assess" | "follow-ups" | "finalize") {
    if (!state) return; setBusy(true); setError("");
    try { await apiPost(`${base}/${action}`, action === "follow-ups" ? { assessmentId: state.assessment!.assessmentId, institution, dueAtUtc: new Date(due).toISOString(), reason }
      : { expectedExecutionRevision: state.executionRevision, reason }, workflowDevelopmentHeaders("officer"), workflowAuthRole("officer")); }
    catch (e) { setError(e instanceof Error ? e.message : "Post-workflow action rejected"); } finally { await refresh(); setBusy(false); }
  }
  return <section aria-label="Post-workflow governance"><h3>Post-workflow governance</h3>
    <p>C4 anomaly findings are advisory. They do not establish corruption or change the lease decision. A real DS notification is required by the current model; no terminal event is fabricated.</p>
    {error && <p role="alert">{error}</p>}<ul>{state?.missingInputs.map(m => <li key={m}>{m}</li>)}</ul>
    <label>Review reason <textarea value={reason} onChange={e => setReason(e.target.value)} /></label>
    <Button disabled={busy || !state?.canAssess || !!state.finalizedRecordId || !reason.trim()} onClick={() => void act("assess")}>Request C4 post-workflow assessment</Button>
    {state?.assessment && <p>Assessment {state.assessment.assessmentId}: {state.assessmentCurrent ? "Current" : "Historical / stale"}; {state.assessment.flagged ? "Human follow-up required" : "Advisory review required"}</p>}
    {state?.assessmentCurrent && !state.finalizedRecordId && <fieldset><legend>Assign an explicit human follow-up</legend>
      <label>Participating institution code <input value={institution} onChange={e => setInstitution(e.target.value)} /></label>
      <label>Explicit due date <input type="datetime-local" value={due} onChange={e => setDue(e.target.value)} /></label>
      <Button disabled={busy || !institution.trim() || !due || !reason.trim()} onClick={() => void act("follow-ups")}>Assign follow-up with verified authority</Button></fieldset>}
    <ul>{state?.followUps.map(f => <li key={f.id}>{f.assignedInstitution}: {f.instructions} — {f.status}; due {f.dueDateUtc}</li>)}</ul>
    <Button disabled={busy || !state?.assessmentCurrent || !!state.finalizedRecordId || !reason.trim() ||
      (state.assessment?.flagged && state.followUps.length === 0) || state.followUps.some(f => f.status !== "ResolvedAfterHumanReview")}
      onClick={() => void act("finalize")}>Finalize reviewed governance record</Button>
    {state?.finalizedRecordId && <p>Persisted finalized record {state.finalizedRecordId}. The institutional completion record is unchanged.</p>}
    <p>Integrity proof: {state?.integrityStatus ?? "Unavailable"}</p>
  </section>;
}
