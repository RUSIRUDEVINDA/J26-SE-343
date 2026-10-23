"use client";
import {EvidenceStatusSummary} from "./EvidenceStatusSummary";
import {ManualEvidenceVerification} from "./ManualEvidenceVerification";
import {MeasuredWorkflowFeatures} from "./MeasuredWorkflowFeatures";
import {ProgressiveSteps,ProgressiveStep,StatusBadge} from "./ProgressiveSteps";
import {statusLabel,sriLankaTime} from "../services/presentation";
import Link from "next/link";
import type {RequestReport,AnomalyResult} from "../services/officerFlows";
import styles from "./KiwulaAssessmentWorkspace.module.css";
const readable=statusLabel;
export function OfficerAssessmentResult({report}:{report:RequestReport}){
 const result=report.result;
 return <section className={styles.card}><div className={styles.resultHeader}><h3>{report.kind==="early"?"Early Governance result":"Workflow analysis result"}</h3><StatusBadge status={report.state}/></div>
 {report.state!=="Saved"&&<p role="status">This request has no confirmed saved result. Refresh history for its status. Pending or Unknown requests must be investigated before another analysis; do not retry.</p>}
 {report.state==="Saved"&&result&&("anomalyScore" in result?<AnomalyDetails result={result}/>:<><h4>{readable(result.result.overallStatus)}</h4><EvidenceStatusSummary states={result.result.indicatorResults.map(i=>i.evidenceState)}/><ProgressiveSteps label="Early Governance screening findings">{result.result.indicatorResults.map(i=><ProgressiveStep key={i.indicatorType} title={readable(i.indicatorType)} description={i.message} status={i.evidenceState}/>)}</ProgressiveSteps></>)}
 {report.kind==="early"&&report.state==="Saved"&&<ManualEvidenceVerification requestId={report.id}/>}
 <Link href={`/component-04/officer-assessments/${report.id}`}>Open Saved Report</Link>
 <details><summary>Technical details and action history</summary><p>Request: {report.id}</p><p>Assessment: {report.earlyAssessmentId??report.anomalyAssessmentId??"Not confirmed"}</p><p>Source version: {report.sourceVersion}</p><ProgressiveSteps label="Recorded assessment actions">{report.audit.map(a=><ProgressiveStep key={a.operation} title={readable(a.operation)} description={sriLankaTime(a.createdAtUtc)} status="Recorded" tone="neutral"/>)}</ProgressiveSteps></details></section>;
}
function AnomalyDetails({result}:{result:AnomalyResult}){
 return <><p><strong>{result.flagged?"Unusual workflow activity flagged":"No unusual workflow activity flagged"}</strong></p><p className={styles.secondary}>Advisory sequence assessment; not a probability or finding of misconduct.</p><MeasuredWorkflowFeatures result={result}/></>;
}
