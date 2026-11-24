"use client";
import {useEffect,useState} from "react";
import {officerApi,type Investigation,type RequestReport} from "../services/officerFlows";
import {investigationEvidence,type InvestigationEvidence} from "../services/investigationEvidence";
import {useOfficerSession} from "./OfficerFlowSession";
import {statusLabel,sriLankaTime} from "../services/presentation";
import {manualEvidence,type EvidenceView} from "../services/manualEvidence";

export function CurrentEarlyFindings({report,investigation,evidence,verification=null}:{report:RequestReport;investigation:Investigation;evidence:InvestigationEvidence;verification?:EvidenceView|null}){
 const result=report.result;
 return <section aria-label="Current finalized investigation result">
  <p>Investigation: {investigation.id}</p><p>Screening assessment: {report.earlyAssessmentId??"Not confirmed"}</p>
  <p>Investigation status: <strong>{evidence.finalization?"Finalized by the responsible DS":"Not finalized"}</strong></p>
  {evidence.finalization&&<p>Finalized by {evidence.finalization.actorId} at {sriLankaTime(evidence.finalization.createdAtUtc)}.</p>}
  <p>Investigation completion records the officer’s findings. It does not independently verify supporting evidence or grant legal approval. Original screening evidence states are retained below as technical details.</p>
  {result&&"result" in result&&<h4>Overall screening result: {statusLabel(result.result.overallStatus)}</h4>}
  {investigation.findings.map(f=>{const latest=verification?.history.filter(v=>v.concern===f.concern).reduce<typeof verification.history[number]|undefined>((a,b)=>!a||b.revision>a.revision?b:a,undefined);return <article key={f.concern}><h4>{statusLabel(f.concern)}</h4><p>Recorded finding: <strong>{statusLabel(f.choice)}</strong></p><p>Officer conclusion / remarks: {f.remarks||"No remarks recorded"}</p>
   <ul>{evidence.attachments.filter(a=>a.concern===f.concern).map(a=><li key={a.id}>{a.fileName} · reference {a.id} · SHA256 {a.sha256}</li>)}</ul>
   {!evidence.attachments.some(a=>a.concern===f.concern)&&<p>No supporting attachment recorded for this concern.</p>}
   <p>Independent supporting-evidence review: {verification?latest?`${statusLabel(latest.status)} · revision ${latest.revision} · ${latest.remarks}`:"No independent verification recorded":"Verification records unavailable; status not inferred"}.</p>
  </article>;})}
  <p>Independent evidence verification is not established by finalization. Any unresolved required evidence review remains outstanding; screening recommendations remain applicable. Saved reports retain the separate evidence-review record and permitted actions.</p>
  {result&&"result" in result&&<details><summary>Saved screening evidence states and recommendations</summary>{result.result.indicatorResults.map(i=><p key={i.indicatorType}>{statusLabel(i.indicatorType)}: {statusLabel(i.evidenceState)} — {i.message}</p>)}</details>}
 </section>;
}

export function CurrentEarlyResult({report}:{report:RequestReport}){
 const {actor,epoch,generation}=useOfficerSession();
 const [loaded,setLoaded]=useState<{requestId:string;actor:string|null;investigation:Investigation;evidence:InvestigationEvidence;verification:EvidenceView|null}|null>(null),[error,setError]=useState("");
 useEffect(()=>{let active=true;const version=epoch.current;
  if(report.investigationId)void Promise.all([officerApi<Investigation>(`officer-investigations/${encodeURIComponent(report.investigationId)}`),investigationEvidence.read(report.investigationId),manualEvidence.read(report.id).catch(()=>null)]).then(([investigation,evidence,verification])=>{if(active&&version===epoch.current)setLoaded({requestId:report.id,actor,investigation,evidence,verification});}).catch(()=>{if(active&&version===epoch.current)setError("Current investigation details could not be read. Open the saved report or refresh; finalization and independent verification are not inferred.");});
  return()=>{active=false;};
 },[report.id,report.investigationId,actor,epoch,generation]);
 if(loaded?.requestId===report.id&&loaded.actor===actor)return <CurrentEarlyFindings report={report} investigation={loaded.investigation} evidence={loaded.evidence} verification={loaded.verification}/>;
 return <p role={error?"alert":"status"}>{error||"Reading the exact saved investigation and finalization…"}</p>;
}
