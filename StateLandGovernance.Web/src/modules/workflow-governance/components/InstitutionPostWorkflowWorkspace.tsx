"use client";
import { useCallback, useEffect, useState } from "react";
import { apiGet, apiPost } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { workflowDevelopmentHeaders, workflowAuthRole } from "../services/workflowDevelopmentHeaders";
type FollowUp = { id: string; revision: number; status: string; instructions: string; dueDateUtc: string };
type Context = { executionRevision: number; finalDecisionId: string; canRecord: boolean };
export function InstitutionPostWorkflowWorkspace({ caseId, institution, evidence }: { caseId: string; institution: string;
  evidence?: { documentId: string; versionId: string } }) {
  const base = `/api/workflow-governance/cases/${caseId}/post-workflow`;
  const [rows, setRows] = useState<FollowUp[]>([]); const [context, setContext] = useState<Context>();
  const [error, setError] = useState(""); const [busy, setBusy] = useState(false); const [reason, setReason] = useState("");
  const [reference, setReference] = useState(""); const [notifiedAt, setNotifiedAt] = useState("");
  const refresh = useCallback(async () => { try {
    setRows(await apiGet(`${base}/follow-ups/assigned`, workflowDevelopmentHeaders("officer"), workflowAuthRole("officer")));
    if (institution === "DS") setContext(await apiGet(`${base}/notification-context`, workflowDevelopmentHeaders("officer"), workflowAuthRole("officer")));
  } catch (e) { setError(e instanceof Error ? e.message : "Assigned follow-up unavailable"); } }, [base, institution]);
  useEffect(() => { void refresh(); }, [refresh]);
  async function act(row?: FollowUp) {
    setBusy(true); setError("");
    try { await apiPost(row ? `${base}/follow-ups/${row.id}/resolve` : `${base}/ds-notification`, row ?
      { expectedRevision: row.revision, reason, evidenceReference: reference } : { expectedExecutionRevision: context!.executionRevision,
        finalDecisionId: context!.finalDecisionId, notifiedAtUtc: new Date(notifiedAt).toISOString(), ...evidence, reason },
      workflowDevelopmentHeaders("officer"), workflowAuthRole("officer")); }
    catch (e) { setError(e instanceof Error ? e.message : "Institutional post-workflow action rejected"); } finally { await refresh(); setBusy(false); }
  }
  return <section aria-label="Assigned governance follow-up"><h3>Your institution&apos;s post-workflow actions</h3>
    <p>Only assigned questions are shown. Internal anomaly scores and other institutions&apos; evidence remain restricted.</p>
    {error && <p role="alert">{error}</p>}
    <label>Review reason <textarea value={reason} onChange={e => setReason(e.target.value)} /></label>
    <label>Actual source evidence reference <input value={reference} onChange={e => setReference(e.target.value)} /></label>
    {rows.map(r => <article key={r.id}><p>{r.instructions} — {r.status}; due {r.dueDateUtc}</p>
      <Button disabled={busy || r.status !== "Assigned" || !reason.trim() || !reference.trim()} onClick={() => void act(r)}>Record authorized human resolution</Button></article>)}
    {institution === "DS" && <fieldset><legend>Record evidence of an actual DS final notification</legend>
      <p>Upload its evidence using this request&apos;s document upload first. Recording a receipt does not send a notification.</p>
      <label>Actual notification time <input type="datetime-local" value={notifiedAt} onChange={e => setNotifiedAt(e.target.value)} /></label>
      <Button disabled={busy || !context?.canRecord || !evidence || !notifiedAt || !reason.trim()} onClick={() => void act()}>Record reviewed notification evidence</Button></fieldset>}
  </section>;
}
