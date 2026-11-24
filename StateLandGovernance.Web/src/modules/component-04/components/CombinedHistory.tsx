"use client";
import {useEffect,useState} from "react";
import {listRegulatoryReports,getRegulatoryReport,type RegulatorySummary,type RegulatoryReport} from "../services/independentRegulatoryReports";
import {CombinedFindingsSummary} from "./CombinedFindingsSummary";
import {useOfficerSession} from "./OfficerFlowSession";
export function CombinedHistory(){
 const {actor,epoch,generation}=useOfficerSession();
 const [rows,setRows]=useState<RegulatorySummary[]>([]),[selected,setSelected]=useState<RegulatoryReport|null>(null),[error,setError]=useState("");
 useEffect(()=>{let active=true;const version=epoch.current;queueMicrotask(()=>{if(!active)return;setRows([]);setSelected(null);setError("");if(actor)void listRegulatoryReports().then(x=>{if(active&&version===epoch.current)setRows(x);}).catch(()=>{if(active&&version===epoch.current)setError("Combined report sources unavailable.");});});return()=>{active=false;};},[actor,epoch,generation]);
 async function open(id:string){const version=epoch.current;setError("");try{const value=await getRegulatoryReport(id);if(version===epoch.current)setSelected(value);}catch{if(version===epoch.current)setError("Selected source unavailable.");}}
 return <section><h2>Combined report sources</h2><p>Deliberately compose a read-only report from its saved Early/Regulatory provenance. This does not change the active workflow or create a combined assessment record.</p>{error&&<p role="alert">{error}</p>}<label htmlFor="historical-regulatory-source">Saved regulatory source</label><select id="historical-regulatory-source" value={selected?.metadata.id??""} onChange={e=>{if(e.target.value)void open(e.target.value);}}><option value="">Select explicitly</option>{rows.map(r=><option key={r.id} value={r.id}>{r.id} · {r.evaluationTimestamp}</option>)}</select>{selected?.provenance?.earlyRequestId?<CombinedFindingsSummary proposalId={selected.metadata.proposalId!} selection={{earlyRequestId:selected.provenance.earlyRequestId,regulatoryEvaluationId:selected.metadata.id,conflictReportId:null}} onConflictChange={()=>{}}/>:selected&&<p>This saved evaluation has no persisted Early binding; no combined chain is inferred.</p>}</section>;
}
