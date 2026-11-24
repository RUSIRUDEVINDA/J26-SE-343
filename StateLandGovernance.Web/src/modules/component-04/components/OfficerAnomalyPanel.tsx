"use client";
import {useEffect,useRef,useState} from "react";
import {StatusBadge} from "./ProgressiveSteps";
import {sriLankaTime} from "../services/presentation";
import Link from "next/link";
import {officerApi,pendingOfficerKey,clearOfficerKey,namedScenario,type RequestReport} from "../services/officerFlows";
import {useOfficerSession} from "./OfficerFlowSession";
import {OfficerAssessmentResult} from "./OfficerAssessmentResult";
import {PendingIndicator} from "./PendingIndicator";
import styles from "./KiwulaAssessmentWorkspace.module.css";
export function OfficerAnomalyPanel(){
 const {actor,epoch,generation}=useOfficerSession();const [history,setHistory]=useState<RequestReport[]>([]),[report,setReport]=useState<RequestReport|null>(null),[ready,setReady]=useState(false),[busy,setBusy]=useState(false),[error,setError]=useState("");const flight=useRef(false);
 async function refresh(){const v=epoch.current;try{const [catalogue,rows]=await Promise.all([officerApi<{canRun:boolean}>("officer-assessments/catalogue"),officerApi<RequestReport[]>("officer-assessments")]);if(v!==epoch.current)return;setReady(catalogue.canRun);setHistory(rows.filter(r=>r.kind==="anomaly"));}catch(e){if(v===epoch.current)setError(e instanceof Error?e.message:"Unavailable");}}
 useEffect(()=>{let active=true;queueMicrotask(()=>{if(!active)return;setHistory([]);setReport(null);setReady(false);setBusy(false);setError("");flight.current=false;if(actor)void refresh();});return()=>{active=false;};/* session epoch rejects in-flight responses */ // eslint-disable-next-line react-hooks/exhaustive-deps
 },[actor,generation]);
 const uncertain=history.some(r=>r.state==="Pending"||r.state==="Unknown");
 async function run(){if(flight.current||!ready||uncertain)return;flight.current=true;setBusy(true);setError("");const v=epoch.current;try{const key=pendingOfficerKey(actor,"anomaly",sessionStorage);const saved=await officerApi<RequestReport>("officer-assessments/anomaly",{scenario:namedScenario,submissionKey:key});if(v!==epoch.current)return;setReport(saved);if(saved.state==="Saved")clearOfficerKey(actor,"anomaly");await refresh();}catch(e){if(v===epoch.current)setError((e instanceof Error?e.message:"Unavailable")+" The same request key is retained. Refresh history before any further analysis.");}finally{if(v===epoch.current){flight.current=false;setBusy(false);}}}
 return <>{!actor&&<p>Sign in to view accessible analyses. Running an analysis requires separate officer authorization. <Link href="/sign-in">Sign in</Link></p>}<div className={styles.toolbar}><button className={styles.primary} disabled={!ready||busy||uncertain} onClick={()=>void run()}>Run Anomaly Analysis</button></div>
 <p className={styles.secondary}>The model assesses the entire sequence. A deliberate new run saves a separate analysis.</p>
 {busy&&<PendingIndicator>Analysing the prepared workflow…</PendingIndicator>}{error&&<p role="alert">{error}</p>}{uncertain&&<p role="status">A request is Pending or Unknown. No new inference is permitted until its outcome is investigated.</p>}{report&&<OfficerAssessmentResult report={report}/>}
 <details><summary>Analysis history ({history.length})</summary>{history.length===0?<p>No saved analyses returned for this account.</p>:<div className={styles.scroll}><table><thead><tr><th>Workflow</th><th>Date / time (Sri Lanka)</th><th>Status</th><th>Report</th></tr></thead><tbody>{history.map(r=><tr key={r.id}><td>Kiwula Kale — Named route v2</td><td>{sriLankaTime(r.createdAtUtc)}</td><td><StatusBadge status={r.state}/></td><td><Link href={`/component-04/officer-assessments/${r.id}`}>View Report</Link></td></tr>)}</tbody></table></div>}</details></>;
}
