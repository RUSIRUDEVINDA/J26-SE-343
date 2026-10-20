"use client";
import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { InstitutionPostWorkflowWorkspace } from "./InstitutionPostWorkflowWorkspace";
import { ProjectFulfillmentWorkspace } from "./ProjectFulfillmentWorkspace";
import { apiDownloadBlob, apiGet, apiPost, apiPostFormData } from "@/lib/apiClient";
import { workflowAuthRole, workflowDevelopmentHeaders } from "../services/workflowDevelopmentHeaders";
import { Button } from "@/shared/components/Button";

type Decision = { id: string; outcome: string; reason?: string; conditions: string[]; actorId: string; at: string };
type History = { previous: Decision; replacement: Decision; reason: string; revision: number };
type Stage = { id: string; code: string; institution: string; status: string; request: string; decision?: Decision; decisionHistory: History[] };
type State = { caseId: string; executionId?: string; planId?: string; revision?: number; status: string; current: boolean;
  missingInputs: string[]; stages: Stage[]; planHistory: { previousPlanId: string; replacementPlanId: string; reason: string; at: string }[];
  isDevelopmentMock: boolean; caseRevision: number; activePlanId?: string; activePlanRevision?: number; activePlanStatus?: string };
type Restricted = { caseId: string; executionId: string; revision: number; stageId: string; institution: string; status: string;
  request: string; ownDecision?: Decision; ownDecisionHistory: History[]; caseRevision: number; canStart: boolean };
