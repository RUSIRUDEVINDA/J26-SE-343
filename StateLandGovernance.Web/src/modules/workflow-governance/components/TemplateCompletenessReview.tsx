"use client";
import { useEffect, useState } from "react";
import { apiGet, apiPost } from "@/lib/apiClient";
import { workflowAuthRole, workflowDevelopmentHeaders } from "../services/workflowDevelopmentHeaders";
import styles from "./TemplateCompletenessReview.module.css";
import { ProposalEvidenceViewer } from "./ProposalEvidenceViewer";
import { reviewFilters, type ReviewFilter, matchesObservationFilter } from "../services/reviewFilters";
import { Button } from "@/shared/components/Button";
type Observation = { requirementId: string; state: string; pageNumber?: number | null; textSpan?: string | null;
  evidenceReference?: string | null; extractionReference?: string | null; explanation?: string | null };
type Payload = { observationSource?: string; savedReviewAtUtc?: string; sourceBinding: { extractionReference?: string; proposalDocumentId?:string; documentVersionId?:string }; template: { name: string; authoritativeSourceReference: string;
  requirements: { id: string; displayName: string; applicabilityContext?: string; authoritativeSourceReference: string }[] }; observations?: Observation[] };
export function TemplateCompletenessReview({ caseId, assessmentId, onChanged, documentId, versionId, isConfirmed = false }: { caseId: string; assessmentId?: string; onChanged?: () => void; documentId?: string; versionId?: string; isConfirmed?: boolean }) {
  const base = `/api/workflow-governance/cases/${caseId}`;
  const [filter,setFilter]=useState<ReviewFilter>("Needs review"); const [item,setItem]=useState(""); const [dirty,setDirty]=useState(false);
  const [payload, setPayload] = useState<Payload>(); const [rows, setRows] = useState<Observation[]>([]);
  const [reason, setReason] = useState(""); const [evidence, setEvidence] = useState(""); const [error, setError] = useState("");
  const [success,setSuccess]=useState("");
  const [busy, setBusy] = useState(false); const [selected, setSelected] = useState(""); const [due, setDue] = useState("");
  useEffect(() => {
    setPayload(undefined); setRows([]); setDirty(false); if (!assessmentId) return;
    apiGet<Payload>(`${base}/completeness/proposal/${assessmentId}/observations`, workflowDevelopmentHeaders("officer"), workflowAuthRole("officer"))
      .then(p => { setPayload(p); setRows(p.template.requirements.map(r => p.observations?.find(o => o.requirementId === r.id) || {
        requirementId: r.id, state: "Uncertain", extractionReference: p.sourceBinding.extractionReference, explanation: "Officer observation required." })); })
      .catch(e => setError(e instanceof Error ? e.message : "Template observations unavailable"));
  }, [base, assessmentId]);
  useEffect(()=>{const select=(event:Event)=>{const id=(event as CustomEvent<string>).detail;setFilter("All");setItem(id);};window.addEventListener("review-template-item",select);return()=>window.removeEventListener("review-template-item",select);},[]);
  function update(id: string, changes: Partial<Observation>) { setDirty(true); setRows(previous => previous.map(r => r.requirementId === id ? { ...r, ...changes } : r)); }
  async function submit(action: "confirm" | "correct" | "correction", next = false) {
    setBusy(true); setError(""); setSuccess("");
    let saved:{id?:string;isConfirmed?:boolean;outcome?:string}|undefined;
    try {
      if (action === "correction") await apiPost(`${base}/corrections`, { requiredAction: "ReviseProposal", requiredClassificationCode: "Proposal",
        requiredSectionId: selected, officerInstructions: reason, dueDateUtc: new Date(due).toISOString() }, workflowDevelopmentHeaders("officer"), workflowAuthRole("officer"));
      else {
        const c = await apiGet<{ revision: number }>(base, workflowDevelopmentHeaders("officer"), workflowAuthRole("officer"));
        saved=await apiPost<{id?:string;isConfirmed?:boolean;outcome?:string}>(`${base}/completeness/proposal/${assessmentId}/${action}`, action === "confirm" ? { expectedCaseRevision: c.revision, notes: reason }
          : { expectedCaseRevision: c.revision, reason, evidenceReference: evidence, observations: rows }, workflowDevelopmentHeaders("officer"), workflowAuthRole("officer"));
      }
      setDirty(false);
      if(next){const index=visible.findIndex(r=>r.id===current?.id);setItem(visible[index+1]?.id || "");}
      setSuccess(action==="confirm"?"Confirmation saved to PostgreSQL for the displayed assessment. Findings were not changed.":action==="correct"?`Corrected observations saved to PostgreSQL. ${saved?.isConfirmed?"Officer-confirmed assessment":"Assessment not confirmed"}; outcome: ${saved?.outcome||"see persisted assessment"}. This does not approve the application.`:"Applicant correction request saved. Awaiting applicant response and officer review.");
      onChanged?.();
    } catch (e) { setError(e instanceof Error ? e.message : "Review rejected"); } finally { setBusy(false); }
  }
  const visible = payload?.template.requirements.filter(r=>filter==="Reviewed" ? isConfirmed : matchesObservationFilter(payload.observations?.find(o=>o.requirementId===r.id)?.state || "Uncertain",filter)) || [];
  const current=visible.find(r=>r.id===item)||visible[0];
  return <section className={styles.review} aria-label="Template observations and correction"><h3>Template observations and officer review</h3>
    <p>Independent of the 17-field fact catalogue. OCR/grounding failure requires review, not a finding of missing content.</p>
    {error && <p role="alert">{error}</p>}{success&&<p role="status">{success}</p>}{!assessmentId && <p>Run template completeness for the selected Proposal first.</p>}
    {payload && <><h4>{payload.template.name}</h4>
      <details><summary>Template source and technical binding</summary><p>{payload.template.authoritativeSourceReference}</p><p>Assessment: {assessmentId}</p></details>
      <p>{isConfirmed ? "Assessment has a persisted officer confirmation. This does not mean all requirements are complete." : "Assessment is not confirmed."} {payload.observationSource === "SavedOfficerReview" ? `Saved officer observations retrieved (${payload.savedReviewAtUtc}).` : "Machine observations below are source suggestions; a Present finding alone is not officer approval."}</p>
      <fieldset><legend>Template requirement filter</legend>{reviewFilters.map(f=><button key={f} type="button" aria-pressed={filter===f} onClick={()=>setFilter(f)}>{f}</button>)}</fieldset>
      <p>{visible.length} of {payload.template.requirements.length} requirements shown. {dirty ? "Unsaved observation edits" : "No unsaved observation edits"}. Reviewed shows a confirmed assessment, not individual item approvals.</p>
      <label>Selected template requirement <select aria-label="Selected template requirement" value={current?.id||""} onChange={e=>setItem(e.target.value)}><option value="" disabled>Select requirement</option>{visible.map(r=><option key={r.id} value={r.id}>{r.displayName}</option>)}</select></label>
      {!current && <p>No items match this filter. Use All to inspect the complete template.</p>}
      {current && (()=>{const r=current;const o=rows.find(x=>x.requirementId===r.id)!;return <div style={{display:"grid",gridTemplateColumns:"repeat(auto-fit,minmax(280px,1fr))",gap:"1.5rem"}}>
        <article id={`template-${r.id}`}><h4>{r.displayName}</h4><p>{r.applicabilityContext}</p>
          <p><strong>Extracted answer / source quote:</strong> {o.textSpan||"No grounded answer available. Review the PDF or request applicant evidence."}</p>
          <label>Finding <select aria-label={`${r.displayName} observation`} value={o.state} onChange={e=>update(r.id,{state:e.target.value})}>{["Present","Missing","Uncertain","Unreadable","Unsupported"].map(v=><option key={v}>{v}</option>)}</select></label>
          <label>Source page <input type="number" min={1} value={o.pageNumber??""} onChange={e=>update(r.id,{pageNumber:e.target.value?Number(e.target.value):null})}/></label>
          <label>Exact evidence quote <textarea aria-label="Exact evidence quote" value={o.textSpan??""} onChange={e=>update(r.id,{textSpan:e.target.value||null})}/></label>
          <label>Evidence reference <input value={o.evidenceReference??""} onChange={e=>update(r.id,{evidenceReference:e.target.value||null})}/></label>
          <label>Finding explanation / required action <textarea aria-label="Finding explanation / required action" value={o.explanation??""} onChange={e=>update(r.id,{explanation:e.target.value})}/></label>
          <details><summary>Requirement source</summary>{r.authoritativeSourceReference}<br/>{r.id}</details>
          <button type="button" onClick={()=>{setSelected(r.id);document.getElementById("template-correction-request")?.scrollIntoView({behavior:"smooth"});}}>Prepare correction for this item</button>
        </article>
        <ProposalEvidenceViewer caseId={caseId} documentId={payload.sourceBinding.proposalDocumentId || documentId} versionId={payload.sourceBinding.documentVersionId || versionId} pageNumber={o.pageNumber}/>
      </div>;})()}
      <p>For a conditional item, record its grounded inapplicability explanation and review it explicitly. The frozen observation enum has no separate “Not applicable” state; an unresolved applicability remains Uncertain.</p>
      <label>Review / correction reason <textarea aria-label="Review / correction reason" value={reason} onChange={e => setReason(e.target.value)} /></label>
      <label>Officer review evidence reference <input aria-label="Officer review evidence reference" value={evidence} onChange={e => setEvidence(e.target.value)} /></label>
      <p>Saving records the full displayed observation set as an authorized officer correction, with your reason and evidence. The existing domain operation marks that reviewed assessment as officer-confirmed; unresolved findings still block completeness. This does not approve the application.</p>
      <Button disabled={busy || !current || !reason.trim() || !evidence.trim()} onClick={() => void submit("correct", true)}>Save & Next requirement</Button>
      <Button disabled={busy || !reason.trim() || !evidence.trim()} onClick={() => void submit("correct")}>Record authorized corrected observations</Button>
      <Button disabled={busy || isConfirmed || dirty || !reason.trim()} onClick={() => void submit("confirm")}>Confirm displayed assessment without changing findings</Button>
      {dirty && <p>Save or discard observation edits before confirming the persisted assessment.</p>}
      <fieldset id="template-correction-request"><legend>Applicant correction request</legend><label>Template item <select aria-label="Template item" value={selected} onChange={e => setSelected(e.target.value)}>
        <option value="">Select requirement</option>{payload.template.requirements.map(r => <option key={r.id} value={r.id}>{r.displayName}</option>)}</select></label>
        <label>Explicit due date <input aria-label="Explicit due date" type="datetime-local" value={due} onChange={e => setDue(e.target.value)} /></label>
        <Button disabled={busy || !selected || !due || !reason.trim()} onClick={() => void submit("correction")}>Send applicant correction request</Button>
      </fieldset>
    </>}
  </section>;
}
