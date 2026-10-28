"use client";
import {useEffect,useState} from "react";
import {investigationEvidence,type InvestigationEvidence} from "../services/investigationEvidence";
import {useOfficerSession} from "./OfficerFlowSession";
export function InvestigationCompletion({investigationId}:{investigationId:string}){
 const {actor,epoch,generation}=useOfficerSession();const [state,setState]=useState<{id:string;actor:string|null;value:InvestigationEvidence}|null>(null),[error,setError]=useState("");
 useEffect(()=>{let active=true;const version=epoch.current;void investigationEvidence.read(investigationId).then(value=>{if(active&&version===epoch.current)setState({id:investigationId,actor,value});}).catch(()=>{});return()=>{active=false;};},[investigationId,actor,epoch,generation]);
 if(state?.id!==investigationId||state.actor!==actor||!state.value.finalization)return null;
 const {finalization,attachments}=state.value;
 async function download(file:typeof attachments[number]){try{await investigationEvidence.download(investigationId,file);}catch(e){setError(e instanceof Error?e.message:"Download unavailable.");}}
 return <section aria-label="Persisted manual investigation completion"><h4>Manual investigation findings completed/verified by the responsible officer</h4><p>Recorded {new Date(finalization.createdAtUtc).toLocaleString()}. This does not establish allegation truth, automated compliance, legal approval or completion of later evidence review.</p><details><summary>Pre-screening evidence ({attachments.length})</summary><ul>{attachments.map(a=><li key={a.id}>{a.concern}: {a.fileName} <button onClick={()=>void download(a)}>Download</button></li>)}</ul></details>{error&&<p role="alert">{error}</p>}</section>;
}
