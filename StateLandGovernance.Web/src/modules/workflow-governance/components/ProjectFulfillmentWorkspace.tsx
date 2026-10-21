"use client";
import { useCallback, useEffect, useState } from "react";
import { apiDownloadBlob, apiGet, apiPost, apiPostFormData } from "@/lib/apiClient";
import { PostWorkflowGovernanceWorkspace } from "./PostWorkflowGovernanceWorkspace";
import { GovernedHandoffWorkspace } from "./GovernedHandoffWorkspace";
import { Button } from "@/shared/components/Button";
import { workflowDevelopmentHeaders, workflowAuthRole } from "../services/workflowDevelopmentHeaders";
type Obligation = { code: string; institution: string; title: string; titlePending: boolean; applicability?: string; forwardRoute?: string[] };
type Requirement = { id: string; revision: number; code: string; institution: string; title: string; status: string; dueAtUtc: string;
  titlePending: boolean; sourceReference: string; submissions?: { documentId: string; versionId: string; submittedAtUtc: string }[]; forwarding?: { institution: string; at: string }[] };
type State = { caseRevision: number; caseStatus: string; requirements: Requirement[]; unconfigured: Obligation[];
  missingInputs?: string[]; readyForFinalRecord?: boolean; readyForHandoff?: boolean; conditions?: { id: string; institution: string; description: string; status: string }[] };