const base = (id: string) => `/api/workflow-governance/cases/${encodeURIComponent(id)}`;
const init = () => workflowDevelopmentHeaders("officer");
const role = () => workflowAuthRole("officer");
function DecisionView({ decision, history }: { decision?: Decision; history: History[] }) {
  return <>{decision && <p><strong>{decision.outcome}</strong> — {decision.reason}<br />Recorded by {decision.actorId} at {decision.at}
    {decision.conditions.length > 0 && <span><br />Conditions: {decision.conditions.join("; ")}</span>}</p>}
    {history.map(h => <details key={h.revision}><summary>Decision revision {h.revision}: {h.reason}</summary>
      <p>{h.previous.outcome} ({h.previous.at}) → {h.replacement.outcome} ({h.replacement.at})</p></details>)}</>;
}
export function WorkflowExecutionWorkspace({ caseId }: { caseId: string }) {
  const [state, setState] = useState<State>(); const [error, setError] = useState(""); const [busy, setBusy] = useState(false);
  const refresh = useCallback(async () => { try { setState(await apiGet<State>(`${base(caseId)}/workflow-execution`, init(), role())); }
    catch (e) { setError(e instanceof Error ? e.message : "Execution unavailable"); } }, [caseId]);
  useEffect(() => { void refresh(); }, [refresh]);
  async function act(revision = false) {
    if (!state) return; setBusy(true); setError("");
    try { await apiPost(revision ? `${base(caseId)}/workflow-execution/plans/${state.activePlanId}/approve` : `${base(caseId)}/workflow-execution/start`,
      revision ? { expectedCaseRevision: state.caseRevision, expectedPlanRevision: state.activePlanRevision, expectedExecutionRevision: state.revision }
        : { planId: state.activePlanId, expectedPlanRevision: state.activePlanRevision, expectedCaseRevision: state.caseRevision }, init(), role()); }
    catch (e) { setError(e instanceof Error ? e.message : "Action rejected"); }
    finally { await refresh(); setBusy(false); }
  }
  return <section aria-label="Institutional execution"><h2>Institutional execution</h2>
    <p>Activation is separate from plan approval. Institutional decisions require authenticated case-scoped capabilities.</p>
    {error && <p role="alert">{error}</p>}{!state && !error && <p>Loading execution readiness…</p>}
    {state && <><p role="status">{state.current ? state.status : "Execution blocked / stale"}</p>
      {state.isDevelopmentMock && <p role="alert">Development mock spatial inputs — no real parcel clearance.</p>}
      <ul>{state.missingInputs.map(m => <li key={m}>{m}</li>)}</ul>
      <Button disabled={busy || !state.current || !!state.executionId} onClick={() => void act()}>Activate displayed approved plan</Button>
      {state.executionId && state.activePlanStatus === "Draft" && <Button disabled={busy} onClick={() => void act(true)}>Approve and adopt displayed governed revision</Button>}
      {!state.executionId && <p>No persisted institutional execution.</p>}
      {state.stages.map(s => <article key={s.id}><h3>{s.code} · {s.institution} · {s.status}</h3><p>{s.request}</p>
        <DecisionView decision={s.decision} history={s.decisionHistory} />
        <Link href={`/workflow-governance/institutional/${caseId}/${s.id}`}>Open restricted institutional request</Link>
        {s.decision?.outcome === "Rejected" && <p role="status">{s.code === "PROVINCIAL_LC" || s.code === "INTER_PROVINCIAL_LC"
          ? "Regional non-recommendation retained for the Commissioner's independent decision. See the persisted Commissioner outcome below."
          : "Rejection retained. Required dependent approvals remain blocked; Commissioner reporting and applicant progress use this persisted decision."}</p>}
      </article>)}
      {state.planHistory.map(h => <p key={h.replacementPlanId}>Plan {h.previousPlanId} → {h.replacementPlanId}: {h.reason} ({h.at})</p>)}
    </>}
  </section>;
}
export function InstitutionalRequestWorkspace({ caseId, stageId }: { caseId: string; stageId: string }) {
  const path = `${base(caseId)}/institutional-requests/${encodeURIComponent(stageId)}`;
  const [documents, setDocuments] = useState<{ documentId: string; versions: { versionId: string; originalFileName: string }[] }[]>([]);
  const [state, setState] = useState<Restricted>(); const [error, setError] = useState(""); const [busy, setBusy] = useState(false);
  const [outcome, setOutcome] = useState("Approved"); const [reason, setReason] = useState("");
  const [conditions, setConditions] = useState(""); const [revisionReason, setRevisionReason] = useState("");
  const [file, setFile] = useState<File>(); const [evidence, setEvidence] = useState<{ documentId: string; versionId: string }>();
  const [department, setDepartment] = useState(""); const [departmentReason, setDepartmentReason] = useState("");
  const refresh = useCallback(async () => { try { setState(await apiGet<Restricted>(path, init(), role())); setDocuments(await apiGet(`${path}/documents`, init(), role())); }
    catch (e) { setError(e instanceof Error ? e.message : "Authenticated institutional access required"); } }, [path]);
  useEffect(() => { void refresh(); }, [refresh]);
  async function submit(action: "start" | "decision" | "upload" | "department") {
    if (!state) return; setBusy(true); setError("");
    try {
      if (action === "upload" && file) { const data = new FormData(); data.set("file", file);
        setEvidence(await apiPostFormData(`${path}/documents`, data, init(), role())); }
      else if (action === "department") await apiPost(`${base(caseId)}/workflow-execution/departments`, {
        expectedExecutionRevision: state.revision, expectedCaseRevision: state.caseRevision, institution: department, reason: departmentReason }, init(), role());
      else await apiPost(`${path}/${action}`, action === "start" ? { expectedExecutionRevision: state.revision } : {
        expectedExecutionRevision: state.revision, outcome, reason, conditions: conditions.split("\n").map(x => x.trim()).filter(Boolean),
        documents: evidence ? [evidence] : [], priorDecisionId: state.ownDecision?.id, revisionReason: revisionReason || null }, init(), role());
    } catch (e) { setError(e instanceof Error ? e.message : "Institutional action rejected"); }
    finally { await refresh(); setBusy(false); }
  }
  async function download(documentId: string, versionId: string, fileName: string) {
    try { const blob = await apiDownloadBlob(`${path}/documents/${documentId}/versions/${versionId}/download`, role(), init());
      const url = URL.createObjectURL(blob); const link = document.createElement("a"); link.href = url; link.download = fileName; link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (e) { setError(e instanceof Error ? e.message : "Scoped evidence access denied"); }
  }
  return <section aria-label="Restricted institutional request"><h1>Restricted institutional request</h1>
    <p>This view contains the institution&apos;s request and its own decision history. Internal C4 findings and unrelated case documents are withheld by the API.</p>
    {error && <p role="alert">{error}</p>}{state && <><h2>{state.institution} · {state.status}</h2><p>{state.request}</p>
      <DecisionView decision={state.ownDecision} history={state.ownDecisionHistory} />
      <h3>Evidence explicitly assigned to this request</h3>
      {documents.length === 0 && <p>No scoped evidence files have been supplied.</p>}
      {documents.flatMap(d => d.versions.map(v => <Button key={v.versionId} onClick={() => void download(d.documentId, v.versionId, v.originalFileName)}>Read {v.originalFileName} ({v.versionId.slice(0, 8)})</Button>))}
      <Button disabled={busy || !state.canStart} onClick={() => void submit("start")}>Start authorized review</Button>
      <label>Decision document <input type="file" onChange={e => setFile(e.target.files?.[0])} /></label>
      <Button disabled={busy || !file} onClick={() => void submit("upload")}>Upload decision evidence</Button>
      {evidence && <p>Submitted document {evidence.documentId}, version {evidence.versionId}. Upload is not approval.</p>}
      <label>Decision <select value={outcome} onChange={e => setOutcome(e.target.value)}><option>Approved</option><option>ApprovedWithConditions</option><option>Rejected</option></select></label>
      <label>Reasons and suggestions <textarea maxLength={250} value={reason} onChange={e => setReason(e.target.value)} /></label>
      <label>Conditions, one per line <textarea value={conditions} onChange={e => setConditions(e.target.value)} /></label>
      {state.ownDecision && <label>Reason for authorized revision <textarea value={revisionReason} onChange={e => setRevisionReason(e.target.value)} /></label>}
      <Button disabled={busy || !evidence || !reason.trim() || (state.ownDecision ? !revisionReason.trim() : state.status !== "InProgress")}
        onClick={() => void submit("decision")}>{state.ownDecision ? "Submit authorized decision revision" : "Record explicit decision"}</Button>
      <ProjectFulfillmentWorkspace caseId={caseId} assignedOnly institutionAccess institutionCode={state.institution} institutionDecisionId={state.ownDecision?.id} /><InstitutionPostWorkflowWorkspace caseId={caseId} institution={state.institution} evidence={evidence} />
      {state.institution === "DS" && <fieldset><legend>Governed department addition</legend>
        <p>Select a code from the configured department pool. No selection criterion is inferred. The replacement preview requires separate approval.</p>
        <label>Department code <input value={department} onChange={e => setDepartment(e.target.value)} /></label>
        <label>Reason <textarea value={departmentReason} onChange={e => setDepartmentReason(e.target.value)} /></label>
        <Button disabled={busy || !department.trim() || !departmentReason.trim()} onClick={() => void submit("department")}>Request governed plan revision</Button>
      </fieldset>}
    </>}
  </section>;
}
