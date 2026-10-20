"use client";
import { useCallback, useEffect, useState } from "react";
import { apiGet, apiPost } from "@/lib/apiClient";
import { ProposalEvidenceViewer } from "./ProposalEvidenceViewer";
import { Button } from "@/shared/components/Button";
import { workflowDevelopmentHeaders, workflowAuthRole } from "../services/workflowDevelopmentHeaders";
type Source = { documentVersionId: string; analysisRevision: number; runId: string; runState: string;
  latestSnapshot?: { id: string; entries: { factCode: string; sourceKind: string; humanEvidence?: { pageNumber: number; quote: string } }[] } };
type State = { source: Source; canPublish: boolean; authorityRequired: string };
export function HumanEvidenceWorkspace({ caseId, documentId, caseRevision, linkedSnapshotId, fields, onLinked }: {
  caseId: string; documentId: string; caseRevision: number; linkedSnapshotId?: string;
  fields: { factCode: string; displayName: string; status: string }[]; onLinked: () => void;
}) {
  const base = `/api/workflow-governance/cases/${caseId}/proposal/human-evidence`;
  const [state, setState] = useState<State>(); const [error, setError] = useState(""); const [busy, setBusy] = useState(false);
  const [success,setSuccess]=useState("");const [transcript,setTranscript]=useState("");const [publishConsent,setPublishConsent]=useState(false);
  const [code, setCode] = useState(""); const [kind, setKind] = useState("Text"); const [value, setValue] = useState("");
  const [page, setPage] = useState(""); const [quote, setQuote] = useState(""); const [reason, setReason] = useState("");
  const refresh = useCallback(async () => { try { setState(await apiGet<State>(`${base}?documentId=${documentId}`,
    workflowDevelopmentHeaders("officer"), workflowAuthRole("officer"))); } catch (e) { setError(e instanceof Error ? e.message : "Human evidence unavailable"); } }, [base, documentId]);
  useEffect(() => { void refresh(); }, [refresh]);
  async function act(action: "publish" | "link") {
    if (!state?.source.latestSnapshot) return; setBusy(true); setError("");setSuccess("");
    const source = state.source;
    try {
      if (action === "publish") await apiPost(`${base}/snapshot`, { documentId, documentVersionId: source.documentVersionId,
        analysisRunId: source.runId, baseSnapshotId: source.latestSnapshot!.id, expectedAnalysisRevision: source.analysisRevision,
        observations: [{ factCode: code, valueKind: kind, canonicalValue: value, pageNumber: Number(page), quote, reviewReason: reason }] },
        workflowDevelopmentHeaders("officer"), workflowAuthRole("officer"));
      else { await apiPost(`${base}/snapshots/${source.latestSnapshot!.id}/link`, { documentId, documentVersionId: source.documentVersionId,
        expectedAnalysisRevision: source.analysisRevision, expectedCaseRevision: caseRevision }, workflowDevelopmentHeaders("officer"), workflowAuthRole("officer")); onLinked(); }
      setSuccess(action==="publish"?"New human-origin snapshot published. Original snapshot and OCR remain preserved. Explicit linking is still required.":"Human-origin snapshot explicitly linked to this case. Prior screening/planning bindings require reassessment.");
    } catch (e) { setError(e instanceof Error ? e.message : "Human evidence action rejected"); } finally { await refresh(); setBusy(false); }
  }
  const snapshot = state?.source.latestSnapshot;
  const candidates = fields.filter(f => !snapshot?.entries.some(e => e.factCode === f.factCode));
  if (!state?.canPublish || !snapshot) return error?<p role="alert">{error}</p>:null;
  return <section aria-label="Human document evidence"><h3>Human evidence for fields without supported candidates</h3>
    <p>Review the selected Proposal page. A source-backed human observation creates a new snapshot; it never becomes a machine candidate or replaces supported facts.</p>
    {error && <p role="alert">{error}</p>}{success&&<p role="status">{success}</p>}
    {!state?.canPublish && <p>Blocked: {state?.authorityRequired ?? "Verified reviewer authority is unavailable."}</p>}
    {!snapshot && <p>Publish the reviewed machine-candidate snapshot first. Unsupported candidates may remain unsupported.</p>}
    <label>Canonical field <select value={code} onChange={e => setCode(e.target.value)}><option value="">Select</option>{candidates.map(f => <option key={f.factCode} value={f.factCode}>{f.displayName} ({f.status})</option>)}</select></label>
    <label>Value kind <select value={kind} onChange={e => setKind(e.target.value)}>{["Text", "Identifier", "Integer", "Decimal", "Date", "Boolean"].map(k => <option key={k}>{k}</option>)}</select></label>
    <label>Reviewed canonical value <input value={value} onChange={e => setValue(e.target.value)} /></label>
    <label>Source page <input type="number" min="1" value={page} onChange={e => setPage(e.target.value)} /></label>
    <Button disabled={busy||Number(page)<1} onClick={async()=>{setError("");try{const source=await apiGet<{text?:string}>(`${base}/page?documentId=${documentId}&pageNumber=${Number(page)}`,workflowDevelopmentHeaders("officer"),workflowAuthRole("officer"));setTranscript(source.text||"");if(!source.text)setError("This run has no persisted OCR text for the requested page.");}catch(e){setError(e instanceof Error?e.message:"Source page unavailable");}}}>Load exact OCR source page</Button>
    {transcript&&<label>Persisted OCR page text <textarea readOnly value={transcript}/></label>}
    <ProposalEvidenceViewer caseId={caseId} documentId={documentId} versionId={state.source.documentVersionId} pageNumber={Number(page)||undefined}/>
    <label>Exact quote from this run&apos;s page <textarea value={quote} onChange={e => setQuote(e.target.value)} /></label>
    <label>Review explanation <textarea value={reason} onChange={e => setReason(e.target.value)} /></label>
    <label><input type="checkbox" checked={publishConsent} onChange={e=>setPublishConsent(e.target.checked)}/> Explicitly publish a new human-origin snapshot; this does not replace OCR or automatically link it.</label>
    <Button disabled={!publishConsent || busy || !state?.canPublish || state.source.runState !== "Completed" || !snapshot || !code || !value.trim() || Number(page) < 1 || !quote.trim() || !reason.trim()}
      onClick={() => void act("publish")}>Publish new human-evidence snapshot</Button>
    {snapshot && snapshot.id !== linkedSnapshotId && snapshot.entries.some(e => e.sourceKind === "HumanDocumentEvidence") && <>
      <p>Published snapshot {snapshot.id} awaits explicit case linking. Linking makes prior screening/planning bindings stale.</p>
      <Button disabled={busy || !state?.canPublish} onClick={() => void act("link")}>Link this exact human-evidence snapshot</Button></>}
    {snapshot?.entries.filter(e => e.humanEvidence).map(e => <p key={e.factCode}>{e.factCode}: HumanDocumentEvidence, page {e.humanEvidence!.pageNumber} — {e.humanEvidence!.quote}</p>)}
  </section>;
}
