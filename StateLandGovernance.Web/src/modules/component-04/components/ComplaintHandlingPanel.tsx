"use client";
import {useEffect,useId,useRef,useState,type Dispatch,type SetStateAction} from "react";
import {complaintHandlingApi,handlingFields,evidenceReferences,orderedHandlingHistory,type HandlingAction,type HandlingView} from "../services/complaintHandling";
import {definitelyRejected} from "../services/requestOutcome";
import {useOfficerSession} from "./OfficerFlowSession";
import {ComplaintHandlingHistory} from "./ComplaintHandlingHistory";
import {statusLabel} from "../services/presentation";
import formStyles from "./ComplaintActionForm.module.css";
export function DsRoutingReason({view,central}:{view:HandlingView;central:boolean}){
 const assignment=view.history.filter(x=>x.operation==="AssignDs"||x.operation==="ReassignDs").sort((a,b)=>b.revision-a.revision)[0];
 return !central&&view.state.mode==="Ds"&&assignment?.reason?.trim()?<section aria-label="Commissioner routing reason"><h3>Commissioner routing reason</h3><p style={{whiteSpace:"pre-wrap"}}>{assignment.reason}</p></section>:null;
}
export function ComplaintHandlingPanel({id,view,central,onSaved}:{id:string;view:HandlingView;central:boolean;onSaved:(message?:string)=>Promise<void>}){
 const summaryHelp=useId(),referenceHelp=useId(),session=useOfficerSession(),flight=useRef(false),pending=useRef<HandlingAction|null>(null),feedback=useRef<HTMLDivElement>(null);
 const [operation,setOperation]=useState(""),[reason,setReason]=useState(""),[details,setDetails]=useState<Record<string,string>>({}),[refs,setRefs]=useState(""),[summary,setSummary]=useState(""),[confirmed,setConfirmed]=useState(false);
 const [busy,setBusy]=useState(false),[retained,setRetained]=useState(false),[error,setError]=useState(""),[message,setMessage]=useState(""),[historyOpen,setHistoryOpen]=useState(false),[needsRefresh,setNeedsRefresh]=useState(false),[replayAvailable,setReplayAvailable]=useState(false);
 const marker=`c4-handling:${session.actor}:${id}`;
 useEffect(()=>{try{if(sessionStorage.getItem(marker))queueMicrotask(()=>{setRetained(true);setMessage("An earlier action outcome is unconfirmed. Refresh saved history; its original payload is unavailable after reload, so this page cannot safely replay it or create a replacement.");});}catch{}},[marker]);
 useEffect(()=>{if(message||error)feedback.current?.focus();},[message,error]);
 function storeMarker(saved:boolean){try{if(saved)sessionStorage.setItem(marker,"unconfirmed");else sessionStorage.removeItem(marker);}catch{}}
 async function refreshHistory(){setHistoryOpen(true);try{await onSaved();setNeedsRefresh(false);}catch{setError("Saved handling history could not be refreshed. No action was submitted.");}}
 async function save(){
  if(flight.current||needsRefresh||!operation||retained&&!pending.current)return;const epoch=session.epoch.current;setError("");
  try{
   const input=pending.current??{submissionKey:crypto.randomUUID(),expectedHandlingRevision:view.state.revision,operation,reason,details,evidenceReferences:evidenceReferences(refs),publicSummary:summary};
   pending.current=input;setReplayAvailable(true);setRetained(true);storeMarker(true);flight.current=true;setBusy(true);setMessage(`Recording ${statusLabel(input.operation)}…`);
   const result=await complaintHandlingApi.act(id,central,input);if(epoch!==session.epoch.current)return;
   if(!result.eventId||!result.current||!Number.isInteger(result.current.revision))throw new Error("Action response did not confirm a saved event.");
   pending.current=null;setReplayAvailable(false);setRetained(false);storeMarker(false);setHistoryOpen(true);setNeedsRefresh(true);setOperation("");const accepted=`${statusLabel(input.operation)} recorded successfully${result.reused?" (existing event retrieved)":""}. Saved handling revision ${result.current.revision}.`;setMessage(accepted);
   try{await onSaved(accepted);setNeedsRefresh(false);}catch{setError("Action accepted; current state/history could not be refreshed. Refresh history without recording another action.");}
  }catch(caught){if(epoch===session.epoch.current){if(definitelyRejected(caught)||!pending.current){pending.current=null;setReplayAvailable(false);setRetained(false);storeMarker(false);setMessage("");}else setMessage("Action status unconfirmed. Refresh history before replaying the same action; its exact key and payload are retained.");setError(caught instanceof Error?caught.message:"Action not confirmed.");}}
  finally{flight.current=false;if(epoch===session.epoch.current)setBusy(false);}
 }
 const actions=view.allowedActions.filter(x=>x!=="AssignDs"),history=orderedHandlingHistory(view);
 return <section aria-label="Complaint handling"><DsRoutingReason view={view} central={central}/><small>Complaint reference: {id}</small><h3>Handling: {statusLabel(view.state.status)}</h3><p>Responsibility: {view.state.mode==="None"?"No active handler":view.state.mode==="Ds"?"Assigned DS officer":"Commissioner"}. Start Investigation is a lifecycle action; Under review is a separate triage status.</p>
 <div ref={feedback} tabIndex={-1}>{message&&<p role="status" aria-live="polite" aria-atomic="true">{message}</p>}{error&&<p role="alert">{error}</p>}</div>
 <button type="button" disabled={busy} onClick={()=>void refreshHistory()}>Refresh handling history</button>
 <p>Choosing an action does not submit it. Complete its fields and press Record to save a handling event. Recording triage does not start an investigation.</p>
 {view.state.responsibleActorId&&central&&<details><summary>Responsible officer reference</summary><p>{view.state.responsibleActorId}</p></details>}
 {actions.length>0&&<form className={formStyles.form} onSubmit={e=>{e.preventDefault();void save();}}>
 <label className={formStyles.field}>Available action<select required value={operation} disabled={busy||retained||needsRefresh} onChange={e=>{setOperation(e.target.value);setDetails({});setConfirmed(false);setMessage("");setError("");}}><option value="">Choose an authorized action</option>{actions.map(x=><option key={x} value={x}>{statusLabel(x)}</option>)}</select></label>
 {operation&&<><ComplaintHandlingFields operation={operation} reason={reason} details={details} refs={refs} summary={summary} confirmed={confirmed} busy={busy} retained={retained||needsRefresh} summaryHelp={summaryHelp} referenceHelp={referenceHelp} setReason={setReason} setDetails={setDetails} setSummary={setSummary} setRefs={setRefs} setConfirmed={setConfirmed}/><button disabled={busy||retained&&!replayAvailable||!reason.trim()||(["Resolve","Close","Reopen"].includes(operation)&&!confirmed)}>{busy?`Recording ${statusLabel(operation)}…`:retained?"Replay retained action":`Record ${statusLabel(operation)}`}</button></>}
 </form>}
 <details open={historyOpen} onToggle={e=>setHistoryOpen(e.currentTarget.open)}><summary>Handling and decision history ({history.length})</summary><p>DS Review Recorded mirrors a triage write in the lifecycle audit; it is not Start Investigation. Repeated triage entries retain separate revisions.</p><ComplaintHandlingHistory view={view}/></details>
 </section>;
}

