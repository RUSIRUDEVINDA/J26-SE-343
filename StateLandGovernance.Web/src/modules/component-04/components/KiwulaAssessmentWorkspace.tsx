"use client";
import {ProgressionSummary} from "./ProgressionSummary";

import {ProgressiveSteps,ProgressiveStep,StatusBadge} from "./ProgressiveSteps";
import {sriLankaTime} from "../services/presentation";
import {useRouter} from "next/navigation";
import {requestInvestigationLeave} from "../services/investigationLeave";
import {rememberCurrentEarly,matchesCurrentEarly,clearWorkflowSelection,rememberActiveWorkflow,readActiveWorkflow,validatedActiveWorkflow,currentRegulatoryForEarly} from "../services/currentEarlySelection";
import {readSequence,sequenceHref,type SequenceReadiness} from "../services/officerSequence";
import {officerApi} from "../services/officerFlows";
import {useOfficerSession} from "./OfficerFlowSession";
import {OfficerAssessmentResult} from "./OfficerAssessmentResult";
import {CombinedFindingsSummary} from "./CombinedFindingsSummary";

import Link from "next/link";

import {useEffect,useRef,useState} from "react";



import {kiwulaApi,type KiwulaCatalogue,type KiwulaEngine,type KiwulaReport} from "../services/kiwulaAssessments";

import {createPostWorkflowScope} from "../services/postWorkflowDemonstrations";

import {getRegulatoryReport,listRegulatoryReports,type RegulatorySummary,type RegulatoryReport} from "../services/independentRegulatoryReports";

import prepared from "../evidence/kiulakale-regulatory-input.json";

import source from "../evidence/kiulakale-input-provenance.json";

import {RegulatoryScreening} from "./RegulatoryScreening";
import type {Investigation,RequestReport} from "../services/officerFlows";
import {EarlyGovernanceInvestigation} from "./EarlyGovernanceInvestigation";



import {PendingIndicator} from "./PendingIndicator";

import styles from "./KiwulaAssessmentWorkspace.module.css";

const home="/component-04/assessments";

const human=(s:string)=>s==="InsufficientInformation"?"Needs more information":s==="RequiresHumanReview"?"Human review required":s.replace(/([a-z])([A-Z])/g,"$1 $2");