export function ProjectFulfillmentWorkspace({ caseId, assignedOnly = false, institutionAccess = false, institutionCode, institutionDecisionId, hidePostWorkflow = false }: { caseId: string; hidePostWorkflow?: boolean; assignedOnly?: boolean; institutionAccess?: boolean; institutionCode?: string; institutionDecisionId?: string }) {
  const base = `/api/workflow-governance/cases/${caseId}/fulfillment`; const auth = assignedOnly && !institutionAccess ? "applicant" : "officer";
  const [state, setState] = useState<State>(); const [error, setError] = useState(""); const [busy, setBusy] = useState(false);
  const [code, setCode] = useState(""); const [due, setDue] = useState(""); const [version, setVersion] = useState("");
  const [title, setTitle] = useState(""); const [source, setSource] = useState(""); const [reason, setReason] = useState("");
  const [evidence, setEvidence] = useState("");
  const [recommendLease, setRecommendLease] = useState("");
  const [files, setFiles] = useState<Record<string, File>>({});
  const refresh = useCallback(async () => { try { setState(await apiGet<State>(base + (assignedOnly ? "/assigned" : ""),
    workflowDevelopmentHeaders(auth), workflowAuthRole(auth))); } catch (e) { setError(e instanceof Error ? e.message : "Fulfillment unavailable"); } }, [base, assignedOnly, auth]);
  useEffect(() => { void refresh(); }, [refresh]);
  async function act(kind: "configure" | "accept" | "changes" | "submit" | "final" | "waive", requirement?: Requirement) {
    if (!state) return; setBusy(true); setError("");
    try {
      if (kind === "submit" && requirement) { const data = new FormData(); data.set("file", files[requirement.id]); data.set("expectedRevision", String(requirement.revision));
        await apiPostFormData(`${base}/${requirement.id}/submit`, data, workflowDevelopmentHeaders(auth), workflowAuthRole(auth)); }
      else {
        const path = kind === "configure" ? `${base}/configure` : kind === "final" ? `${base}/final-record` : `${base}/${requirement!.id}/${kind === "waive" ? "waive" : "review"}`;
        const body = kind === "configure" ? { expectedCaseRevision: state.caseRevision, version, requirements: [{ code,
          dueAtUtc: new Date(due).toISOString(), actualTitle: title || null, actualTitleSourceReference: source || null }] }
          : kind === "final" ? { expectedCaseRevision: state.caseRevision }
            : kind === "waive" ? { expectedCaseRevision: state.caseRevision, expectedRequirementRevision: requirement!.revision, reason, evidenceReference: evidence }
            : { expectedRevision: requirement!.revision, accept: kind === "accept", reason };
        await apiPost(path, body, workflowDevelopmentHeaders(auth), workflowAuthRole(auth));
      }
    } catch (e) { setError(e instanceof Error ? e.message : "Fulfillment action rejected"); } finally { await refresh(); setBusy(false); }
  }
  async function reportAction(requirement: Requirement, action: "lease-recommendation" | "forward-receipt") {
    setBusy(true); setError("");
    try { await apiPost(`${base}/${requirement.id}/${action}`, action === "lease-recommendation" ? { expectedRevision: requirement.revision,
      dsDecisionId: institutionDecisionId, recommendLease: recommendLease === "yes", reason } : { expectedRevision: requirement.revision, reason }, workflowDevelopmentHeaders(auth), workflowAuthRole(auth)); }
    catch (e) { setError(e instanceof Error ? e.message : "Report action rejected"); } finally { await refresh(); setBusy(false); }
  }
  async function reviewCondition(id: string, action: "fulfill" | "waive") {
    if (!state) return; setBusy(true); setError("");
    try { await apiPost(`${base}/conditions/${id}/${action}`, { expectedCaseRevision: state.caseRevision, reason, evidenceReference: evidence }, workflowDevelopmentHeaders(auth), workflowAuthRole(auth)); }
    catch (e) { setError(e instanceof Error ? e.message : "Condition review rejected"); } finally { await refresh(); setBusy(false); }
  }
  async function download(requirement: Requirement, documentId: string, versionId: string) {
    try { const blob = await apiDownloadBlob(`${base}/${requirement.id}/documents/${documentId}/versions/${versionId}/download`, workflowAuthRole(auth), workflowDevelopmentHeaders(auth));
      const url = URL.createObjectURL(blob); const link = document.createElement("a"); link.href = url; link.download = `${requirement.code}-${versionId}`; link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (e) { setError(e instanceof Error ? e.message : "Evidence access denied"); }
  }
  return <section aria-label="Role-specific document fulfillment"><h2>Role-specific document fulfillment</h2>
    <p>Parallel document obligations come from the supplied project workflow. Uploads await explicit review. Unresolved titles and applicability remain pending.</p>
    {error && <p role="alert">{error}</p>}{state && <>
      <ul>{state.missingInputs?.map(m => <li key={m}>{m}</li>)}</ul>
      {state.unconfigured.length > 0 && <><h3>Configuration still needed</h3><ul>{state.unconfigured.map(o => <li key={o.code}>{o.institution}: {o.title}
        {o.applicability && ` — ${o.applicability}`}{o.forwardRoute && ` — route ${o.forwardRoute.join(" → ")}`}</li>)}</ul></>}
      {!assignedOnly && state.unconfigured.length > 0 && <fieldset><legend>Issue an explicit versioned obligation</legend>
        <label>Project obligation <select value={code} onChange={e => setCode(e.target.value)}><option value="">Select</option>{state.unconfigured.map(o => <option key={o.code} value={o.code}>{o.institution}: {o.title}</option>)}</select></label>
        <label>Configuration version <input value={version} onChange={e => setVersion(e.target.value)} /></label>
        <label>Explicit due date <input type="datetime-local" value={due} onChange={e => setDue(e.target.value)} /></label>
        <label>Exact title, if supplied by owner <input value={title} onChange={e => setTitle(e.target.value)} /></label>
        <label>Exact title source reference <input value={source} onChange={e => setSource(e.target.value)} /></label>
        <Button disabled={busy || !code || !version.trim() || !due} onClick={() => void act("configure")}>Issue obligation with verified authority</Button>
      </fieldset>}
      {(!assignedOnly || institutionAccess) && <><label>Review reason <textarea value={reason} onChange={e => setReason(e.target.value)} /></label><label>Source evidence for governed waiver <input value={evidence} onChange={e => setEvidence(e.target.value)} /></label></>}
      {state.requirements.map(r => <article key={r.id}><h3>{r.institution}: {r.title}</h3><p>{r.status} · revision {r.revision} · due {r.dueAtUtc}</p>
        {r.titlePending && <p>Owner-sourced exact title is pending; a filename does not resolve it.</p>}
        <p>Project source: {r.sourceReference}</p>{r.submissions?.map(s => <Button key={s.versionId} onClick={() => void download(r, s.documentId, s.versionId)}>Read submitted version {s.versionId.slice(0, 8)}</Button>)}
        {assignedOnly && (!institutionAccess || r.institution === institutionCode) && (r.status === "Pending" || r.status === "ChangesRequested") && <><input aria-label={`Upload ${r.title}`} type="file"
          onChange={e => { if (e.target.files?.[0]) setFiles(p => ({ ...p, [r.id]: e.target.files![0] })); }} />
          <Button disabled={busy || !files[r.id]} onClick={() => void act("submit", r)}>Submit for review</Button></>}
        {!assignedOnly && r.status === "SubmittedAwaitingReview" && <><Button disabled={busy || !reason.trim() || r.titlePending} onClick={() => void act("accept", r)}>Accept reviewed exact version</Button>
          <Button disabled={busy || !reason.trim()} onClick={() => void act("changes", r)}>Request changes</Button></>}
        {(!assignedOnly || institutionAccess) && (r.status === "Pending" || r.status === "ChangesRequested") && <Button disabled={busy || !reason.trim() || !evidence.trim() || r.titlePending} onClick={() => void act("waive", r)}>Record authorized source-backed waiver</Button>}
        {r.code === "DS_RELEVANT_LAND_REPORT" && institutionCode === "DS" && <fieldset><legend>Explicit lease recommendation applicability</legend>
          <select aria-label="Lease recommendation" value={recommendLease} onChange={e => setRecommendLease(e.target.value)}><option value="">Undetermined</option><option value="yes">DS recommends lease</option><option value="no">DS does not recommend lease</option></select>
          <Button disabled={busy || !institutionDecisionId || !recommendLease || !reason.trim()} onClick={() => void reportAction(r, "lease-recommendation")}>Record recommendation against current DS decision</Button></fieldset>}
        {r.code === "DS_RELEVANT_LAND_REPORT" && (institutionCode === "PROVINCIAL_LC" || institutionCode === "COMMISSIONER") && <Button disabled={busy || !reason.trim()} onClick={() => void reportAction(r, "forward-receipt")}>Record reviewed exact-version forwarding receipt</Button>}
        {r.forwarding?.map(f => <p key={f.institution}>{f.institution} receipt: {f.at}</p>)}
      </article>)}
      {state.conditions?.map(c => <article key={c.id}><p>{c.institution}: {c.description} — {c.status}</p>
        {(c.status === "Pending" || c.status === "Overdue") && <><Button disabled={busy || !reason.trim() || !evidence.trim()} onClick={() => void reviewCondition(c.id, "fulfill")}>Record authorized condition fulfillment</Button>
        <Button disabled={busy || !reason.trim() || !evidence.trim()} onClick={() => void reviewCondition(c.id, "waive")}>Record governed condition waiver</Button></>}</article>)}
      {!assignedOnly && <><Button disabled={busy || !state.readyForFinalRecord} onClick={() => void act("final")}>Record immutable completed workflow decision</Button>
        <p>Handoff: {state.readyForHandoff ? "Fulfillment gates met; downstream contract and acknowledgement remain required." : "Blocked pending final decision, reviewed evidence, conditions and configuration."}</p>
        <GovernedHandoffWorkspace caseId={caseId} />{!hidePostWorkflow && <PostWorkflowGovernanceWorkspace caseId={caseId} />}</>}
    </>}
  </section>;
}
