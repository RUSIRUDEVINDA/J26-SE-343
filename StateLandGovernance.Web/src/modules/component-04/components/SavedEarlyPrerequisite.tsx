"use client";
import {useEffect,useRef,useState} from "react";
import {officerApi,type RequestReport} from "../services/officerFlows";
import {readSequence} from "../services/officerSequence";
import {useOfficerSession} from "./OfficerFlowSession";
export function SavedEarlyPrerequisite({value,onChange,disabled=false}:{value:string;onChange:(id:string,ready:boolean)=>void;disabled?:boolean}){
 const {actor,epoch,generation}=useOfficerSession();const [snapshot,setSnapshot]=useState<{actor:string;rows:RequestReport[]}>({actor:"",rows:[]}),[error,setError]=useState("");
 useEffect(()=>{let active=true;const version=epoch.current;if(actor)void officerApi<RequestReport[]>("officer-assessments").then(r=>{if(active&&version===epoch.current)setSnapshot({actor,rows:r.filter(x=>x.kind==="early"&&x.state==="Saved")});}).catch(e=>{if(active&&version===epoch.current)setError(e.message);});return()=>{active=false;};},[actor,epoch,generation]);
 const choice=useRef(0);const rows=snapshot.actor===actor?snapshot.rows:[];
 async function choose(id:string){const selected=++choice.current;const version=epoch.current;onChange(id,false);setError("");if(!id)return;try{const state=await readSequence(id);if(version!==epoch.current||selected!==choice.current)return;onChange(id,state.canRunRegulatory);if(!state.canRunRegulatory)setError(state.holds.join(" "));}catch(e){if(version===epoch.current&&selected===choice.current)setError(e instanceof Error?e.message:"Prerequisite unavailable.");}}
 return <div><label>Saved finalized Early Governance screening<select value={value} disabled={disabled} onChange={e=>void choose(e.target.value)}><option value="">Select explicitly — no latest-result fallback</option>{rows.map(r=><option key={r.id} value={r.id}>{new Date(r.createdAtUtc).toLocaleString()} — {r.id}</option>)}</select></label>{error&&<p role="alert">{error}</p>}</div>;
}
