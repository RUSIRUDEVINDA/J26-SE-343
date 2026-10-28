"use client";
import type {InvestigationFile} from "../services/investigationEvidence";
import styles from "./AssessmentHistory.module.css";
export function CurrentInvestigationAttachments({investigationId,concern,attachments,busy,onDownload}:{investigationId:string;concern:string;attachments:InvestigationFile[];busy:boolean;onDownload:(file:InvestigationFile)=>void}){
 const files=attachments.filter(a=>a.investigationId===investigationId&&a.concern===concern);
 return <details className={styles.compact}><summary>Current investigation attachments ({files.length})</summary>{!files.length&&<p>No saved attachment for this concern.</p>}<ul className={styles.files}>{files.map(a=><li key={a.id}><span>{a.fileName} · {a.bytes.toLocaleString()} bytes</span><button type="button" disabled={busy} onClick={()=>onDownload(a)}>Download {a.fileName}</button></li>)}</ul></details>;
}
