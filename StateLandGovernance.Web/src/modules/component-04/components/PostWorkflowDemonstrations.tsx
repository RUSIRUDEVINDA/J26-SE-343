"use client";
import {MeasuredWorkflowFeatures} from "./MeasuredWorkflowFeatures";

import {ProgressiveSteps,ProgressiveStep} from "./ProgressiveSteps";
import {sriLankaTime} from "../services/presentation";
import Link from "next/link";
import {NamedWorkflowPreview} from "./NamedWorkflowPreview";

import {useEffect,useRef,useState} from "react";

import {analysisMessage,createPostWorkflowScope,postWorkflowApi,type DemoChoice,type DemoReport} from "../services/postWorkflowDemonstrations";

import styles from "./KiwulaAssessmentWorkspace.module.css";

import {PendingIndicator} from "./PendingIndicator";



export function PostWorkflowDemonstrations({id,initialScenario=""}:{id?:string;initialScenario?:string}){

 const [choices,setChoices]=useState<DemoChoice[]>([]),[history,setHistory]=useState<DemoReport[]>([]);

 const [selection,setSelection]=useState(initialScenario),[report,setReport]=useState<DemoReport|null>(null),[error,setError]=useState("");

 const [loading,setLoading]=useState(true),[busy,setBusy]=useState(false),[ready,setReady]=useState(false);const lock=useRef(false);

 const scope=useRef(createPostWorkflowScope()),refreshVersion=useRef(0);

 async function refresh(){const captured=scope.current.capture();const version=++refreshVersion.current;

  const current=()=>scope.current.current(captured.epoch)&&version===refreshVersion.current;

  setLoading(true);setReady(false);setError("");setReport(null);setHistory([]);setChoices([]);try{

  const [catalogue,saved]=await Promise.all([postWorkflowApi.catalogue(captured.signal),postWorkflowApi.history(captured.signal)]);

  const detail=id?await postWorkflowApi.get(id,captured.signal):saved.find(r=>r.scenario===selection)??null;

  if(!current())return;setChoices(catalogue);setHistory(saved);setReport(detail);setReady(true);

 }catch(e){if(current())setError(e instanceof Error?e.message:"Retrieval failed.");}finally{if(current())setLoading(false);}}

 useEffect(()=>{const sessionScope=scope.current;let active=true;const changed=()=>{sessionScope.invalidate();lock.current=false;setBusy(false);setReady(false);setChoices([]);setHistory([]);setReport(null);setSelection("");void refresh();};

  queueMicrotask(()=>{if(active)void refresh();});window.addEventListener("development-session-changed",changed);

  return()=>{active=false;sessionScope.invalidate();window.removeEventListener("development-session-changed",changed);};/* Retrieval only: never invoke inference during mount/refresh. */

 // eslint-disable-next-line react-hooks/exhaustive-deps
 },[id]);

 async function run(retry=false){if(lock.current||!selection||!ready||!choices.find(c=>c.scenario===selection)?.canRun)return;

  const captured=scope.current.capture();lock.current=true;setBusy(true);setError("");try{

  const saved=await postWorkflowApi.run(selection,retry,report?.attempt??0,captured.signal);

  if(!scope.current.current(captured.epoch))return;setReport(saved);const rows=await postWorkflowApi.history(captured.signal);

  if(scope.current.current(captured.epoch))setHistory(rows);

 }catch(e){if(scope.current.current(captured.epoch)){setReady(false);setError(`${e instanceof Error?e.message:"Submission failed."} Outcome may be uncertain. Refresh saved history before any further action.`);}}

 finally{if(scope.current.current(captured.epoch)){lock.current=false;setBusy(false);}}}

 const isKiwula=selection==="kiwula-kale-example-v1"||report?.scenario==="kiwula-kale-example-v1";
 const preview=choices.find(c=>c.scenario===selection);const projection=report?.projection??preview?.projection;const a=report?.assessment;

 if(!id&&!initialScenario&&!selection)return <NamedWorkflowPreview><details className={styles.card}><summary>Previous workflow analyses / saved reports</summary>{loading&&<PendingIndicator>Loading previous analyses…</PendingIndicator>}{error&&<p role="alert">{error}</p>}{history.map(r=><p key={r.id}><Link href={`/component-04/post-workflow/reports/${r.id}`}>{choices.find(c=>c.scenario===r.scenario)?.name??r.scenario} · {r.state} · {new Date(r.createdAtUtc).toLocaleString()}</Link></p>)}<button disabled={loading} onClick={()=>void refresh()}>Refresh History</button></details></NamedWorkflowPreview>;
 return <div className={styles.workspace}><header className={styles.heading}><h1>Post-Workflow Anomaly</h1>

 <p>Illustrative workflow histories, not official decisions or actual C3 executions. Source and version are retained in each report.</p>

 <p>Isolation Forest scores are not probabilities. An anomaly does not prove corruption or establish its cause.</p></header>

 {loading&&<PendingIndicator>Retrieving workflow histories and saved analyses…</PendingIndicator>}{error&&<p role="alert">{error}</p>}

 <button type="button" disabled={busy} onClick={()=>void refresh()}>Refresh saved retrieval</button>

 {!id&&!selection&&<section className={styles.card}><h2>Completed workflow examples</h2><p>Choose an example to inspect its events and saved analysis. Start Analysis opens a preview without running the model.</p><div className={styles.scroll}><table><thead><tr><th>Proposal / workflow</th><th>History version</th><th>Events</th><th>Analysis status</th><th>Action</th></tr></thead><tbody>{[...choices].sort((a,b)=>Number(b.scenario==="kiwula-kale-example-v1")-Number(a.scenario==="kiwula-kale-example-v1")).map(c=><tr key={c.scenario}><th scope="row">{c.scenario==="kiwula-kale-example-v1"?"Project_Proposal_Kiwula Kale":c.name}</th><td>{c.version}</td><td>{c.projection.events.length}</td><td>{history.find(r=>r.scenario===c.scenario)?.state??"Not analysed"}</td><td><button disabled={loading||busy||!ready} onClick={()=>{setSelection(c.scenario);setReport(history.find(r=>r.scenario===c.scenario)??null);}}>Start Analysis</button></td></tr>)}</tbody></table></div>{ready&&choices.length===0&&<p>No workflow examples available for this account.</p>}</section>}

 {!id&&!!selection&&<section className={styles.card}><button disabled={busy} onClick={()=>{setSelection("");setReport(null);}}>Back to workflows</button><h2>Preview the selected workflow</h2><select aria-label="Demonstration workflow" value={selection} disabled={busy||loading} onChange={e=>{setSelection(e.target.value);setReport(history.find(r=>r.scenario===e.target.value)??null);}}><option value="">Choose a history</option>{choices.map(c=><option key={c.scenario} value={c.scenario}>{c.name}</option>)}</select>

 <p>{preview?.description??"Select a history to preview its server-controlled timeline."}</p><p>Scenario version: {preview?.version??"No history selected"} .</p>

 {preview&&!preview.canRun&&<p role="status">Preview available; assessment writes are not activated.</p>}{!report&&<button disabled={!preview?.canRun||!ready||loading||busy} onClick={()=>void run()}>Run New Analysis</button>}{report?.state==="Saved"&&<button disabled title="The saved scenario/version is idempotent; a new model run is not supported.">Run New Analysis — unavailable for this saved version</button>}{report?.state==="Saved"&&<p>A saved analysis is available below. The same creator/scenario/version can only retrieve this result; it cannot invoke the model again. <Link href={`/component-04/post-workflow/reports/${report.id}`}>View Saved Analysis</Link>. No new inference occurs on replay.</p>}{report?.state==="Pending"&&<p role="status">This analysis is Pending. Refresh saved retrieval; do not submit another run.</p>}

 {report?.state==="Failed"&&<button disabled={busy} onClick={()=>void run(true)}>Explicitly retry failed analysis</button>}</section>}

 {busy&&<PendingIndicator>Waiting for the actual model result and saved assessment…</PendingIndicator>}

 {projection&&<section className={styles.card}><h2>Timeline and input provenance</h2><p>Timezone: {projection.eventTimeZone}. Model timestamps preserve the original naive wall-clock strings.</p><p>Related proposal: {selection==="kiwula-kale-example-v1"||report?.scenario==="kiwula-kale-example-v1"?"Project_Proposal_Kiwula Kale · kiulakale-demonstration-adaptation-v1":"See source history"}.</p><ProgressiveSteps label="Saved workflow events">{projection.events.map(e=><ProgressiveStep key={e.eventSeq} title={e.activity} description={<>{isKiwula&&(e.institutionCode==="DS"||e.institutionCode==="EXAMPLE-DS")?"Mahakumbukkadawala Divisional Secretariat (prepared role)":isKiwula&&e.institutionCode==="COMMISSIONER"?"Land Commissioner General?s Department (prepared role)":e.institutionCode??"Institution unavailable"} ? {sriLankaTime(e.timestamp)}</>} status={e.eventOutcome??"No outcome recorded"} tone="neutral"><p>Resource: {e.resource}</p></ProgressiveStep>)}</ProgressiveSteps><details><summary>Conditional institutional routing context</summary><p>North Western Provincial Land Commissioner’s Department, North Western Provincial Environmental Authority, Department of Agrarian Development, relevant Pradeshiya Sabha, Irrigation Department and Department of Agriculture are possible review bodies requiring jurisdiction, applicability and policy confirmation. They are not all mandatory and are not asserted to have participated in the saved seven-event history. The saved regional branch remains unconfirmed.</p></details><p>DS Final Notification is a separate terminal model event; Commissioner Decision does not substitute for it.</p><details><summary>Technical input provenance</summary><p>Projection SHA-256: {projection.inputSha256}</p><pre className={styles.json}>{JSON.stringify(projection.provenance,null,2)}</pre></details><h3>Deterministic validation</h3><p>Contiguous ordering, timestamp chronology and terminal event eligibility are validated by the existing projection contract. These checks are separate from ML output; no legal timing clearance is inferred.</p></section>}

 {report&&<section className={styles.card}><h2>{analysisMessage(report)}</h2><p>{report.failure}</p><Link href={`/component-04/post-workflow/reports/${report.id}`}>Open saved report</Link><p>Created {new Date(report.createdAtUtc).toLocaleString()}; source {report.version}.</p><details><summary>Saved creator association</summary><p>Authenticated creator: {report.creatorActorId}; saved report {report.id}.</p></details><h3>Analysis attempts</h3><ProgressiveSteps label="Actual analysis attempts">{report.attempts.map(t=><ProgressiveStep key={t.number} title={`Attempt ${t.number}`} status={t.state} description={t.finishedAtUtc?sriLankaTime(t.finishedAtUtc):"Not finished"}>{t.failure&&<p>{t.failure}</p>}</ProgressiveStep>)}</ProgressiveSteps>

 {a&&<><MeasuredWorkflowFeatures result={a} timeline={projection}/><details><summary>Exact saved assessment</summary><pre className={styles.json}>{JSON.stringify(a,null,2)}</pre></details></>}</section>}

 <section className={styles.card}><h2>Accessible workflow analyses</h2>{ready&&history.length===0&&<p>No accessible saved analyses.</p>}{history.map(r=><p key={r.id}><Link href={`/component-04/post-workflow/reports/${r.id}`}>{choices.find(c=>c.scenario===r.scenario)?.name??r.scenario}: {r.state}</Link></p>)}</section>

 <p><Link href="/component-04/development">Other development input tools</Link>. Raw prediction tools do not create this persisted assessment record.</p></div>;

}

