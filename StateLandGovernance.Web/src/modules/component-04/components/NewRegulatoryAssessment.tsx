"use client";

import {useEffect,useRef,useState} from "react";

import Link from "next/link";
import {SavedEarlyPrerequisite} from "./SavedEarlyPrerequisite";

import {PendingIndicator} from "./PendingIndicator";

import prepared from "../evidence/kiulakale-regulatory-input.json";

import provenance from "../evidence/kiulakale-input-provenance.json";

import {IndependentRegulatoryReports} from "./IndependentRegulatoryReports";

import {validateRegulatoryDraft,isCorrectableRegulatoryError,regulatorySubmissionKey,regulatorySubmissionEnabled,runRegulatory,savePending,pendingRequest,clearPending,findRegulatorySubmission,type RegulatoryInput,type RegulatoryRequest,type JsonValue} from "../services/regulatorySubmission";

import type {RegulatoryReport} from "../services/independentRegulatoryReports";

import styles from "./IndependentGovernance.module.css";

const booleanFields=new Set(["requiresLand", "resettlementApplicable", "preFeasibilityDone", "eiaDone", "duplicationAssessmentSupplied", "impactAssessmentApplicable", "isAssumption", "revenueExpected", "resettlementApplicable", "genderConsidered", "accessibilityConsidered", "applicable", "assessmentCompleted"]);

const numberFields=new Set(["extentHectares","submittedProjectBudget","amount","revenueForecastAmount","resettlementCost","oAndMCost","discountRate","submittedNpv","submittedIrr","submittedPaybackPeriod","submittedCbr","submittedPvBenefits","submittedPvCosts","baselineValue","baselineYear","targetValue"]);

const listFields=new Set(["expectedStakeholders","consultedStakeholders","declaredPolicies","assessmentReportReferences","hazardsConsidered","mitigationMeasures","kpiReferences"]);

const title=(key:string)=>key.replace(/([A-Z])/g," $1").replace(/^./,s=>s.toUpperCase());

