"use client";
import {classificationNeedsReview} from "../services/completenessActions";
import {useState} from "react";
import {apiPost} from "@/lib/apiClient";
import {Button} from "@/shared/components/Button";
import type {CaseCompletenessSummaryResponse} from "../types/caseManagement";
type Classification={id:string;documentBinding:{governedDocumentId:string};originalClassificationCode?:string;status:string};
export function CompletenessNextActions({caseId,summary,onChanged,onRequest}:{caseId:string;summary:CaseCompletenessSummaryResponse;onChanged:()=>void;onRequest:(section:string,instructions:string)=>void}){
 const [review,setReview]=useState<Classification>();const [decision,setDecision]=useState("Confirmed");const [code,setCode]=useState("Proposal");const [reason,setReason]=useState("");const [error,setError]=useState("");const [busy,setBusy]=useState(false);
 const doc=summary.documentCompleteness; const proposal=summary.proposalContent;
 const classifications=(doc as (typeof doc & {classifiedDocuments?:Classification[]}))?.classifiedDocuments??[];
 const reviews=(doc as (typeof doc & {reviews?:{classifiedDocumentId:string;decision:string}[]}))?.reviews??[];
 const pending=classifications.filter(d=>classificationNeedsReview(d.id,d.status,reviews));
 const unresolved=[...(proposal?.missingMandatory??[]).map(r=>({...r,state:"Missing"})),...(proposal?.reviewRequiredMandatory??[]).map(r=>({...r,state:"Needs source review"}))];
 return <section aria-label="Outstanding completeness actions"><h3>Outstanding completeness items</h3>
 <p>Supporting documents: {doc?.outcome??"Not assessed"}. Proposal content: {proposal?.outcome??"Not assessed"}{proposal?.isConfirmed?" (confirmed findings; this does not make them complete)":""}.</p>
 {error&&<p role="alert">{error}</p>}
 {pending.map(d=><p key={d.id}>Uploaded {d.originalClassificationCode??"unclassified document"}: {d.status}. <Button onClick={()=>{setReview(d);setDecision("Confirmed");setCode(d.originalClassificationCode??"");setError("");}}>Review {d.originalClassificationCode??"document"} classification</Button></p>)}
 {review&&<fieldset><legend>Review the actual document classification</legend><p>Open the uploaded document and determine its classification. This does not confirm its content.</p>
 <label>Classification decision <select value={decision} onChange={e=>setDecision(e.target.value)}>{["Confirmed","Corrected","Rejected"].map(d=><option key={d}>{d}</option>)}</select></label>
 {decision==="Corrected"&&<label>Reviewed classification code <input value={code} onChange={e=>setCode(e.target.value)}/></label>}
 {decision!=="Confirmed"&&<label>Classification review reason <textarea value={reason} onChange={e=>setReason(e.target.value)}/></label>}
 <Button disabled={busy||(decision!=="Confirmed"&&!reason.trim())||(decision==="Corrected"&&!code.trim())} onClick={async()=>{setBusy(true);try{await apiPost(`/api/workflow-governance/cases/${caseId}/completeness/documents/${doc!.id}/review`,{classifiedDocumentId:review.id,documentId:review.documentBinding.governedDocumentId,expectedAssessmentRevision:doc!.revision,decision,correctedClassificationCode:decision==="Corrected"?code:null,reason:decision==="Confirmed"?null:reason},undefined,"officer");setReview(undefined);onChanged();}catch(e){setError(e instanceof Error?e.message:"Classification review rejected");}finally{setBusy(false);}}}>Record classification review</Button><Button onClick={()=>setReview(undefined)}>Cancel classification review</Button></fieldset>}
 <p>{unresolved.length} mandatory template items need resolution. Uncertain or unreadable means review, not proof of absence.</p>
 <details><summary>Choose an outstanding item or request correction ({unresolved.length})</summary><ul>{unresolved.map(r=><li key={r.id}><strong>{r.displayName}</strong>: {r.state}. <button onClick={()=>{window.dispatchEvent(new CustomEvent("review-template-item",{detail:r.id}));setTimeout(()=>document.getElementById(`template-${r.id}`)?.scrollIntoView({behavior:"smooth",block:"center"}),0);}}>Review source for {r.displayName}</button> <button onClick={()=>onRequest(r.id,`Please provide or clarify ${r.displayName}, including its applicable source evidence or an explicit grounded inapplicability explanation.`)}>Request applicant correction for {r.displayName}</button></li>)}</ul></details>
 </section>;
}
