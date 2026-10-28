"use client";
import {useEffect,useRef,useState} from "react";
import {manualEvidence,type PreviousEvidence} from "../services/manualEvidence";
import {useOfficerSession} from "./OfficerFlowSession";
import {sriLankaTime,statusLabel} from "../services/presentation";
import {StatusBadge} from "./ProgressiveSteps";
import styles from "./AssessmentHistory.module.css";

export function HistoricalEvidenceGroup({item,onDownload}:{item:PreviousEvidence;onDownload:(item:PreviousEvidence,attachment:PreviousEvidence["evidence"]["attachments"][number])=>void}){
 const evidence={...item.evidence,attachments:item.evidence.attachments.filter(a=>a.requestId===item.requestId),history:item.evidence.history.filter(h=>h.requestId===item.requestId)};
 const concerns=[...new Set([...evidence.attachments.map(x=>x.concern),...evidence.history.map(x=>x.concern)])];
 return <article className={styles.card} aria-label={`Previous assessment ${sriLankaTime(item.createdAtUtc)}`}><header className={styles.header}><h4>Previous assessment · {sriLankaTime(item.createdAtUtc)}</h4><a href={`/component-04/officer-assessments/${encodeURIComponent(item.requestId)}`}>Open original saved report</a></header>
 <p className={styles.meta}>Assessment {item.assessmentId.slice(0,8)} · evidence belongs only to this saved request.</p>
 <div className={styles.concerns}>{concerns.map(concern=>{const latest=evidence.history.filter(h=>h.concern===concern).reduce<typeof evidence.history[number]|undefined>((a,b)=>!a||b.revision>a.revision?b:a,undefined);return <span key={concern}>{statusLabel(concern)} <StatusBadge status={latest?.status??"Unverified"}/></span>;})}</div>
 <p className={styles.counts}><span>Attachments: {evidence.attachments.length}</span><span>Verification history: {evidence.history.length}</span></p>
 <h5>Evidence attachments</h5><ul className={styles.files}>{evidence.attachments.map(a=><li key={a.id}><strong>{a.fileName}</strong> · {a.bytes.toLocaleString()} bytes · {statusLabel(a.concern)}<span className={styles.meta}>Uploaded by responsible DS · {sriLankaTime(a.createdAtUtc)}</span><button type="button" onClick={()=>onDownload(item,a)}>Download {a.fileName}</button></li>)}</ul>{!evidence.attachments.length&&<p>No attachments saved on this assessment.</p>}
 <h5>Verification history</h5>{evidence.history.map(h=><article className={styles.entry} key={h.id}><div className={styles.concerns}><strong>{statusLabel(h.concern)}</strong><StatusBadge status={h.status}/><span className={styles.meta}>revision {h.revision}</span></div><p>{h.remarks}</p>{h.evidenceReferences.map((r,i)=><p key={i}>Document reference: {r}</p>)}<p className={styles.meta}>Attachments reviewed: {h.attachmentIds.length} · Recorded by responsible DS · {sriLankaTime(h.createdAtUtc)}</p></article>)}{!evidence.history.length&&<p>No verification decisions saved on this assessment.</p>}
 <details className={styles.technical}><summary>Technical details</summary><p>Request: {item.requestId}</p><p>Assessment: {item.assessmentId}</p>{evidence.attachments.map(a=><p key={a.id}>Attachment {a.id} · uploader {a.actorId} · SHA256 {a.sha256}</p>)}{evidence.history.map(h=><p key={h.id}>Event {h.id} · recorded officer {h.actorId} · {statusLabel(h.previousStatus)} → {statusLabel(h.status)} · revision {h.revision} · attachment IDs: {h.attachmentIds.join(", ")||"None"} · SHA256 {h.eventSha256}</p>)}</details></article>;
}
export function PreviousEarlyEvidence({requestId}:{requestId:string}){
 const session=useOfficerSession(),epoch=session.epoch;
 const [state,setState]=useState<{scope:string;items:PreviousEvidence[];cursor:string|null;busy:boolean;error:string;loaded:boolean}>({scope:"",items:[],cursor:null,busy:false,error:"",loaded:false});
 const flight=useRef(false),scope=`${session.actor}:${session.generation}:${requestId}`;
 useEffect(()=>{let active=true;const e=epoch.current;flight.current=true;
  queueMicrotask(()=>setState({scope,items:[],cursor:null,busy:true,error:"",loaded:false}));
  void manualEvidence.previous(requestId).then(page=>{if(active&&e===epoch.current)setState({scope,items:page.items,cursor:page.nextCursor,busy:false,error:"",loaded:true});}).catch(caught=>{if(active&&e===epoch.current)setState({scope,items:[],cursor:null,busy:false,error:caught.message,loaded:false});}).finally(()=>{if(active)flight.current=false;});
  return()=>{active=false;};
 },[requestId,scope,epoch]);
 async function more(){if(flight.current)return;flight.current=true;const e=epoch.current;setState(old=>({...old,busy:true,error:""}));try{const page=await manualEvidence.previous(requestId,state.cursor??undefined);if(e===epoch.current)setState(old=>({...old,items:[...old.items,...page.items.filter(item=>!old.items.some(x=>x.requestId===item.requestId))],cursor:page.nextCursor,busy:false,error:"",loaded:true}));}catch(caught){if(e===epoch.current)setState(old=>({...old,busy:false,error:caught instanceof Error?caught.message:"Could not load historical evidence."}));}finally{if(e===epoch.current)flight.current=false;}}
 async function download(item:PreviousEvidence,attachment:PreviousEvidence["evidence"]["attachments"][number]){const e=epoch.current;try{await manualEvidence.download(item.requestId,attachment);}catch(caught){if(e===epoch.current)setState(old=>({...old,error:caught instanceof Error?caught.message:"Download unavailable."}));}}
 const current=state.scope===scope;
 return <section className={styles.section} aria-label="Previous Assessments"><h3>Previous Assessments</h3><p className={styles.intro}>Previous assessments are shown for reference. Changes can only be made to the current assessment.</p>{(!current||state.busy)&&<p role="status">Loading previous evidence…</p>}{current&&<>{state.error&&<p role="alert">{state.error}</p>}{state.items.map(item=><HistoricalEvidenceGroup key={item.requestId} item={item} onDownload={(group,attachment)=>void download(group,attachment)}/>)}{state.loaded&&!state.items.length&&<p>No authorized earlier evidence or verifications exist for this proposal.</p>}{(state.cursor||!state.loaded)&&<button disabled={state.busy} type="button" onClick={()=>void more()}>{state.loaded?"Load more previous assessments":"Retry previous evidence"}</button>}</>}</section>;
}