export function NewRegulatoryAssessment({preparedDemo=false,onSaved}:{preparedDemo?:boolean;onSaved?:(report:RegulatoryReport,reused:boolean)=>void}){

 const[input,setInput]=useState<RegulatoryInput>({proposalId:""}),[version,setVersion]=useState(""),[source,setSource]=useState(""),[raw,setRaw]=useState("");

 const[caseId,setCaseId]=useState(""),[snapshotId,setSnapshotId]=useState(""),[proposalVersion,setProposalVersion]=useState("");

 const[early,setEarly]=useState(""),[earlyReady,setEarlyReady]=useState(false);
 const[rejected,setRejected]=useState(false);

 const[enabled,setEnabled]=useState(false);

 const[pending,setPending]=useState<RegulatoryRequest|null>(null),[report,setReport]=useState<RegulatoryReport|null>(null),[busy,setBusy]=useState(false),[error,setError]=useState(""),[notice,setNotice]=useState("");const flight=useRef(false),epoch=useRef(0);

 useEffect(()=>{function restore(){epoch.current++;setEarly("");setEarlyReady(false);setRejected(false);setReport(null);setInput({proposalId:""});setVersion("");setSource("");setRaw("");setCaseId("");setSnapshotId("");setProposalVersion("");setError("");setEnabled(false);const current=epoch.current;void regulatorySubmissionEnabled().then(value=>{if(current===epoch.current)setEnabled(value);}).catch(()=>{if(current===epoch.current)setEnabled(false);});let hasPending=false;try{const request=pendingRequest();hasPending=!!request;setPending(request);if(request){setInput(request.input);setVersion(request.inputVersion);setSource(request.sourceReference);setCaseId(request.c3Binding?.caseId||"");setSnapshotId(request.c3Binding?.snapshotId||"");setProposalVersion(request.c3Binding?.proposalVersion||"");setNotice("Uncertain submission restored. Check saved submission or retry this same request; do not create a new key.");}}catch(e){setPending(null);setError(e instanceof Error?e.message:"Sign in required.");}if(preparedDemo&&!hasPending)load();}restore();window.addEventListener("development-session-changed",restore);return()=>{epoch.current++;window.removeEventListener("development-session-changed",restore);};},[preparedDemo]);

 function load(){setInput(structuredClone(prepared) as RegulatoryInput);setVersion(provenance.inputVersion);setSource(provenance.sourceReference+" Prepared English adaptation SHA-256: "+provenance.englishProposalSha256);setCaseId("");setSnapshotId("");setProposalVersion("");setReport(null);setError("");setNotice("Inputs loaded only. No engine request, approval or C3 binding was created.");}

 async function run(){if(flight.current||!enabled)return;const current=epoch.current;flight.current=true;setBusy(true);setError("");let sent=false;try{

  let request=pending;if(!request||rejected){const errors=validateRegulatoryDraft(input,version,source);if(errors.length)throw new Error(errors.join(" "));

   if((snapshotId||proposalVersion)&&!caseId)throw new Error("C3 snapshot/version require an assigned C3 case.");

   if(!earlyReady)throw new Error("Select a saved finalized Early screening of this exact proposal first.");
   request={earlyRequestId:early,actionName:"Independent regulatory assessment",input,submissionKey:regulatorySubmissionKey(pending),inputVersion:version,schemaVersion:"c4-regulatory-input/1",sourceReference:source,c3Binding:caseId?{caseId,snapshotId:snapshotId||null,proposalVersion:proposalVersion||null}:null};savePending(request);setPending(request);setRejected(false);}

  sent=true;const result=await runRegulatory(request);if(current!==epoch.current)return;clearPending();setPending(null);setReport(result.report);onSaved?.(result.report,result.reused);setNotice(result.reused?"Saved submission retrieved; engines were not rerun.":"Actual engine result saved. Open saved report retrieves it without an engine rerun.");

 }catch(e){if(current===epoch.current){const correctable=isCorrectableRegulatoryError(e)||(!sent&&rejected);setRejected(correctable);setError((e instanceof Error?e.message:"Submission failed.")+(correctable?" Correct the draft; its submission key is retained to prevent duplicate records.":" If submission may have reached the server, check saved submission or retry the same key."));}}finally{flight.current=false;setBusy(false);}}

 async function check(){if(!pending||flight.current)return;const current=epoch.current;flight.current=true;setBusy(true);setError("");try{const saved=await findRegulatorySubmission(pending.submissionKey);if(current!==epoch.current)return;if(saved){setReport(saved);onSaved?.(saved,true);clearPending();setPending(null);setNotice("Persisted report retrieved. No engines rerun.");}else setNotice("No committed report for this key yet. Retry the same request safely; do not invent a new submission key.");}catch(e){if(current===epoch.current)setError(e instanceof Error?e.message:"Cannot check saved submission.");}finally{flight.current=false;setBusy(false);}}

 const locked=busy||!!pending&&!rejected;return <main className={styles.stack}><header><h1>New regulatory assessment</h1><p>Independent officer assessment. It does not publish C3 facts, verify allegations or approve a lease.</p><Link href="/component-04/regulatory-assessments">Saved regulatory reports</Link></header>

 <section className={styles.card}><h2>Demonstration proposal</h2><strong>{provenance.label}</strong><p>Kiulakale English adaptation. Source facts and fictional additions remain separate from real applications.</p><button disabled={locked} onClick={load}>Load Kiulakale demonstration proposal</button><ul>{provenance.notes.map(n=><li key={n}>{n}</li>)}</ul><details><summary>Read prepared English adaptation</summary><div style={{whiteSpace:"pre-wrap"}}>{provenance.proposalText}</div></details><details><summary>Source facts versus assumptions and original pages</summary><table><thead><tr><th>Topic</th><th>Original source</th><th>Adaptation</th><th>Limitation</th></tr></thead><tbody>{provenance.sourceAssumptionRows.map(row=><tr key={row.topic}><th>{row.topic}</th><td>{row.source}</td><td>{row.adaptation}</td><td>{row.limitation}</td></tr>)}</tbody></table></details><details><summary>Prepared source checksums</summary><pre>{JSON.stringify({preparedInputSha256:provenance.preparedInputSha256,englishProposalSha256:provenance.englishProposalSha256,originalPdfSha256:provenance.originalPdfSha256},null,2)}</pre></details></section>

 {!enabled&&<p role="status">Read-only preparation: regulatory submission is not activated or could not be confirmed. Loading and editing inputs does not run an engine.</p>}{notice&&<p role="status">{notice}</p>}{busy&&<PendingIndicator>Running or retrieving assessment…</PendingIndicator>}{error&&<p role="alert" className={styles.error}>{error}</p>}

 <details><summary>Review or edit structured proposal input</summary><fieldset disabled={locked} className={styles.card}><legend>Editable proposal inputs</legend><p>For this Kiulakale adaptation: monetary values are LKR, extent is hectares, and discount rate is a fraction (0.10 means10%).</p><p>Unknown fields remain null. Empty lists are explicit declarations, not inferred absence. Editing does not modify the source proposal or any saved report.</p><label className={styles.field}>Input version<input value={version} maxLength={100} onChange={e=>setVersion(e.target.value)}/></label><label className={styles.field}>Declared source and assumptions<textarea value={source} maxLength={2000} onChange={e=>setSource(e.target.value)}/></label>

 {Object.entries(input).map(([key,value])=><details key={key}><summary>{title(key)}</summary><Editor name={key} value={value} update={v=>setInput(x=>({...x,[key]:v}))}/></details>)}

 <details><summary>Optional C3 binding (no values assumed)</summary><label className={styles.field}>Assigned case UUID<input value={caseId} onChange={e=>setCaseId(e.target.value)}/></label><label className={styles.field}>Linked snapshot UUID<input value={snapshotId} onChange={e=>setSnapshotId(e.target.value)}/></label><label className={styles.field}>Proposal version UUID<input value={proposalVersion} onChange={e=>setProposalVersion(e.target.value)}/></label></details>

 <details><summary>Advanced structured JSON</summary><textarea rows={12} value={raw} onChange={e=>setRaw(e.target.value)} aria-label="Advanced regulatory input JSON"/><button type="button" onClick={()=>{try{const next=JSON.parse(raw);if(!next||Array.isArray(next)||typeof next!=="object")throw new Error("Input must be an object.");setInput(next);setError("");}catch(e){setError(e instanceof Error?e.message:"Invalid JSON");}}}>Apply JSON to draft only</button></details></fieldset></details>

 <SavedEarlyPrerequisite value={early} disabled={locked} onChange={(id,ready)=>{setEarly(id);setEarlyReady(ready);}}/>
 <section className={styles.card}><h2>Input summary before running</h2><p>Proposal: {String(input.proposalId||"Missing")}; version: {version||"Missing"}; schema: c4-regulatory-input/1.</p><p>C3 binding: {caseId||"None - independent demonstration"}. Catalogue selection is server-owned; its actual digest/version is saved with the result, never supplied by the browser.</p><details><summary>Exact draft values and unknowns</summary><pre className={styles.json}>{JSON.stringify(input,null,2)}</pre></details><p>The assessment can return unresolved, not applicable or non-compliant findings. No outcome is promised.</p>

 {!report&&<button className={styles.primary} disabled={!enabled||busy||((!pending||rejected)&&(!earlyReady||validateRegulatoryDraft(input,version,source).length>0))} onClick={()=>void run()}>{pending?rejected?"Submit corrected draft with same key":"Retry same submission":"Run Regulatory Assessment"}</button>}{pending&&<button disabled={busy} onClick={()=>void check()}>Check saved submission</button>}

 {report&&<><p>Findings below belong to the saved submitted input, not subsequent unsent draft edits.</p><p>Actual returned findings: {report.findings?.length??"Not stored"}. Overall: {report.metadata.status}.</p><Link href={`/component-04/regulatory-assessments/${report.metadata.id}`}>Open saved report</Link><button disabled={busy} onClick={()=>{setReport(null);setNotice("A deliberately new submission will get a new key. Existing report is preserved.");}}>Prepare another assessment</button></>}</section>

 {report&&<IndependentRegulatoryReports id={report.metadata.id}/>}</main>;

}

