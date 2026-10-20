"use client";
import { useState } from "react";
import type { ConsolidatedAssessment } from "@/modules/workflow-governance/types/caseScreening";
import styles from "./AssessmentReport.module.css";

export const screeningSections = ["Regulatory", "Conflict", "Early Governance", "Summary"] as const;
export type ScreeningSection = typeof screeningSections[number];
import {findingGroup} from "../services/reportPresentation";
export function AssessmentReport({ assessment, section, historical = false }: { assessment: ConsolidatedAssessment; section: ScreeningSection; historical?: boolean }) {
  const [selected, setSelected] = useState(0);
  const findings = assessment.consolidatedFindings.filter(f => section === "Summary" || f.dimension === (section === "Early Governance" ? "EarlyGovernance" : section));
  const item = findings[selected] || findings[0];
  const engine = section === "Regulatory" ? assessment.regulatoryCheck : section === "Conflict" ? assessment.conflictCheck : section === "Early Governance" ? assessment.earlyGovernanceCheck : null;
  const status = engine?.status ?? assessment.overallStatus;
  return <div className={styles.report}>
    <section className={styles.checks} aria-label={`${section} evaluated checks`}>
      <div className={styles.heading}><h2>{section === "Summary" ? "Assessment summary" : `${section} checks`}</h2><p>{section === "Summary" ? "Overall assessment" : "Section status"}: <strong>{String(status ?? "Not evaluated")}</strong></p><small>{findings.length} returned findings · {historical ? "Historical assessment" : "Recorded engine results"}</small>{section === "Regulatory" && typeof engine?.evaluatedRulesCount === "number" && <p>{engine.evaluatedRulesCount} rules evaluated. Only this configured rule set was checked.</p>}</div>
      {(["Issues", "Review items", "Satisfied / advisory"] as const).map(group => {
        const entries = findings.map((f, index) => ({ f, index })).filter(x => findingGroup(x.f) === group);
        return entries.length > 0 && <div key={group} className={styles.group}><h3>{group} <span>{entries.length}</span></h3>{entries.map(({ f, index }) => <button key={`${f.itemName}-${index}`} type="button" className={styles.step} aria-pressed={item === f} onClick={() => setSelected(index)}>
          <span aria-hidden="true" className={`${styles.circle} ${f.isBlocking ? styles.danger : f.requiresHumanReview ? styles.warning : styles.success}`}>{f.isBlocking ? "!" : f.requiresHumanReview ? "!" : "✓"}</span><span className={styles.stepText}><strong>{f.itemName}</strong><small>{f.status}</small></span><span aria-hidden="true">›</span>
        </button>)}</div>;
      })}
      {engine && <dl className={styles.technical}>{Object.entries(engine).filter(([key,v])=>key!=="status" && (typeof v==="number"||typeof v==="boolean"||key==="highestSeverity"||key==="referralReason")).map(([key,v])=><div key={key}><dt>{key.replace(/([A-Z])/g," $1").replace(/^./,c=>c.toUpperCase())}</dt><dd>{typeof v==="boolean"?(v?"Yes":"No"):String(v??"Not supplied")}</dd></div>)}</dl>}
      {!findings.length && <p className={styles.empty}>{engine ? "Section summary returned; individual check details were not supplied." : "Not evaluated: no section results were returned."}</p>}
    </section>
    <section className={styles.detail} aria-live="polite" aria-label="Selected finding details">
      {item ? <><span className={`${styles.badge} ${item.isBlocking ? styles.danger : item.requiresHumanReview ? styles.warning : styles.success}`}>{item.status}</span><h2>{item.itemName}</h2><h3>What was found?</h3><p>{item.details || "No explanation was supplied by the engine."}</p><h3>Evidence considered</h3><p className={styles.evidence}>{item.evidenceReference || "No evidence reference supplied by C4."}</p><h3>Rule or source returned</h3><p>{item.sourceAuthorityOrRule || "No rule reference supplied by C4."}</p><h3>What happens next?</h3><p>{item.isBlocking ? "Resolve the recorded blocker and obtain an authorized reassessment before proceeding." : item.requiresHumanReview ? "An authorized officer must review this finding and its supporting evidence." : "Retain this finding with the assessment for officer review. It does not constitute application approval."}</p></> : <><h2>{engine?"Returned section summary":"No evaluated check selected"}</h2><p>{engine?`The engine returned ${String(status)}. It did not return individual explanations for this section. Review the recorded source bindings and available findings; no additional checks are inferred.`:"Run screening when the required inputs are ready. Missing results are not a clearance."}</p></>}
      {item && <><h3>Why it matters</h3><p>{item.isBlocking ? "The engine identifies this as a blocker for further review under the evaluated inputs." : item.requiresHumanReview ? "The engine requests human review rather than treating this finding as resolved." : "This is part of the limited evaluated check set. It supports officer review and does not determine legal or application approval."}</p></>}
      {section === "Summary" && <div className={styles.recommendation}><h3>Returned officer recommendation</h3><p>{assessment.officerRecommendationSummary}</p>{assessment.requiredConditions.length > 0 && <ul>{assessment.requiredConditions.map(c => <li key={c}>{c}</li>)}</ul>}</div>}
      <details className={styles.technical}><summary>Sources & versions</summary><p>Catalogue / rule set: {String(assessment.provenance?.ruleSetVersion||"Not supplied")}<br/>Catalogue source: {String(assessment.provenance?.regulatoryCatalogueSource||"Not supplied")}</p><p>Only the returned rule set was evaluated; this is not a statement of full legal compliance. Development source labels do not establish real evidence clearance.</p><dl><dt>Assessment</dt><dd>{assessment.assessmentId}</dd><dt>Evaluated</dt><dd>{assessment.evaluatedAtUtc}</dd><dt>Snapshot</dt><dd>{assessment.verifiedFactSnapshotId}</dd><dt>Proposal version</dt><dd>{assessment.proposalVersion}</dd></dl><pre>{JSON.stringify({ section: engine, provenance: assessment.provenance }, null, 2)}</pre></details>
    </section>
  </div>;
}
