"use client";
import {useEffect,useRef,useState} from "react";
import {manualEvidence,type EvidenceView,type VerificationInput} from "../services/manualEvidence";
import {EvidenceUpload,type UploadState} from "../services/evidenceUpload";
import {definitelyRejected} from "../services/requestOutcome";
import {investigationConcerns} from "../services/earlyInvestigation";
import {useOfficerSession} from "./OfficerFlowSession";
import {sriLankaTime,statusLabel} from "../services/presentation";
import styles from "./ManualEvidenceVerification.module.css";
import {PreviousEarlyEvidence} from "./PreviousEarlyEvidence";
import historyStyles from "./AssessmentHistory.module.css";
import {StatusBadge} from "./ProgressiveSteps";
export function UploadFeedback({state}:{state:UploadState}){
 if(state.phase==="loaded")return <p role="status" data-upload-state="loaded" className={styles.loaded}>Saved attachments are listed below.</p>;
 const pending=["validating","uploading","confirming"].includes(state.phase)||state.refreshing;
 return <div role="status" aria-live="polite" aria-atomic="true" data-upload-state={state.phase} className={styles.feedback}>{pending&&<span className={styles.spinner} aria-hidden="true"/>}<span>{state.message}</span>{state.fileName&&<small>{state.fileName} · {state.bytes?.toLocaleString()} bytes · {statusLabel(state.concern??"")}</small>}</div>;
}
export function ManualEvidenceVerification({requestId}:{requestId:string}){
 const session=useOfficerSession();
 return session.actor?<CurrentAssessmentEvidence key={`${session.actor}:${session.generation}:${requestId}`} requestId={requestId}/>:<p role="status">Loading authenticated evidence workspace…</p>;
}
function CurrentAssessmentEvidence({requestId}:{requestId:string}){
 const session=useOfficerSession(),epoch=session.epoch;
 const [view,setView]=useState<EvidenceView|null>(null),[concern,setConcern]=useState<string>(investigationConcerns[0][0]),[decision,setDecision]=useState("MoreInformationRequired"),[remarks,setRemarks]=useState(""),[references,setReferences]=useState(""),[selected,setSelected]=useState<string[]>([]),[decisionBusy,setDecisionBusy]=useState(false),[error,setError]=useState(""),[decisionUncertain,setDecisionUncertain]=useState(false),[decisionMessage,setDecisionMessage]=useState("");
 const [upload,setUpload]=useState<UploadState>({phase:"empty",message:"No file selected."});
 const file=useRef<HTMLInputElement>(null),feedback=useRef<HTMLDivElement>(null),uploader=useRef<EvidenceUpload|null>(null),decisionFlight=useRef(false),pendingDecision=useRef<VerificationInput|null>(null);
 useEffect(()=>{
  let active=true;const e=epoch.current;const markerKey=`c4-upload:${session.actor}:${requestId}`;
  const marker=(value:unknown)=>{try{if(value)sessionStorage.setItem(markerKey,JSON.stringify(value));else sessionStorage.removeItem(markerKey);}catch{/* Exact payload remains in memory when storage is unavailable. */}};
  let previous=null;try{previous=JSON.parse(sessionStorage.getItem(markerKey)||"null");}catch{}
  const controller=new EvidenceUpload({requestId,attach:(form,waiting)=>manualEvidence.attach(requestId,form,waiting),read:()=>manualEvidence.read(requestId),updated:value=>{if(active&&e===epoch.current)setView(value);},changed:state=>{if(active&&e===epoch.current)setUpload(state);},key:()=>crypto.randomUUID(),marker},previous);uploader.current=controller;
  queueMicrotask(()=>{if(!active)return;setView(null);setError("");setDecisionMessage("");setDecisionUncertain(false);pendingDecision.current=null;setUpload(controller.state);void controller.refresh().catch(caught=>{if(active&&e===epoch.current)setError(caught.message);});});
  return()=>{active=false;controller.dispose();if(uploader.current===controller)uploader.current=null;};
 },[requestId,session.actor,session.generation,epoch]);
 useEffect(()=>{if(["saved","failed","uncertain"].includes(upload.phase)||decisionMessage||error)feedback.current?.focus();},[upload.phase,decisionMessage,error]);
 const uploadBusy=["validating","uploading","confirming"].includes(upload.phase)||upload.refreshing===true,unresolved=upload.phase==="uncertain"||decisionUncertain,busy=uploadBusy||decisionBusy;
 async function refresh(){try{setError("");await uploader.current?.refresh();}catch(caught){setError(caught instanceof Error?caught.message:"Could not refresh saved evidence.");}}
 async function saveDecision(){
  if(decisionFlight.current||uploader.current?.busy||uploader.current?.unresolved||!view?.canVerify)return;
  const e=epoch.current;decisionFlight.current=true;setDecisionBusy(true);setError("");setDecisionMessage("Recording verification decision…");
  try{
   const input=pendingDecision.current??{submissionKey:crypto.randomUUID(),concern,expectedRevision:Math.max(0,...view.history.filter(h=>h.concern===concern).map(h=>h.revision)),decision,remarks,attachmentIds:selected,evidenceReferences:references.split("\n").map(x=>x.trim()).filter(Boolean)};pendingDecision.current=input;
   await manualEvidence.verify(requestId,input);if(e!==epoch.current)return;
   pendingDecision.current=null;setDecisionUncertain(false);setDecisionMessage("Verification decision recorded successfully. This does not confirm an attachment upload.");setRemarks("");setReferences("");setSelected([]);
   try{await uploader.current?.refresh();}catch{setError("Decision accepted; saved history could not be refreshed. Refresh history without submitting another decision.");}
  }catch(caught){if(e===epoch.current){const rejected=definitelyRejected(caught);if(rejected)pendingDecision.current=null;setDecisionUncertain(!rejected);setDecisionMessage("");setError(caught instanceof Error?caught.message:"Verification response unconfirmed.");}}
  finally{decisionFlight.current=false;if(e===epoch.current)setDecisionBusy(false);}
 }
 const latest=view?.history.filter(x=>x.concern===concern).at(-1);
 return <section className={styles.panel} aria-label="Manual DS evidence verification"><h3>Current Assessment</h3><h4>Manual DS evidence verification</h4><p>Separate from the original automated screening. The responsible DS may attach evidence personally and record a purpose-specific decision. No decision changes the saved engine result or constitutes legal approval.</p>
 <div ref={feedback} tabIndex={-1}><UploadFeedback state={upload}/>{decisionMessage&&<p role="status" aria-live="polite">{decisionMessage}</p>}{error&&<p role="alert">{error}</p>}</div>
 {!view&&!error&&<p role="status">Loading evidence history…</p>}
 {!view&&error&&<button disabled={busy} onClick={()=>void refresh()}>Retry loading evidence history</button>}
 {view&&<><div className={styles.toolbar}><button disabled={busy} onClick={()=>void refresh()}>Refresh attachments and verification history</button><span>{view.canVerify?"Responsible DS workspace":"Read-only verification history"}</span></div>
 {view.canVerify&&<fieldset disabled={busy||unresolved}><legend>Review a governance concern</legend><label>Concern<select value={concern} onChange={e=>{setConcern(e.target.value);setSelected([]);uploader.current?.select(file.current?.files?.[0]??null,e.target.value);}}>{investigationConcerns.map(([key,label])=><option key={key} value={key}>{label}</option>)}</select></label><p>Current manual status: <strong>{statusLabel(latest?.status??"Unverified")}</strong> · revision {latest?.revision??0}</p>
 <label>Attach evidence (PDF, PNG, JPEG or UTF-8 text, maximum 5 MiB)<input ref={file} type="file" accept="application/pdf,image/png,image/jpeg,text/plain" onChange={e=>uploader.current?.select(e.target.files?.[0]??null,concern)}/></label><button type="button" disabled={upload.phase==="empty"||upload.phase==="saved"||upload.phase==="loaded"} onClick={()=>{if(!decisionFlight.current)void uploader.current?.start(concern);}}>Save evidence attachment</button>
 <label>Verification decision<select value={decision} onChange={e=>setDecision(e.target.value)}><option value="Verified">Verified</option><option value="Rejected">Rejected</option><option value="MoreInformationRequired">More Information Required</option></select></label><label>Verification remarks (required, 20–2000 characters)<textarea minLength={20} maxLength={2000} value={remarks} onChange={e=>setRemarks(e.target.value)}/></label><label>Evidence/document references (one per line, maximum 10)<textarea value={references} onChange={e=>setReferences(e.target.value)}/></label><div>Attachments reviewed{view.attachments.filter(a=>a.concern===concern).map(a=><label key={a.id}><input type="checkbox" checked={selected.includes(a.id)} onChange={e=>setSelected(old=>e.target.checked?[...old,a.id]:old.filter(x=>x!==a.id))}/>{a.fileName}</label>)}</div><button type="button" disabled={remarks.trim().length<20||(decision==="Verified"&&!selected.length&&!references.trim())} onClick={()=>void saveDecision()}>Save verification decision</button></fieldset>}
 {upload.phase==="uncertain"&&<div role="status"><p>Refresh saved attachments first. Never create a new key automatically for an unconfirmed upload.</p><button disabled={busy} onClick={()=>void refresh()}>Refresh attachment history</button><button disabled={busy||!upload.canReplay} onClick={()=>void uploader.current?.replay()}>Replay the same upload request</button>{!upload.canReplay&&<p>The original file payload is unavailable after reload. Exact replay cannot be performed here; investigate saved history before another upload.</p>}</div>}
 {decisionUncertain&&<p role="status">The unconfirmed verification decision retains its exact key and payload. Refresh history, then <button disabled={busy} onClick={()=>void saveDecision()}>Replay the same verification decision</button>. This is not an evidence upload.</p>}
 <section className={historyStyles.section}><h4>Evidence attachments ({view.attachments.length})</h4><p>Only saved attachments returned by the server appear here. A selected file or a verification decision does not establish an upload.</p>{!view.attachments.length&&<p>No saved evidence attachments exist for this screening request.</p>}<ul>{view.attachments.map(a=><li key={a.id}><strong>{a.fileName}</strong> · {a.bytes.toLocaleString()} bytes · {statusLabel(a.concern)} · {sriLankaTime(a.createdAtUtc)}<button onClick={()=>void manualEvidence.download(requestId,a).catch(caught=>setError(caught.message))}>Download {a.fileName}</button></li>)}</ul></section>
 <section className={historyStyles.section}><h4>Verification history ({view.history.length})</h4>{view.history.map(h=><article key={h.id}><div className={historyStyles.concerns}><strong>{statusLabel(h.concern)}</strong><StatusBadge status={h.status}/></div><p className={historyStyles.meta}>revision {h.revision} · {sriLankaTime(h.createdAtUtc)}</p><p>{h.remarks}</p><p>Attachments referenced: {h.attachmentIds.length}; document references: {h.evidenceReferences.length}.</p>{h.evidenceReferences.map((r,i)=><p key={i}>{r}</p>)}<p className={historyStyles.meta}>Recorded by responsible DS</p></article>)}{!view.history.length&&<p>No manual verification has been recorded.</p>}</section><details className={historyStyles.technical}><summary>Technical details</summary><p>Request: {requestId}</p>{view.attachments.map(a=><p key={a.id}>Attachment {a.id} · uploader {a.actorId} · SHA256 {a.sha256}</p>)}{view.history.map(h=><p key={h.id}>Event {h.id} · recorded officer {h.actorId} · {statusLabel(h.previousStatus)} → {statusLabel(h.status)} · SHA256 {h.eventSha256}</p>)}</details></>}
 {view&&<PreviousEarlyEvidence key={`${session.actor}:${session.generation}:${requestId}`} requestId={requestId}/>}
 </section>;
}