function Editor({name,value,update}:{name:string;value:JsonValue;update:(v:JsonValue)=>void}){

 if(Array.isArray(value))return <div><p>{value.length} declared items.</p>{value.map((v,i)=><fieldset key={i}><legend>{title(name)} {i+1}</legend><Editor name={name==="cashFlows"?"amount":name} value={v} update={n=>update(value.map((x,j)=>j===i?n:x))}/><button type="button" onClick={()=>update(value.filter((_,j)=>j!==i))}>Remove item</button></fieldset>)}{(listFields.has(name)||name==="cashFlows")&&<button type="button" onClick={()=>update([...value,name==="cashFlows"?0:""])}>Add item</button>}<button type="button" onClick={()=>update(null)}>Mark list unknown</button></div>;

 if(value&&typeof value==="object")return <div>{Object.entries(value).map(([k,v])=><Editor key={k} name={k} value={v} update={n=>update({...value,[k]:n})}/>)}</div>;

 if(value===null&&name==="preliminaryAssessment")return <p>Unknown. <button type="button" onClick={()=>update({preFeasibilityDone:null,preFeasibilityRef:null,eiaDone:null,eiaRef:null,assessmentReportReferences:null})}>Provide assessment information</button></p>;

 if(value===null&&listFields.has(name))return <p>{title(name)}: Unknown (not an explicit empty list). <button type="button" onClick={()=>update([])}>Provide list</button></p>;

 if(typeof value==="boolean"||booleanFields.has(name))return <label className={styles.field}>{title(name)}<select value={value===null?"unknown":String(value)} onChange={e=>update(e.target.value==="unknown"?null:e.target.value==="true")}><option value="unknown">Unknown</option><option value="true">Yes / applicable</option><option value="false">No / explicitly absent</option></select></label>;

 if(typeof value==="number"||numberFields.has(name))return <label className={styles.field}>{title(name)}<input type="number" step="any" placeholder="Unknown" value={value===null?"":String(value)} onChange={e=>update(e.target.value===""?null:Number(e.target.value))}/></label>;

 return <label className={styles.field}>{title(name)}{value===null&&" (Unknown)"}<textarea value={value===null?"":String(value)} placeholder="Unknown" onChange={e=>update(e.target.value===""?null:e.target.value)}/></label>;

}