type HandlingFieldsProps={operation:string;reason:string;details:Record<string,string>;refs:string;summary:string;confirmed:boolean;busy:boolean;retained:boolean;summaryHelp:string;referenceHelp:string;setReason:(value:string)=>void;setDetails:Dispatch<SetStateAction<Record<string,string>>>;setSummary:(value:string)=>void;setRefs:(value:string)=>void;setConfirmed:(value:boolean)=>void};
export function ComplaintHandlingFields({operation,reason,details,refs,summary,confirmed,busy,retained,summaryHelp,referenceHelp,setReason,setDetails,setSummary,setRefs,setConfirmed}:HandlingFieldsProps){
 const terminal=["Resolve","Close","Reopen"].includes(operation);
 return <><label className={formStyles.field}>Reason<textarea required maxLength={2000} disabled={busy||retained} value={reason} onChange={e=>setReason(e.target.value)}/></label>
 {handlingFields(operation).map(field=><label className={formStyles.field} key={field}>{statusLabel(field)}{field==="outcome"?<select required disabled={busy||retained} value={details[field]??""} onChange={e=>setDetails(old=>({...old,[field]:e.target.value}))}><option value="">Choose recorded outcome</option>{["ActionTaken","NoFurtherAction","UnableToDetermine"].map(x=><option key={x} value={x}>{statusLabel(x)}</option>)}</select>:<textarea className={field==="notes"?formStyles.notes:undefined} required maxLength={field==="notes"?10000:field==="decisionBasis"?2000:3000} disabled={busy||retained} value={details[field]??""} onChange={e=>setDetails(old=>({...old,[field]:e.target.value}))}/>}</label>)}
 <label className={formStyles.field}>Applicant-safe summary<textarea aria-describedby={summaryHelp} required={terminal||operation==="RequestInformation"} maxLength={2000} disabled={busy||retained} value={summary} onChange={e=>setSummary(e.target.value)}/></label><small id={summaryHelp}>Only this summary is shown in the applicant handling history. Do not include private investigation notes.</small>
 <details><summary>Evidence references (optional)</summary><label className={formStyles.field}>Kind | reference | label<textarea aria-describedby={referenceHelp} disabled={busy||retained} value={refs} onChange={e=>setRefs(e.target.value)} placeholder="ExternalReference | actual reference | description"/></label><p id={referenceHelp}>Use C4Record or ExternalReference. References do not upload files, fetch sources or verify allegations.</p></details>
 {terminal&&<label className={formStyles.confirmation}><input type="checkbox" required checked={confirmed} disabled={busy||retained} onChange={e=>setConfirmed(e.target.checked)}/> I confirm this reasoned {statusLabel(operation).toLowerCase()} action for this complaint. Earlier decisions remain in history.</label>}
</>;
}
