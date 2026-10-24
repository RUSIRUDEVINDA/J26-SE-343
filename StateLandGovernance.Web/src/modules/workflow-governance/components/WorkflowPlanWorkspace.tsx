"use client";

import { useEffect, useState } from "react";
import { apiGet, apiPost } from "@/lib/apiClient";
import { workflowAuthRole, workflowDevelopmentHeaders } from "../services/workflowDevelopmentHeaders";
import { Button } from "@/shared/components/Button";

type Plan = { id: string; revision: number; status: string; snapshotId: string; ruleIdentifier: string; ruleVersion: string;
  binding: string; generatedBy: string; approvedBy?: string; createdAt: string; approvedAt?: string; revisionReason?: string;
  requiredEvidence: string[]; stages: { id: string; code: string; institution: string; capability: string; type: string;
    dependencies: string[]; targetDays?: number; explanation: string }[] };
type State = { caseId: string; caseRevision: number; snapshotId?: string; ready: boolean; missingInputs: string[];
  binding?: string; activePlan?: Plan; history: Plan[]; canApprove: boolean };
const path = (id: string) => `/api/workflow-governance/cases/${encodeURIComponent(id)}/workflow-plan`;
export function WorkflowPlanWorkspace({ caseId }: { caseId: string }) {
  const [state, setState] = useState<State | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [reason, setReason] = useState("");
  async function refresh() {
    try { const next = await apiGet<State>(path(caseId), workflowDevelopmentHeaders("officer"), workflowAuthRole("officer")); setState(next); setError(""); return next; }
    catch (e) { setState(null); setError(e instanceof Error ? e.message : "Readiness could not be checked."); return null; }
  }
  useEffect(() => { void refresh(); const focus = () => void refresh(); window.addEventListener("focus", focus); return () => window.removeEventListener("focus", focus); }, [caseId]); // eslint-disable-line react-hooks/exhaustive-deps
  async function act(approve: boolean) {
    if (!state) return;
    setBusy(true); setError("");
    try {
      // Send the displayed revisions. The server rejects changes since preview, rather than approving a newer plan.
      const next = await apiPost<State>(approve ? `${path(caseId)}/${state.activePlan!.id}/approve` : `${path(caseId)}/generate`,
        approve ? { verifiedFactSnapshotId: state.snapshotId, expectedCaseRevision: state.caseRevision, expectedPlanRevision: state.activePlan!.revision }
          : { verifiedFactSnapshotId: state.snapshotId, expectedCaseRevision: state.caseRevision,
            expectedPlanId: state.activePlan?.id, expectedPlanRevision: state.activePlan?.revision, revisionReason: reason || null },
        workflowDevelopmentHeaders("officer"), workflowAuthRole("officer"));
      setState(next); setReason("");
    } catch (e) { await refresh(); setError(e instanceof Error ? e.message : "The plan action was rejected."); }
    finally { setBusy(false); }
  }
  const canGenerate = state?.snapshotId && state.missingInputs.every(m => m.startsWith("The active workflow plan is stale"));
  return <section aria-label="Workflow Plan">
    <h2>Workflow Plan</h2>
    <p>Preview is a blueprint. Officer plan approval does not activate tasks, approve the application or complete institutional review.</p>
    <Button onClick={() => void refresh()} disabled={busy}>Refresh readiness</Button>
    {error && <p role="alert">{error}</p>}
    {!state && !error && <p>Loading planning readiness…</p>}
    {state && <>
      <p role="status"><strong>{state.ready ? "Ready for planning" : "Planning blocked / stale"}</strong></p>
      <p>Case <code>{state.caseId}</code> · revision {state.caseRevision}<br />Verified snapshot: {state.snapshotId || "Not published / linked"}</p>
      <ul>{state.missingInputs.map(m => <li key={m}>{m}</li>)}</ul>
      {state.activePlan && <label>Reason for governed revision <input value={reason} maxLength={500} onChange={e => setReason(e.target.value)} /></label>}
      <Button disabled={busy || !canGenerate || (!!state.activePlan && !reason.trim())} onClick={() => void act(false)}>
        {state.activePlan ? "Generate governed revision" : "Generate preview"}</Button>
      <Button disabled={busy || !state.canApprove} onClick={() => void act(true)}>Approve displayed plan</Button>
      {!state.canApprove && state.activePlan?.status === "Draft" && <p>Approval requires current inputs and verified case-scoped WorkflowPlanApprover authority.</p>}
      {state.history.length === 0 ? <p>No persisted workflow plans.</p> : state.history.map(plan => <article key={plan.id}>
        <h3>{plan.id === state.activePlan?.id ? "Active plan" : "Prior plan"}: {plan.status} · revision {plan.revision}</h3>
        <p>Plan <code>{plan.id}</code><br />Routing {plan.ruleIdentifier} / {plan.ruleVersion}<br />
          Snapshot <code>{plan.snapshotId}</code><br />Input binding <code>{plan.binding}</code></p>
        {plan.binding !== state.binding && <p role="status">Historical / stale — not current for the displayed inputs.</p>}
        {plan.revisionReason && <p>Revision reason: {plan.revisionReason}</p>}
        {plan.approvedAt && <p>Plan approved {plan.approvedAt}; institutional execution remains separate.</p>}
        <h4>Required evidence</h4><ul>{plan.requiredEvidence.map(e => <li key={e}>{e}</li>)}</ul>
        <table><thead><tr><th>Stage / task</th><th>Institution / responsible capability</th><th>Dependencies / parallel branches</th><th>Routing explanation</th></tr></thead>
          <tbody>{plan.stages.map(s => <tr key={s.id}><td>{s.code}<br />{s.type}{s.targetDays ? ` · target ${s.targetDays} days` : ""}</td>
            <td>{s.institution}<br />{s.capability}</td><td>{s.dependencies.length ? s.dependencies.map(d => plan.stages.find(x => x.id === d)?.code || d).join(", ") : "Entry branch — can run in parallel with other entry branches after activation"}</td><td>{s.explanation}</td></tr>)}</tbody></table>
      </article>)}
    </>}
  </section>;
}
