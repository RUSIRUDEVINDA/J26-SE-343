import {followupOperationLabel,latestFindingFollowup,actionableFollowupFinding} from "../services/regulatoryFollowups";
import type {ClosureEligibility,FollowupEvent} from "../services/regulatoryFollowups";
import type {RegulatoryFinding} from "../services/independentRegulatoryReports";
import {sriLankaTime,statusLabel,statusTone} from "../services/presentation";
import {StatusBadge} from "./ProgressiveSteps";
import {c4FindingResolution,isAddressingFollowup} from "../services/c4FindingResolution";
import styles from "./AssessmentHistory.module.css";

export function RegulatoryFollowupEntries({events,findings,loading=false}:{events:FollowupEvent[];findings?:RegulatoryFinding[]|null;loading?:boolean}){
 return <>{events.map(event=>{const finding=findings?.find(f=>f.ruleCode===event.ruleCode&&f.ruleVersion===event.ruleVersion);return <article className={styles.entry} key={event.id}>
  <h5>{event.ruleCode}{finding?` · ${finding.expectedRequirement||statusLabel(finding.category)}`:""}</h5>
  <div className={styles.concerns}><span>{finding?<StatusBadge status={`Initial Outcome: ${statusLabel(finding.status)}`} tone={statusTone(finding.status)}/>:<span>{loading?"Loading original finding…":"Unavailable in the loaded report"}</span>}</span>{event.operation==="FindingVerifiedClosed"&&<span><StatusBadge status="Verified & Closed" tone="positive"/></span>}</div>
  <div className={styles.concerns}><strong>{followupOperationLabel(event.operation)}</strong>{finding&&isAddressingFollowup(finding,event)&&<StatusBadge status="Addressed" tone="positive"/>}{event.reviewOutcome&&<StatusBadge status={event.reviewOutcome}/>}<span className={styles.meta}>revision {event.revision}</span></div>
  {event.missingInformation&&<p><strong>Missing information:</strong> {event.missingInformation}</p>}<p>{event.remarks}</p>{event.references.length>0&&<ul>{event.references.map((r,i)=><li key={i}>Document reference: {r}</li>)}</ul>}
  <p className={styles.meta}>Recorded by responsible DS {event.actorId} · {sriLankaTime(event.createdAtUtc)}</p>
 </article>;})}</>;
}
export function outstandingFindingAction(hold:string){
 if(hold.includes("awaiting officer follow-up"))return "Record a valid permitted officer follow-up. Outstanding regulatory concerns remain documented separately.";
 if(hold.includes("unresolved C4"))return "Record a resolving DS disposition for this C4 finding.";
 if(hold.includes("clarification remains unresolved"))return "Supply updated supporting information and obtain a deliberate reassessment before progressing.";
 if(hold.includes("record the required reasoned human review"))return "Resolve the saved human-review obligation before progressing. Officer closure is recorded separately.";
 if(/blocking|adverse/i.test(hold))return "An adverse or blocking outcome still prevents progression. Review the saved requirement and authoritative resolution process.";
 return "An independent or institutional requirement remains outstanding. Review its exact saved reason in Technical Details before progressing.";
}
export function CurrentFindingDispositions({findings,events,progressionHolds=[]}:{progressionHolds?:string[];findings:RegulatoryFinding[];events:FollowupEvent[];eligibility?:ClosureEligibility[]}){
 return <section aria-label="Current officer-reviewed finding statuses"><h3>Current officer review</h3>{findings.filter(actionableFollowupFinding).map(f=>{const latest=latestFindingFollowup(events,f);return <article key={f.ruleCode}><h4>{statusLabel(f.category??"Saved finding")}</h4><p>{f.expectedRequirement}</p><p>Original regulatory result: {statusLabel(f.status)}</p><p><strong>Current officer status: {c4FindingResolution(f,events,latest?.evaluationId??"").label}</strong></p>{progressionHolds.filter(h=>h.startsWith(f.ruleCode+":")).map(h=><p key={h}><strong>Required before progression:</strong> {outstandingFindingAction(h)}</p>)}<details><summary>Technical Details - saved rule</summary><p>{f.ruleCode} / {f.ruleVersion}</p><p>Original evidence verification: {statusLabel(f.evidenceStatus??"Unavailable")}</p><p>{f.recommendedAction}</p>{progressionHolds.filter(h=>h.startsWith(f.ruleCode+":")).map(h=><p key={h}>{h}</p>)}</details>{latest?.operation==="FindingVerifiedClosed"&&<p>C4 finding resolved. Later legal and institutional approvals remain outside this screening.</p>}{latest&&<><p>{latest.remarks}</p><p>Officer {latest.actorId} · {sriLankaTime(latest.createdAtUtc)} · revision {latest.revision}</p><ul>{latest.references.map((r,i)=><li key={i}>{r}</li>)}</ul></>}</article>;})}<p>Officer closure concerns only the selected saved finding. It does not change the engine result or constitute lease approval or independent evidence verification.</p></section>;
}
export function FollowupTechnicalDetails({evaluationId,events}:{evaluationId:string;events:FollowupEvent[]}){
 return <details className={styles.technical}><summary>Technical details</summary><p>Assessment: {evaluationId}</p>{events.map(event=><p key={event.id}>Event {event.id} · rule {event.ruleCode} / {event.ruleVersion} · revision {event.revision} · officer {event.actorId} · recorded state {event.status} · previous event {event.previousEventId??"None"} · SHA256 {event.eventSha256}</p>)}</details>;
}
