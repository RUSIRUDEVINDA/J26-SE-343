import {Fragment} from "react";
import {orderedHandlingHistory,type HandlingView} from "../services/complaintHandling";
import {statusLabel,sriLankaTime} from "../services/presentation";
import styles from "./ComplaintHistory.module.css";

export function ComplaintHandlingHistory({view}:{view:HandlingView}){
 const history=orderedHandlingHistory(view);
 return <div className={styles.history}>{history.map(h=><article key={h.id} className={styles.card}><h4>{statusLabel(h.operation)}</h4><p className={styles.meta}>Revision {h.revision} · {sriLankaTime(h.atUtc)}</p><dl className={styles.fields}>
 <dt>Status</dt><dd>{h.fromStatus?statusLabel(h.fromStatus):"Previous state not returned in this authorized history"} → {statusLabel(h.status)}</dd>
 {h.publicSummary&&<><dt>Applicant-safe summary</dt><dd>{h.publicSummary}</dd></>}
 {h.reason&&<><dt>Reason</dt><dd>{h.reason}</dd></>}
 {h.details&&Object.entries(h.details).filter(([key])=>!/(?:id|sha256|hash)$/i.test(key)).map(([key,value])=><Fragment key={key}><dt>{statusLabel(key)}</dt><dd>{value}</dd></Fragment>)}
 {h.evidenceReferences?.map((entry,index)=><Fragment key={index}><dt>{entry.label}</dt><dd>{entry.reference} ({entry.kind})</dd></Fragment>)}
 {h.actorId&&<><dt>Recorded by</dt><dd>{["TakeDirect","AssignDs","Resolve","Close","Reopen"].includes(h.operation)?"Commissioner":"Authorized officer"}</dd></>}
 </dl>{h.actorId&&<details className={styles.technical}><summary>Technical details</summary><p>Actor {h.actorId} · event {h.id}</p>{h.details&&Object.entries(h.details).filter(([key])=>/(?:id|sha256|hash)$/i.test(key)).map(([key,value])=><p key={key}>{statusLabel(key)}: {value}</p>)}</details>}</article>)}{!history.length&&<p>No handling actions returned for this account.</p>}</div>;
}
