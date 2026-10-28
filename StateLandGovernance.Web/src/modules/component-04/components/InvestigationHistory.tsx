"use client";
import {useEffect,useState} from "react";
import {useOfficerSession} from "./OfficerFlowSession";
import {officerApi,type Investigation} from "../services/officerFlows";
import {statusLabel} from "../services/presentation";
import {InvestigationCompletion} from "./InvestigationCompletion";
export function InvestigationHistory(){
 const {actor,epoch,generation}=useOfficerSession();const [rows,setRows]=useState<Investigation[]>([]),[selected,setSelected]=useState<Investigation|null>(null),[error,setError]=useState("");
 useEffect(()=>{let active=true;const version=epoch.current;queueMicrotask(()=>{if(!active)return;setRows([]);setSelected(null);setError("");if(actor)void officerApi<Investigation[]>("officer-investigations").then(x=>{if(active&&version===epoch.current)setRows(x);}).catch(()=>{if(active&&version===epoch.current)setError("Investigation history unavailable to this account.");});});return()=>{active=false;};},[actor,epoch,generation]);
 async function open(id:string){const version=epoch.current;try{const value=await officerApi<Investigation>(`officer-investigations/${encodeURIComponent(id)}`);if(version===epoch.current)setSelected(value);}catch{if(version===epoch.current)setError("Selected investigation is unavailable.");}}
 return <section><h2>Investigation History</h2>{error&&<p role="alert">{error}</p>}<label hidden style={{display:"none"}} htmlFor="historical-investigation">Historical investigation</label><select hidden style={{display:"none"}} id="historical-investigation" value={selected?.id??""} onChange={e=>{if(e.target.value)void open(e.target.value);}}><option value="">Select deliberately</option>{rows.map(r=><option key={r.id} value={r.id}>{r.id} · {r.createdAtUtc}</option>)}</select>{selected&&<article><h3>Saved investigation {selected.id}</h3><p>Proposal {selected.proposalId}. Viewing this record does not select an active workflow.</p><ul>{selected.findings.map(f=><li key={f.concern}>{statusLabel(f.concern)}: {statusLabel(f.choice)}<p>{f.remarks}</p></li>)}</ul><InvestigationCompletion investigationId={selected.id}/></article>}</section>;
}
