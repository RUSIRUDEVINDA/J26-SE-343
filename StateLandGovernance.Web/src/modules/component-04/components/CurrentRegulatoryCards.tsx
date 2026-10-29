import type {RegulatoryFinding} from "../services/independentRegulatoryReports";
import type {FollowupEvent} from "../services/regulatoryFollowups";
import {c4FindingResolution} from "../services/c4FindingResolution";
import {ProgressiveSteps,ProgressiveStep} from "./ProgressiveSteps";
import {RegulatoryFindingExplanation} from "./RegulatoryFindingExplanation";
import {sriLankaTime,statusLabel} from "../services/presentation";
export function CurrentRegulatoryCards({evaluationId,findings,events}:{evaluationId:string;findings:RegulatoryFinding[];events:FollowupEvent[]|null}){
 const resolved=events?findings.filter(f=>c4FindingResolution(f,events,evaluationId).resolved).length:null;
 const outstanding=events?findings.filter(f=>!c4FindingResolution(f,events,evaluationId).resolved):[];
 return <section aria-label="Current C4 regulatory findings"><h3>Regulatory findings</h3><p role="status">{resolved===null?"Reading saved officer follow-ups...":`${resolved} of ${findings.length} Addressed / ${outstanding.length} Awaiting Officer Follow-up`}</p><p>Addressed means the officer follow-up is documented, not that outstanding concerns are resolved. Continue advances within C4 screening only. It does not approve a lease, land use, legal compliance or any external institutional process.</p>
 {outstanding.length>0&&<details open><summary>Awaiting Officer Follow-up ({outstanding.length})</summary><ul aria-label="Awaiting Officer Follow-up">{outstanding.map(f=><li key={f.ruleCode}><a href={`#c4-rule-${evaluationId}-${f.ruleCode}`}>{f.ruleCode}: {f.expectedRequirement||statusLabel(f.category)}</a></li>)}</ul></details>}
 <ProgressiveSteps label="Returned regulatory findings">{findings.map(f=>{const current=events?c4FindingResolution(f,events,evaluationId):null;return <ProgressiveStep key={f.ruleCode} title={<span id={`c4-rule-${evaluationId}-${f.ruleCode}`}>{statusLabel(f.category)}</span>} description={current?.resolved?f.status==="Compliant"?f.observedValueSummary:current.concern??"Saved DS resolution for this C4 finding.":f.expectedRequirement||f.observedValueSummary} status={current?.label??"Reading saved review"} tone={current?.resolved?"positive":"warning"}>
 <p><strong>Current C4 status: {current?.label??"Reading saved review"}</strong></p>{current?.concern&&<p><strong>Regulatory concern:</strong> {current.concern}</p>}{current?.event&&<>{current.event.missingInformation&&<p><strong>Missing information:</strong> {current.event.missingInformation}</p>}<p>{current.event.remarks}</p><p>Officer {current.event.actorId} - {sriLankaTime(current.event.createdAtUtc)} - revision {current.event.revision}</p>{current.event.references.length>0&&<ul>{current.event.references.map((r,i)=><li key={i}>{r}</li>)}</ul>}</>}
 <details><summary>Technical Details - original engine result</summary><RegulatoryFindingExplanation finding={f}/></details>
 </ProgressiveStep>;})}</ProgressiveSteps></section>;
}
