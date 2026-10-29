"use client";
import {EvidenceStatusSummary} from "./EvidenceStatusSummary";
import {CurrentEarlyResult} from "./CurrentEarlyResult";
import {ManualEvidenceVerification} from "./ManualEvidenceVerification";
import {InvestigationCompletion} from "./InvestigationCompletion";
import {MeasuredWorkflowFeatures} from "./MeasuredWorkflowFeatures";
import {ProgressiveSteps,ProgressiveStep,StatusBadge} from "./ProgressiveSteps";
import {statusLabel,sriLankaTime} from "../services/presentation";
import Link from "next/link";
import type {RequestReport,AnomalyResult} from "../services/officerFlows";
import styles from "./KiwulaAssessmentWorkspace.module.css";
const readable=statusLabel;
export function OfficerAssessmentResult({report,currentWorkflow=false}:{report:RequestReport;currentWorkflow?:boolean}){
 const result=report.result;
 return <section className={styles.card}><div className={styles.resultHeader}><h3>{report.kind==="early"?"Early Governance Result":"Workflow analysis result"}</h3><StatusBadge status={report.state}/></div>
 {report.state!=="Saved"&&<p role="status">This request has no confirmed saved result. Refresh history for its status. Pending or Unknown requests must be investigated before another analysis; do not retry.</p>}
 {currentWorkflow&&report.kind==="early"&&report.state==="Saved"?<CurrentEarlyResult key={report.id} report={report}/>:<>
 {report.state==="Saved"&&result&&("anomalyScore" in result?<AnomalyDetails result={result} timeline={report.projection??undefined} showSavedReport={!currentWorkflow}/>:<><h4>{readable(result.result.overallStatus)}</h4><EvidenceStatusSummary states={result.result.indicatorResults.map(i=>i.evidenceState)}/><ProgressiveSteps label="Early Governance screening findings">{result.result.indicatorResults.map(i=><ProgressiveStep key={i.indicatorType} title={readable(i.indicatorType)} description={i.message} status={i.evidenceState}/>)}</ProgressiveSteps></>)}
 {report.kind==="early"&&report.state==="Saved"&&<>{report.investigationId&&<InvestigationCompletion investigationId={report.investigationId}/>}<ManualEvidenceVerification requestId={report.id}/></>}</>}
 {!currentWorkflow&&!(result&&"anomalyScore" in result)&&<Link href={`/component-04/officer-assessments/${report.id}`}>Open Saved Report</Link>}
 <details><summary>Technical details and action history</summary><p>Request: {report.id}</p><p>Assessment: {report.earlyAssessmentId??report.anomalyAssessmentId??"Not confirmed"}</p><p>Source version: {report.sourceVersion}</p><ProgressiveSteps label="Recorded assessment actions">{report.audit.map(a=><ProgressiveStep key={a.operation} title={readable(a.operation)} description={sriLankaTime(a.createdAtUtc)} status="Recorded" tone="neutral"/>)}</ProgressiveSteps></details></section>;
}
function AnomalyDetails({result,timeline,showSavedReport}:{result:AnomalyResult;timeline?:{eventTimeZone?:string;events:{timestamp:string}[]};showSavedReport:boolean}){
 return <><p><strong>{result.flagged?"Unusual workflow activity flagged":"No unusual workflow activity flagged"}</strong></p><p className={styles.secondary}>Advisory sequence assessment; not a probability or finding of misconduct.</p><MeasuredWorkflowFeatures result={result} timeline={timeline}/>{showSavedReport&&<div className={styles.toolbar}><button type="button" disabled title="Workflow Governance handoff is not available yet">Send results to Workflow Governance</button></div>}</>;
}
