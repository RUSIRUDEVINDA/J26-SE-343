"use client";

import Link from "next/link";
import {RegulatoryFollowups} from "./RegulatoryFollowups";
import {RegulatoryFindingExplanation} from "./RegulatoryFindingExplanation";

import {PendingIndicator} from "./PendingIndicator";

import {useEffect,useState} from "react";

import {filterRegulatoryFindings,getRegulatoryReport,listRegulatoryReports,regulatoryCounts,regulatoryError,

 type RegulatoryFinding,type RegulatoryReport,type RegulatorySummary} from "../services/independentRegulatoryReports";

import {ProgressiveSteps,ProgressiveStep,StatusBadge} from "./ProgressiveSteps";
import {statusLabel,sriLankaTime} from "../services/presentation";

import styles from "./GovernanceWorkspace.module.css";

export function IndependentRegulatoryReports({id,embedded=false}:{id?:string;embedded?:boolean}){

 const [rows,setRows]=useState<RegulatorySummary[]>([]),[report,setReport]=useState<RegulatoryReport|null>(null);

 const [loading,setLoading]=useState(true),[error,setError]=useState(""),[retry,setRetry]=useState(0);

 const [query,setQuery]=useState(""),[status,setStatus]=useState("");

 useEffect(()=>{

  const abort=new AbortController();let active=true;queueMicrotask(()=>{if(!active)return;setLoading(true);setError("");setRows([]);setReport(null);setQuery("");setStatus("");

  void (id?getRegulatoryReport(id,abort.signal).then(x=>{if(active)setReport(x);}):listRegulatoryReports(abort.signal).then(x=>{if(active){if(!Array.isArray(x))throw new Error("Invalid report history response.");setRows(x);}}))

   .catch(e=>{if(active)setError(regulatoryError(e));}).finally(()=>{if(active)setLoading(false);});

  });return()=>{active=false;abort.abort();};

 },[id,retry]);

 useEffect(()=>{const refresh=()=>setRetry(x=>x+1);window.addEventListener("focus",refresh);window.addEventListener("development-session-changed",refresh);return()=>{window.removeEventListener("focus",refresh);window.removeEventListener("development-session-changed",refresh);};},[]);

 const findings=report&&report.metadata.id===id?report.findings??[]:[];

 const filtered=filterRegulatoryFindings(findings,status,query);



 return <div className={styles.stack}>

  {!embedded&&<header className={styles.heading}><h1>{id?"Regulatory report":"Regulatory assessment history"}</h1><Link href="/component-04/regulatory-assessments/new">New regulatory assessment</Link><p>Saved regulatory engine assessments. Separate from consolidated screening, C3 clearance and application approval.</p><Link href="/component-04/regulatory-assessments">Assigned report history</Link>  |  <Link href="/component-04/assessments/history">All assessment history</Link></header>}

  {loading&&<PendingIndicator>Loading saved regulatory report…</PendingIndicator>}

  {error&&<div role="alert" className={styles.notice}><p>{error}</p><button type="button" onClick={()=>setRetry(x=>x+1)}>Retry reading</button></div>}

  {!loading&&!error&&!id&&<section className={styles.card}><h2>Reports assigned to you</h2>{rows.length===0?<p>No independent reports are currently assigned to you.</p>:<table><caption>Saved assessments, newest first</caption><thead><tr><th>Assessment</th><th>Recorded status</th><th>Recorded time</th></tr></thead><tbody>{rows.map(x=><tr key={x.id}><td><Link href={`/component-04/regulatory-assessments/${x.id}`}>{x.actionName||"Regulatory assessment"}</Link></td><td><StatusBadge status={x.status}/></td><td>{sriLankaTime(x.evaluationTimestamp)}</td></tr>)}</tbody></table>}<p>Up to100 assigned records. No assessment is run by opening this view.</p></section>}

  {!loading&&!error&&report&&report.metadata.id===id&&<>

   <section className={styles.card}><h2>{report.metadata.actionName}</h2><p>Recorded regulatory outcome: <StatusBadge status={report.metadata.status}/></p><p>Recorded time: {sriLankaTime(report.metadata.evaluationTimestamp)}</p><p>{report.findings===null?"Individual findings were not persisted; no count is inferred.":`${findings.length} stored rule findings. ${Object.entries(regulatoryCounts(findings)).map(([s,n])=>`${n} ${statusLabel(s)}`).join("; ")}.`}</p>{report.provenance?<details><summary>Saved input, source and catalogue provenance</summary><pre className={styles.json}>{JSON.stringify(report.provenance,null,2)}</pre></details>:<p>Historical provenance was not persisted; no missing information is reconstructed.</p>}<details><summary>Stored IDs and available provenance</summary><pre className={styles.json}>{JSON.stringify(report.metadata,null,2)}</pre></details></section>

   {report.findings!==null&&<><RegulatoryFollowups evaluationId={report.metadata.id} findings={findings}/><section className={styles.card} aria-label="Finding filters"><label>Find rule, explanation or source <input value={query} onChange={e=>setQuery(e.target.value)}/></label><label>Returned status <select value={status} onChange={e=>setStatus(e.target.value)}><option value="">All statuses</option>{[...new Set(findings.map(f=>f.status))].map(s=><option key={s} value={s}>{statusLabel(s)}</option>)}</select></label><p>{filtered.length} of {findings.length} findings shown. Filters do not remove stored findings.</p></section>

   <section className={styles.card}><ProgressiveSteps label="Stored regulatory checks">{filtered.map(f=><ProgressiveStep key={f.ruleCode} title={f.expectedRequirement||statusLabel(f.category)} status={f.status} description={f.observedValueSummary||"No explanation stored."}><Finding finding={f}/></ProgressiveStep>)}</ProgressiveSteps>{filtered.length===0&&<p>No findings match these filters.</p>}</section></>}

  </>}

 </div>;

}

function Finding({finding:f}:{finding?:RegulatoryFinding}){

 if(!f)return <p>Select a stored finding.</p>;

 return <><RegulatoryFindingExplanation finding={f}/><p>Calculation: {f.calculationStatus||"Not stored"}. Blocking: {f.isBlocking?"Yes":"No"}.</p><details><summary>Exact stored finding</summary><pre className={styles.json}>{JSON.stringify(f,null,2)}</pre></details></>;
}
