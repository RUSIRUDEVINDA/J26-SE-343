"use client";
import Link from "next/link";
import {useEffect,useRef,useState} from "react";
import {regulatoryFollowups,type PreviousFollowup} from "../services/regulatoryFollowups";
import {sriLankaTime} from "../services/presentation";
import {readDevelopmentSession} from "@/lib/developmentSession";
import {getRegulatoryReport,type RegulatoryFinding} from "../services/independentRegulatoryReports";
import {RegulatoryFollowupEntries,FollowupTechnicalDetails} from "./RegulatoryFollowupHistory";
import styles from "./AssessmentHistory.module.css";

type HistoryState={evaluationId:string;actorId:string;items:PreviousFollowup[];next:string|null};
export function groupPreviousFollowups(items:PreviousFollowup[]){
 const groups=new Map<string,{timestamp:string;events:PreviousFollowup["event"][]}>();
 for(const item of items){let group=groups.get(item.evaluationId);if(!group){group={timestamp:item.evaluationTimestamp,events:[]};groups.set(item.evaluationId,group);}if(!group.events.some(x=>x.id===item.event.id))group.events.push(item.event);}
 return [...groups].map(([id,value])=>({id,...value}));
}
export function RegulatoryHistoryCard({group,findings,loading=false}:{group:ReturnType<typeof groupPreviousFollowups>[number];findings?:RegulatoryFinding[]|null;loading?:boolean}){
 return <article className={styles.card}><header className={styles.header}><h4>{sriLankaTime(group.timestamp)}</h4><Link href={`/component-04/regulatory-assessments/${group.id}`}>Open original saved report</Link></header><p className={styles.counts}>Follow-up history: {group.events.length}</p><RegulatoryFollowupEntries events={group.events} findings={findings} loading={loading}/><FollowupTechnicalDetails evaluationId={group.id} events={group.events}/></article>;
}
function OriginalRegulatoryGroup({group}:{group:ReturnType<typeof groupPreviousFollowups>[number]}){
 const [findings,setFindings]=useState<RegulatoryFinding[]|null>(null),[loading,setLoading]=useState(true);
 useEffect(()=>{let active=true;const abort=new AbortController();void getRegulatoryReport(group.id,abort.signal).then(report=>{if(active)setFindings(report.findings);}).catch(()=>{/* Keep the authorized history; never infer missing original finding metadata. */}).finally(()=>{if(active)setLoading(false);});return()=>{active=false;abort.abort();};},[group.id]);
 return <RegulatoryHistoryCard group={group} findings={findings} loading={loading}/>;
}
export function PreviousRegulatoryFollowups({evaluationId}:{evaluationId:string}){
 const [state,setState]=useState<HistoryState|null>(null),[loading,setLoading]=useState(false),[error,setError]=useState("");
 const epoch=useRef(0),abort=useRef<AbortController|null>(null),flight=useRef(false);
 const actorId=readDevelopmentSession()?.user.actorId;
 const current=state?.evaluationId===evaluationId&&state.actorId===actorId?state:null;
 async function load(reset:boolean){if(!reset&&flight.current)return;abort.current?.abort();const controller=new AbortController();abort.current=controller;const version=++epoch.current;flight.current=true;setLoading(true);setError("");
  const actor=readDevelopmentSession()?.user.actorId;if(reset)setState(null);
  try{if(!actor)throw new Error("Sign in again.");const page=await regulatoryFollowups.previous(evaluationId,reset?null:current?.next??null,controller.signal);
   if(version!==epoch.current||actor!==readDevelopmentSession()?.user.actorId)return;
   setState(old=>({evaluationId,actorId:actor,items:[...(!reset&&old?.evaluationId===evaluationId&&old.actorId===actor?old.items:[]),...page.items],next:page.nextCursor}));
  }catch(e){if(version===epoch.current&&!controller.signal.aborted){setState(null);setError(e instanceof Error?e.message:"History unavailable.");}}
  finally{if(version===epoch.current){flight.current=false;setLoading(false);}}
 }
 const latestLoad=useRef(load);useEffect(()=>{latestLoad.current=load;});
 useEffect(()=>{let active=true;const cancel=()=>{epoch.current++;abort.current?.abort();};const refresh=()=>{if(active)void latestLoad.current(true);};queueMicrotask(refresh);window.addEventListener("development-session-changed",refresh);return()=>{active=false;cancel();window.removeEventListener("development-session-changed",refresh);};},[evaluationId]);
 return <section className={styles.section} aria-label="Previous Regulatory Follow-ups"><h3>Previous Regulatory Follow-ups</h3><p className={styles.intro}>Previous assessments are shown for reference. Changes can only be made to the current assessment.</p>
  <button type="button" disabled={loading} onClick={()=>void load(true)}>Refresh previous follow-ups</button>
  {loading&&<p role="status">Loading authorized previous follow-ups…</p>}{error&&<p role="alert">{error}</p>}
  {current&&current.items.length===0&&!loading&&<p>No authorized previous follow-ups were found for this proposal.</p>}
  {current&&groupPreviousFollowups(current.items).map(group=><OriginalRegulatoryGroup key={group.id} group={group}/>)}
  {current?.next&&<button type="button" disabled={loading} onClick={()=>void load(false)}>Load more previous follow-ups</button>}
 </section>;
}