export function KiwulaAssessmentWorkspace({engine,id,historyOnly=false}:{engine?:KiwulaEngine|"regulatory";id?:string;historyOnly?:boolean}){

 const[catalogue,setCatalogue]=useState<KiwulaCatalogue|null>(null),[history,setHistory]=useState<KiwulaReport[]>([]),[report,setReport]=useState<KiwulaReport|null>(null),[error,setError]=useState(""),[loading,setLoading]=useState(true),[busy,setBusy]=useState(false);

 const router=useRouter();const {actor,epoch,generation}=useOfficerSession();
 const [earlyId,setEarlyId]=useState<string|null>(null),[regulatoryId,setRegulatoryId]=useState<string|null>(null),[sequence,setSequence]=useState<SequenceReadiness|null>(null),[sequenceError,setSequenceError]=useState(""),[sequenceBusy,setSequenceBusy]=useState(false),[sequenceRefresh,setSequenceRefresh]=useState(0);
 const [conflictId,setConflictId]=useState<string|null>(null);
 const [showProposal,setShowProposal]=useState(false);
 const [currentEarly,setCurrentEarly]=useState<RequestReport|null>(null);
 const [currentRegulatory,setCurrentRegulatory]=useState<RegulatoryReport|null>(null);
 
 const [regulatoryRows,setRegulatoryRows]=useState<RegulatorySummary[]>([]);

 const scope=useRef(createPostWorkflowScope()),refreshVersion=useRef(0);const [selection,setSelection]=useState(engine??"proposal");

 async function refresh(){const c=scope.current.capture();const version=++refreshVersion.current;setLoading(true);setError("");

 const [cat,rows,reg]=await Promise.allSettled([kiwulaApi.catalogue(c.signal),historyOnly?kiwulaApi.history(c.signal):Promise.resolve([]),historyOnly?listRegulatoryReports(c.signal):Promise.resolve([])]);

 if(!scope.current.current(c.epoch)||version!==refreshVersion.current)return;

 setCatalogue(cat.status==="fulfilled"?cat.value:null);setHistory(rows.status==="fulfilled"?rows.value:[]);

 setRegulatoryRows(reg.status==="fulfilled"?reg.value.filter(r=>r.proposalId===prepared.proposalId):[]);

 

 const failed=[cat,rows,reg].find(x=>x.status==="rejected");if(failed?.status==="rejected")setError(failed.reason instanceof Error?failed.reason.message:"Saved information unavailable. Refresh before starting any assessment.");

 if(id){try{const saved=await kiwulaApi.get(id,c.signal);if(scope.current.current(c.epoch)&&version===refreshVersion.current)setReport(saved);}catch(e){if(scope.current.current(c.epoch))setError(e instanceof Error?e.message:"Retrieval failed.");}}

 if(scope.current.current(c.epoch)&&version===refreshVersion.current)setLoading(false);

 }

 useEffect(()=>{const currentScope=scope.current;let active=true;const changed=()=>{currentScope.invalidate();setBusy(false);setCatalogue(null);setHistory([]);setRegulatoryRows([]);setReport(null);setCurrentRegulatory(null);setCurrentEarly(null);setEarlyId(null);setRegulatoryId(null);setConflictId(null);setSequence(null);void refresh();};queueMicrotask(()=>{if(active)void refresh();});window.addEventListener("development-session-changed",changed);return()=>{active=false;currentScope.invalidate();window.removeEventListener("development-session-changed",changed);};

 // eslint-disable-next-line react-hooks/exhaustive-deps
 },[id]);

 useEffect(()=>{let active=true;const restore=()=>queueMicrotask(()=>{if(!active||id||historyOnly)return;const q=new URL(window.location.href).searchParams;const context=actor?readActiveWorkflow(actor,q,sessionStorage):{earlyRequestId:null,regulatoryEvaluationId:null,conflictReportId:null};setCurrentEarly(null);setCurrentRegulatory(null);setEarlyId(context.earlyRequestId);setRegulatoryId(context.regulatoryEvaluationId);setConflictId(context.conflictReportId);setSelection(engine==="conflict"&&["step4-pending","combined-findings"].includes(q.get("stage")??"")?"combined":engine??"proposal");setSequenceRefresh(n=>n+1);});restore();window.addEventListener("popstate",restore);return()=>{active=false;window.removeEventListener("popstate",restore);};},[engine,id,historyOnly,actor,generation]);
 useEffect(()=>{let active=true;const version=epoch.current;queueMicrotask(()=>{if(!active||!actor||id||historyOnly)return;setSequenceError("");setSequenceBusy(true);void readSequence(earlyId,regulatoryId).then(async state=>{if(!active||version!==epoch.current)return;setSequence(state);}).catch(e=>{if(active&&version===epoch.current){setSequence(null);setSequenceError(e instanceof Error?e.message:"Sequence unavailable.");}}).finally(()=>{if(active&&version===epoch.current)setSequenceBusy(false);});});return()=>{active=false;};},[actor,epoch,generation,earlyId,regulatoryId,id,historyOnly,sequenceRefresh]);
 useEffect(()=>{let active=true;const version=epoch.current;
  if(actor&&earlyId&&!id&&!historyOnly){
   const selection={earlyRequestId:earlyId,regulatoryEvaluationId:regulatoryId,conflictReportId:conflictId};
   void Promise.all([officerApi<RequestReport>(`officer-assessments/${encodeURIComponent(earlyId)}`),regulatoryId?getRegulatoryReport(regulatoryId):Promise.resolve(null)]).then(async ([original,regulatory])=>{
    if(!original.investigationId)throw new Error("The selected saved workflow has no investigation.");
    const investigation=await officerApi<Investigation>(`officer-investigations/${encodeURIComponent(original.investigationId)}`);
    const context=validatedActiveWorkflow(actor,selection,original,investigation,regulatory,prepared.proposalId);
    if(!active||version!==epoch.current)return;
    rememberCurrentEarly(actor,original.id,sessionStorage);rememberActiveWorkflow(actor,context,sessionStorage);
    setCurrentEarly(original);setCurrentRegulatory(regulatory);
   }).catch(e=>{if(active&&version===epoch.current){clearWorkflowSelection(actor,sessionStorage);setCurrentEarly(null);setCurrentRegulatory(null);setSequenceError(e instanceof Error?e.message:"Selected workflow unavailable.");}});
  }
  return()=>{active=false;};
 },[actor,epoch,generation,earlyId,regulatoryId,conflictId,id,historyOnly,sequenceRefresh]);
 function bind(early:string|null,regulatory:string|null){if(actor){rememberCurrentEarly(actor,early,sessionStorage);rememberActiveWorkflow(actor,{earlyRequestId:early,regulatoryEvaluationId:regulatory,conflictReportId:null},sessionStorage);}setEarlyId(early);setRegulatoryId(regulatory);setConflictId(null);setSequence(null);const url=new URL(window.location.href);for(const key of ["earlyRequestId","regulatoryEvaluationId","conflictReportId","stage","freshWorkflow"])url.searchParams.delete(key);if(early)url.searchParams.set("earlyRequestId",early);if(regulatory)url.searchParams.set("regulatoryEvaluationId",regulatory);window.history.replaceState(null,"",url);}
 function earlyResult(value:RequestReport|null){setCurrentEarly(value);setCurrentRegulatory(null);bind(value?.state==="Saved"?value.id:null,null);}
 function regulatoryResult(value:RegulatoryReport){setCurrentRegulatory(value);bind(earlyId,value.metadata.id);}
 function openFreshProposal(){if(!requestInvestigationLeave()||!actor)return;clearWorkflowSelection(actor,sessionStorage);setEarlyId(null);setRegulatoryId(null);setConflictId(null);setCurrentEarly(null);setCurrentRegulatory(null);setSequence(null);setSelection("early");router.push(`/component-04/assessments/early?freshWorkflow=${crypto.randomUUID()}`);}
 const proposal=catalogue?.proposal;
 const activeRegulatory=currentRegulatoryForEarly(currentRegulatory,earlyId,regulatoryId);

 const shownReport=id?report:null;
 const stages=[["early","Early Governance"],["regulatory","Regulatory Screening"],["conflict","Conflict Detection"],["combined","Combined Findings Summary"]];
 const completed=(stage:string)=>stage==="regulatory"?!!currentRegulatory:stage==="early"&&currentEarly?.state==="Saved";
 const move=(stage:string)=>{if(!requestInvestigationLeave())return;if(stage==="combined"){setSelection("combined");const url=new URL(window.location.href);url.searchParams.set("stage","combined-findings");window.history.pushState(null,"",url);return;}if(stage==="proposal"){router.push(home);return;}if(stage==="early"||stage==="regulatory"||stage==="conflict"){setSelection(stage);router.push(sequenceHref(stage,earlyId,regulatoryId,conflictId));}};

 if(historyOnly)return <div className={styles.workspace}><h2>Regulatory and earlier assessments</h2><Link href={home}>Back to Proposals</Link>{loading&&<PendingIndicator>Loading saved reports...</PendingIndicator>}{error&&<p role="alert">{error}</p>}<section className={styles.card}><h2>Regulatory assessments</h2><div className={styles.scroll}><table><thead><tr><th>Assessment</th><th>Date / time (Sri Lanka)</th><th>Actual status</th><th>Report</th></tr></thead><tbody>{regulatoryRows.map(r=><tr key={r.id}><td>Regulatory / Kiwula Kale</td><td>{sriLankaTime(r.evaluationTimestamp)}</td><td><StatusBadge status={r.status}/></td><td><Link href={`/component-04/regulatory-assessments/${r.id}`}>View Report</Link></td></tr>)}</tbody></table></div>{!loading&&!regulatoryRows.length&&<p>No accessible saved regulatory assessments.</p>}</section><section className={styles.card}><h2>Earlier screening and decision comparisons</h2><div className={styles.scroll}><table><thead><tr><th>Assessment</th><th>Date / time (Sri Lanka)</th><th>Actual status</th><th>Report</th></tr></thead><tbody>{history.map(r=><tr key={r.id}><td>{human(r.engine)} / Kiwula Kale</td><td>{sriLankaTime(r.createdAtUtc)}</td><td><StatusBadge status={r.result.overallStatus??r.result.status??"Saved"}/></td><td><Link href={home+"/reports/"+r.id}>View Report</Link></td></tr>)}</tbody></table></div></section></div>;
 return <div className={styles.workspace}><header><p className={styles.eyebrow}>Governance Intelligence</p><h1>{!id&&selection==="proposal"?"Proposals":"Project_Proposal_Kiwula Kale"}</h1>{(selection!=="proposal"||showProposal)&&<><p>Puttalam · Mahakumbukkadawala · proposed coconut cultivation · 17 acres stated</p><p className={styles.secondary}>Advisory screening; proposal facts and institutional routing require verification.</p></>}</header>

 {!id&&selection!=="proposal"&&<nav aria-label="C4 workflow stages" className={styles.steps}><ProgressiveSteps label="C4 presentation stages, not institutional execution">{stages.map(([value,label])=><ProgressiveStep key={value} title={value==="combined"?<span>{label}</span>:<button onClick={()=>move(value)} disabled={busy||sequenceBusy||(value==="regulatory"&&!sequence?.canRunRegulatory)||(value==="conflict"&&!sequence?.canProceedToConflict)} aria-current={selection===value?"step":undefined}>{label}</button>} current={selection===value} status={selection===value?"Current":value==="conflict"?"Capability limitation":completed(value)?"Saved":value==="handoff"?"Preview":"Available"}/>)}</ProgressiveSteps></nav>}

 <div className={styles.toolbar}><button onClick={()=>{setSequenceRefresh(n=>n+1);void refresh();}} disabled={busy||loading||sequenceBusy}>Refresh saved results and progression</button></div>

 {loading&&<PendingIndicator>Retrieving saved information…</PendingIndicator>}{error&&<p role="alert" className={styles.notice}>{error}</p>}{busy&&<PendingIndicator>Evaluating and saving this assessment…</PendingIndicator>}

 {!id&&selection==="proposal"&&<section className={styles.card}><h2>Proposals</h2><p>Select a proposal to begin live screening. Start with Early Governance; saving or viewing does not run screening.</p><div className={styles.scroll}><table><thead><tr>{["Proposal name","Location","Land extent","Proposed use","Screening status","Action"].map(x=><th key={x}>{x}</th>)}</tr></thead><tbody><tr><th scope="row">{proposal?.displayName??"Project_Proposal_Kiwula Kale"}</th><td>Puttalam · Mahakumbukkadawala</td><td>{proposal?.extent??`${prepared.landRequirement.extentHectares} ha / 17 stated acres`}</td><td>{proposal?.proposedUse??"Coconut cultivation"}</td><td>{loading?"Loading":error?"Unavailable":currentRegulatory?"Regulatory screening completed":"Ready to screen"}</td><td><div className={styles.proposalActions}><button aria-expanded={showProposal} aria-controls="proposal-details" onClick={()=>setShowProposal(value=>!value)}>View Proposal Data</button><button className={styles.primary} disabled={loading||!!error} onClick={openFreshProposal}>Start Screening</button></div></td></tr></tbody></table></div></section>}

 {!id&&selection==="proposal"&&showProposal&&<div id="proposal-details"><button onClick={()=>setShowProposal(false)}>Close Proposal Data</button><section className={styles.card}><h2>Proposal information</h2><label>Proposal<select aria-label="Select proposal" defaultValue={prepared.proposalId}><option value={prepared.proposalId}>Project_Proposal_Kiwula Kale</option></select></label><dl><dt>Original source</dt><dd>Kiwula Kale English adaptation</dd><dt>Land location</dt><dd>Puttalam; Mahakumbukkadawala DS; GN 614/F Kiwula No. 01; Anamaduwa local-authority area. Province unknown.</dd><dt>Land and use</dt><dd>17 acres / {prepared.landRequirement.extentHectares} ha; coconut cultivation; dairy is a future feasibility option only.</dd><dt>Presentation label</dt><dd>Kiwula Kale State Land · Lot 17 (user-provided presentation label; not a verified cadastral identifier).</dd><dt>Tenure</dt><dd>15-year request is an adaptation assumption. Land classification, parcel, ownership and lawful tenure unknown.</dd></dl><details><summary>Source, versions and assumptions</summary><p>{source.sourceReference}</p><ul>{source.notes.map(note=><li key={note}>{note}</li>)}</ul><p>Input version {source.inputVersion}; schema {source.schemaVersion}; prepared hash {source.preparedInputSha256}.</p><p>Fictional replacement proponent, not the original applicant; no real case or parcel binding.</p><pre>{source.proposalText}</pre></details></section>

 <section className={styles.card}><h2>Proposed institutional workflow</h2><p>This is a reference-based plan for discussion. No approved routing policy is bound to this proposal. Province coverage and selected external institutions require confirmation.</p>{proposal?<div className={styles.scroll}><table><thead><tr><th>Review</th><th>Evidence status</th><th>Sequence / dependency</th><th>Why</th></tr></thead><tbody>{proposal.workflow.map(s=><tr key={s.code}><th scope="row">{s.institution}</th><td>{s.status}</td><td>{s.dependsOn}</td><td>{s.reason}</td></tr>)}</tbody></table></div>:<p>Activate the reviewed workspace API to retrieve the versioned workflow. Existing topology: selected external reviews → DS → provincial/interprovincial review → Commissioner; no province branch is established here. Cabinet threshold remains strictly above 50 acres.</p>}</section></div>}

 {!id&&selection!=="proposal"&&<>{sequenceBusy&&<PendingIndicator>Checking persisted prerequisites…</PendingIndicator>}{sequenceError&&<p role="alert">{sequenceError}</p>}{sequence&&<ProgressionSummary state={sequence} stage={selection}/>}</>}
 {!id&&selection==="regulatory"&&<>{!sequence?.canRunRegulatory?<><p>Complete or explicitly select a finalized saved Early screening before running Regulatory.</p></>:regulatoryId&&currentRegulatory?.metadata.id!==regulatoryId?<p role="status">Reading the exact active Regulatory evaluation. No new evaluation is created.</p>:<RegulatoryScreening key={`${actor}/${earlyId}/${regulatoryId??"fresh"}`} progressionHolds={sequence?.holds??[]} earlyRequestId={earlyId!} result={activeRegulatory} onResult={regulatoryResult} onFollowupRecorded={()=>setSequenceRefresh(n=>n+1)}/>}<div className={styles.toolbar}><button onClick={()=>move("early")}>Back to Early Governance</button><button className={styles.primary} disabled={sequenceBusy||!sequence?.canProceedToConflict} onClick={()=>move("conflict")}>Continue to Conflict Detection →</button></div></>}
 {!id&&selection==="conflict"&&sequence?.canProceedToConflict&&<section className={styles.card}><h2>Pre-workflow conflict screening</h2><p className={styles.notice}>This capability is not supported for the available proposal evidence. The existing engine compares recorded decisions on a common subject and effective period. It does not inspect land claims or calculate spatial overlaps from this proposal.</p><p>No verified parcel identifier, claims register, boundaries or allocation records are available. Institutional review conditions must not substitute for those inputs.</p><p>The current proposal has no supported persisted pre-workflow Conflict result. No historical decision comparison is substituted.</p></section>}
 {!id&&selection==="early"&&<>{matchesCurrentEarly(earlyId,currentEarly)&&currentEarly?<OfficerAssessmentResult report={currentEarly} currentWorkflow/>:earlyId?<p role="status">Reading the selected current assessment {earlyId}. No historical result is substituted.</p>:<EarlyGovernanceInvestigation onResult={earlyResult}/>}<div className={styles.toolbar}><button onClick={()=>move("proposal")}>Back to Proposals</button><button className={styles.primary} disabled={sequenceBusy||!sequence?.canRunRegulatory} onClick={()=>move("regulatory")}>Continue to Regulatory Screening →</button></div></>}

 {shownReport&&<section className={styles.card}><h2>{shownReport.engine==="conflict"?"Conflict detection":"Early governance screening"} assessment saved</h2><p><strong>{human(shownReport.result.status??shownReport.result.overallStatus??"Saved")}</strong></p><p>{shownReport.engine==="conflict"?`${shownReport.result.conflicts?.length??0} possible conflicting decisions found in the supplied examples. This is not regulatory clearance.`:"Review the evidence gaps before making any institutional decision. This screening does not verify allegations."}</p>{shownReport.result.conflicts?.map(f=><article key={f.conflictId}><h3>{human(f.conflictType)} · {f.severity}</h3><p>{f.explanation}</p><p>Records: {f.involvedDecisionIds.join(", ")}. Evidence rule: {f.evidenceRule}</p><p>Next: {f.recommendedAction}</p></article>)}{shownReport.result.indicatorResults&&<div className={styles.scroll}><table><thead><tr><th>Concern</th><th>Evidence</th><th>Finding / follow-up</th></tr></thead><tbody>{shownReport.result.indicatorResults.map(f=><tr key={f.indicatorType}><th>{human(f.indicatorType)}</th><td>{human(f.evidenceState)}<br/>{f.evidenceReference??"No supplied evidence"}</td><td>{f.message}{f.needsEvidence&&" Obtain evidence and human review."}</td></tr>)}</tbody></table></div>}<Link href={home+"/reports/"+shownReport.id}>Open saved report</Link><details><summary>Evaluated input and provenance</summary><p>Created {shownReport.createdAtUtc}; input {shownReport.inputVersion}; engine {shownReport.engineVersion}; input SHA256 {shownReport.inputHash}; engine artifact SHA256 {shownReport.engineArtifactHash}; source {shownReport.sourceReference}; assessment {shownReport.assessmentId}</p><InputTable rows={shownReport.input}/></details></section>}

 {!id&&selection==="conflict"&&<div className={styles.toolbar}><button onClick={()=>move("regulatory")}>Back to Regulatory Screening</button><button className={styles.primary} disabled={sequenceBusy||!sequence?.canProceedToConflict} onClick={()=>move("combined")}>Continue →</button></div>}
 {!id&&selection==="combined"&&<><CombinedFindingsSummary selection={{earlyRequestId:earlyId,regulatoryEvaluationId:regulatoryId,conflictReportId:conflictId}} proposalId={prepared.proposalId} onConflictChange={value=>{setConflictId(value);const url=new URL(window.location.href);if(value)url.searchParams.set("conflictReportId",value);else url.searchParams.delete("conflictReportId");window.history.pushState(null,"",url);}}/><button onClick={()=>move("conflict")}>Back to Conflict Detection</button></>}




 </div>;

}

function InputTable({rows}:{rows?:Record<string,unknown>[]}){if(!rows)return <p>Server input preview unavailable until activation.</p>;return <div className={styles.scroll}>{rows.map((row,i)=><details key={i} open><summary>Input {i+1}: {String(row.indicatorType??row.decisionId??"record")}</summary><dl>{Object.entries(row).filter(([,v])=>v!==null).map(([k,v])=><div key={k}><dt>{human(k)}</dt><dd>{typeof v==="object"?JSON.stringify(v):String(v)}</dd></div>)}</dl></details>)}</div>;}

