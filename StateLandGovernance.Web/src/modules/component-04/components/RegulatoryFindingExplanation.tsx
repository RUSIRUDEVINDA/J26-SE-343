import type {RegulatoryFinding} from "../services/independentRegulatoryReports";
import {statusLabel} from "../services/presentation";

export function RegulatoryFindingExplanation({finding:f}:{finding:RegulatoryFinding}) {
 const insufficient=f.status==="InsufficientInformation";
 const review=f.requiresHumanReview||["RequiresHumanReview","RequiresReview"].includes(f.status);
 return <section aria-label={`Explanation for ${f.ruleCode}`}>
  <h3>{statusLabel(f.category)} — {f.ruleCode}</h3>
  <p>Original screening outcome: <strong>{statusLabel(f.status)}</strong>. Rule version: {f.ruleVersion}.</p>
  <h4>{insufficient?"Missing or undetermined information":review?"Why human judgment is needed":"What was checked"}</h4>
  <p>{f.observedValueSummary||"The saved result contains no detailed explanation; clarification is required."}</p>
  {insufficient&&<p>This is the stored explanation, not an inferred document checklist. No additional mandatory document requirement is established here.</p>}
  <h4>Recorded rule requirement</h4><p>{f.expectedRequirement||"No requirement text was stored."}</p>
  <h4>Source reference</h4><p>{[f.sourceAuthority,f.sourceDocument,f.sourceSection].filter(Boolean).join(" / ")||"No source reference was stored."}</p>
  <h4>Recommended next action</h4><p>{f.recommendedAction||(insufficient?"Request clarification of the undetermined information.":review?"Seek a reasoned human review of the submitted information.":"No next action was stored.")}</p>
  <p>Recorded evidence state: {statusLabel(f.evidenceStatus||"Not stored")}. Applicability: {statusLabel(f.applicability)}.</p>
  <p>Regulatory screening is decision support, not a final legal or lease approval decision. A review note does not change this original finding.</p>
 </section>;
}
