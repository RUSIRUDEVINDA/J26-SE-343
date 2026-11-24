"use client";
import {CurrentRegulatoryCards} from "./CurrentRegulatoryCards";
import {useEffect,useRef,useState} from "react";
import type {RegulatoryFinding} from "../services/independentRegulatoryReports";
import {actionableFollowupFinding} from "../services/regulatoryFollowups";
import {FollowupRejectedError,followupDraftKey,followupOperations,followupOperationLabel,followupRevision,humanReviewOutcomes,regulatoryFollowups,type FollowupHistory,type FollowupInput} from "../services/regulatoryFollowups";
import {statusLabel} from "../services/presentation";
import styles from "./RegulatoryFollowups.module.css";

import {RegulatoryFollowupEntries,FollowupTechnicalDetails,CurrentFindingDispositions} from "./RegulatoryFollowupHistory";
import {latestFindingFollowup} from "../services/regulatoryFollowups";
import historyStyles from "./AssessmentHistory.module.css";

export function RegulatoryFollowups({evaluationId,findings,onRecorded,progressionHolds=[],currentWorkflow=false}:{currentWorkflow?:boolean;progressionHolds?:string[];onRecorded?:()=>void;evaluationId:string;findings:RegulatoryFinding[]}){
 const [view,setView]=useState<FollowupHistory|null>(null),[error,setError]=useState(""),[notice,setNotice]=useState(""),[busy,setBusy]=useState(false),[pending,setPending]=useState<FollowupInput|null>(null);
 const [rule,setRule]=useState(""),[operation,setOperation]=useState("ClarificationRequested"),[outcome,setOutcome]=useState<string>(humanReviewOutcomes[0]),[remarks,setRemarks]=useState(""),[missing,setMissing]=useState(""),[references,setReferences]=useState("");
 const epoch=useRef(0),flight=useRef(false);
 useEffect(()=>{let active=true;const abort=new AbortController();
  const load=()=>{const version=++epoch.current;flight.current=false;setBusy(false);setView(null);setError("");setNotice("");setPending(null);setRule("");setRemarks("");setMissing("");setReferences("");
   try{const saved=sessionStorage.getItem(followupDraftKey(evaluationId));if(saved)setPending(JSON.parse(saved) as FollowupInput);}catch{setError("The local pending request could not be read. Do not create another submission until it is reconciled.");return;}
   void regulatoryFollowups.read(evaluationId,abort.signal).then(x=>{if(active&&version===epoch.current)setView(x);}).catch(e=>{if(active&&version===epoch.current)setError(e instanceof Error?e.message:"Follow-up unavailable.");});};
  queueMicrotask(()=>{if(active)load();});window.addEventListener("development-session-changed",load);return()=>{active=false;abort.abort();window.removeEventListener("development-session-changed",load);};
 },[evaluationId]);
 const eligible=findings.filter(actionableFollowupFinding),finding=eligible.find(x=>x.ruleCode===rule),operations=finding?followupOperations(finding):[];
 const closing=operation==="FindingVerifiedClosed",closure=finding?view?.closureEligibility?.find(x=>x.ruleCode===finding.ruleCode&&x.ruleVersion===finding.ruleVersion):undefined;
 const closed=!!finding&&!!view&&latestFindingFollowup(view.history,finding)?.operation==="FindingVerifiedClosed";
 async function refresh(){const version=epoch.current;try{const next=await regulatoryFollowups.read(evaluationId);if(version===epoch.current)setView(next);}catch(e){if(version===epoch.current)setError(e instanceof Error?e.message:"Read failed.");}}
 async function save(){if(flight.current||!view?.canWrite)return;flight.current=true;setBusy(true);setError("");setNotice("");const version=epoch.current;
  try{const input=pending??{submissionKey:crypto.randomUUID(),ruleCode:rule,operation,reviewOutcome:operation==="HumanReviewRecorded"?outcome:null,missingInformation:operation==="ClarificationRequested"?missing.trim():null,remarks:operation==="FindingVerifiedClosed"?"":remarks.trim(),references:operation==="FindingVerifiedClosed"?[]:references.split("\n").map(x=>x.trim()).filter(Boolean),expectedRevision:followupRevision(view.history,rule)};
   sessionStorage.setItem(followupDraftKey(evaluationId),JSON.stringify(input));setPending(input);
   const result=await regulatoryFollowups.record(evaluationId,input);if(version!==epoch.current)return;
   sessionStorage.removeItem(followupDraftKey(evaluationId));setPending(null);setRemarks("");setMissing("");setReferences("");setNotice(result.reused?`Saved event retrieved; current rule revision ${result.currentRevision}. No screening ran.`:result.event.operation==="FindingVerifiedClosed"?"Verified & Closed: this finding only. The original regulatory result and all other finding statuses remain unchanged.":"Internal follow-up recorded. No applicant delivery, finding change or reassessment occurred.");await refresh();onRecorded?.();
  }catch(e){if(version===epoch.current){if(e instanceof FollowupRejectedError){sessionStorage.removeItem(followupDraftKey(evaluationId));setPending(null);await refresh();setError(e.message+" The server rejected this request. Review the current history before a new deliberate submission.");}else setError((e instanceof Error?e.message:"Save not confirmed.")+" The exact pending request is retained. Refresh history and deliberately replay; do not invent another key.");}}
  finally{if(version===epoch.current){flight.current=false;setBusy(false);}}
 }
 return <>{currentWorkflow&&<CurrentRegulatoryCards evaluationId={evaluationId} findings={findings} events={view?.history??null}/>}<section className={styles.panel} aria-label="Regulatory follow-up"><h2>Current Regulatory Follow-ups</h2>{view&&<p><strong>{findings.filter(f=>latestFindingFollowup(view.history,f)?.operation==="FindingVerifiedClosed").length} findings Verified &amp; Closed</strong>. Other findings retain their own C4 status. Later approvals remain separate.</p>}<p>Record an internal DS follow-up for the selected C4 finding. Original findings remain unchanged; this does not deliver a request to an applicant or grant legal or institutional approval.</p>
  <p>Only Insufficient Information and Human Review Required findings appear in the action selector. Compliant findings remain in the saved report and need no follow-up.</p>
  <p>No Issues Found — Verify and Close records the individual officer disposition for the selected finding. No remarks, evidence or references are required. A saved closure resolves that individual finding for C4 screening only. It records no external approval.</p>
  {error&&<p role="alert">{error}</p>}{notice&&<p role="status">{notice}</p>}{!view&&!error&&<p role="status">Reading follow-up history…</p>}
  {view&&<><button type="button" disabled={busy} onClick={()=>void refresh()}>Refresh follow-up history</button>
   {!view.canWrite&&<p>Read-only. Only the currently eligible responsible DS who created this assessment can record follow-up.</p>}
   {view.canWrite&&pending&&<div role="status"><p>An exact request is pending confirmation for {pending.ruleCode}. No automatic retry will occur.</p><button type="button" disabled={busy} onClick={()=>void save()}>Replay Same Follow-up Request</button></div>}
   {view.canWrite&&!pending&&eligible.length>0&&<form onSubmit={e=>{e.preventDefault();void save();}}><fieldset disabled={busy}><legend>Record internal follow-up</legend>
    <label>Saved finding<select required value={rule} onChange={e=>{setRule(e.target.value);const f=eligible.find(x=>x.ruleCode===e.target.value);if(f)setOperation(view&&latestFindingFollowup(view.history,f)?.operation==="FindingVerifiedClosed"?"FindingVerifiedClosed":followupOperations(f)[0]);}}><option value="">Select a saved finding</option>{eligible.map(f=><option key={f.ruleCode} value={f.ruleCode}>{f.ruleCode} — {statusLabel(f.status)}</option>)}</select></label>
    {finding&&<><p>Saved rule version {finding.ruleVersion}. Current follow-up revision {followupRevision(view.history,rule)}.</p><label>Action<select disabled={closed} value={operation} onChange={e=>setOperation(e.target.value)}>{operations.map(x=><option key={x} value={x}>{followupOperationLabel(x)}</option>)}</select></label>
     {closed&&<p role="status">Verified &amp; Closed. This finding already has a saved officer resolution. A duplicate closure is not permitted; its history remains below.</p>}
     {closing&&!closed&&<p role="status">{closure?.reason??"Closure eligibility is unavailable from the current backend. No closure can be confirmed."}</p>}
     {operation==="NoFollowupRequired"&&<p>Record an administrative rationale only. This cannot waive required clarification, human or legal review and does not change the original screening outcome.</p>}
     {operation==="ClarificationRequested"?<label>Missing or undetermined information (20–2000 characters)<textarea required minLength={20} maxLength={2000} value={missing} onChange={e=>setMissing(e.target.value)}/></label>:operation==="HumanReviewRecorded"?<label>Reasoned review outcome<select value={outcome} onChange={e=>setOutcome(e.target.value)}>{humanReviewOutcomes.map(x=><option key={x} value={x}>{statusLabel(x)}</option>)}</select></label>:null}
     {!closing&&<><label>{operation==="ClarificationRequested"?"Clarification request (20–2000 characters)":"Required review remarks (20–2000 characters)"}<textarea required minLength={20} maxLength={2000} value={remarks} onChange={e=>setRemarks(e.target.value)}/></label><label>Optional descriptive references (one per line, maximum 10)<textarea value={references} onChange={e=>setReferences(e.target.value)}/></label></>}
     <button disabled={closed||(closing&&!closure?.canClose)} type="submit">{closed?"Already Verified & Closed":followupOperationLabel(operation)}</button></>}
   </fieldset></form>}
   {busy&&<p role="status">Saving or retrieving the exact follow-up request…</p>}
   {!currentWorkflow&&<CurrentFindingDispositions progressionHolds={progressionHolds} findings={findings} events={view.history} eligibility={view.closureEligibility}/>}
   <details className={historyStyles.section} open={currentWorkflow?undefined:true}><summary>{currentWorkflow?"Technical Details - ":""}Saved follow-up history ({view.history.length})</summary><RegulatoryFollowupEntries events={view.history} findings={findings}/>{!view.history.length&&<p>No internal follow-up events recorded.</p>}<FollowupTechnicalDetails evaluationId={evaluationId} events={view.history}/></details>
  </>}
 </section></>;
}
