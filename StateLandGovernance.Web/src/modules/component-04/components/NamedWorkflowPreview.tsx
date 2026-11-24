"use client";
import Link from "next/link";
import {OfficerAnomalyPanel} from "./OfficerAnomalyPanel";
import {ProgressiveSteps,ProgressiveStep} from "./ProgressiveSteps";
import {sriLankaTime} from "../services/presentation";
import type {ReactNode} from "react";
import workflow from "../evidence/kiwula-named-workflow-v2.json";
import styles from "./KiwulaAssessmentWorkspace.module.css";
export function InstitutionalTimeline(){return <ProgressiveSteps label="Illustrative institutional workflow events">{workflow.events.map(e=><ProgressiveStep key={e.eventSeq} title={e.activity} description={<>{e.institutionName}<br/>{sriLankaTime(e.timestamp)} · Sri Lanka</>} status={e.outcome} tone="neutral"/>)}</ProgressiveSteps>;}
export function NamedWorkflowPreview({children,analysis=true}:{children?:ReactNode;analysis?:boolean}){
 return <div className={styles.workspace}><header><p className={styles.eyebrow}>Project_Proposal_Kiwula Kale</p><h1>{analysis?"Post-Workflow Anomaly":"Institutional Workflow Preview"}</h1><p className={styles.notice}>Illustrative workflow preview — no institutional action has been submitted.</p></header><section className={styles.card}><div className={styles.resultHeader}><h2>Institutional timeline</h2><span className={styles.secondary}>7 events · Sri Lanka time</span></div><InstitutionalTimeline/>{analysis?<OfficerAnomalyPanel/>:<div className={styles.toolbar}><Link href="/component-04/assessments/early">Back to Early Governance</Link><Link href="/component-04/post-workflow">Continue to Post-Workflow Anomaly</Link></div>}<details><summary>Source, limitations and routing assumptions</summary><p>Institutional routing is unconfirmed. These are not official decisions.</p><p>Source version: {workflow.sourceVersion}; proposal reference: {workflow.proposalId}.</p><ul>{workflow.assumptions.map(s=><li key={s}>{s}</li>)}</ul><p>No C3 workflow, official decision or legal handover is created by this preview.</p></details></section>{children}</div>;
}
