"use client";
import { useState } from "react";
import Link from "next/link";
import { Button } from "@/shared/components/Button";
import { StatusBadge } from "@/shared/components/StatusBadge";
import { useGovernanceMode } from "./GovernanceWorkspace";
import { Unavailable, Facts, Checks } from "./GovernancePanels";
import { complaintCategories, demoComplaints, demoAnomaly, screeningIndicators } from "../services/demoEvidence";
import { formatGovernanceTime } from "../adapters/presentation";
import styles from "./GovernanceWorkspace.module.css";
import { StructuredEvaluation } from "./LiveGovernanceEvaluations";

export type Capability = "complaints" | "anomalies" | "screening" | "timing" | "commissioner" | "decision-review" | "summary" | "verification" | "audit" | "risk" | "explain" | "consensus" | "conditions";
const titles: Record<Capability, [string, string]> = {
  complaints: ["Complaint Intelligence", "Advisory machine predictions and individual assessment history."],
  anomalies: ["Post-Workflow Governance Anomaly Detection", "Statistical review of completed workflow traces, separate from legal and rule-based findings."],
  screening: ["Early Governance Screening", "Evidence-based screening of seven recorded governance indicators."],
  timing: ["Lease Approval Timing", "Two-calendar-month business expectation from confirmed complete proposal to applicant handover."],
  commissioner: ["Governance Review Queue", "Cases awaiting a separate Commissioner governance review process."],
  "decision-review": ["Governance Decision Review", "Review governance evidence before recording an authorized human decision."],
  summary: ["Final Governance Summary", "Completed, pending and unavailable checks remain visible as separate outcomes."],
  verification: ["Verified Governance Record", "Record integrity and verification evidence from the shared trust service."],
  audit: ["Governance Audit History", "Audit records and their integrity evidence from the shared trust service."],
  risk: ["Governance Risk Assessment", "Advisory governance risk evaluation from supplied evidence."],
  explain: ["Explainable Governance", "Plain-language explanations of supplied governance findings."],
  consensus: ["Institutional Consensus", "Evaluate supplied institutional positions under the provided policy."],
  conditions: ["Conditional Verification", "Evaluate supplied condition and evidence status."],
};
export function GovernanceCapabilityPage({ capability }: { capability: Capability }) {
  const demo = useGovernanceMode() === "demo";
  return <>
    <header className={styles.heading}><h1>{titles[capability][0]}</h1><p>{titles[capability][1]}</p></header>
    {capability === "complaints" && <ComplaintView demo={demo} />}
    {capability === "anomalies" && <AnomalyView demo={demo} />}
    {capability === "screening" && <ScreeningView demo={demo} />}
    {capability === "timing" && <TimingView />}
    {capability === "commissioner" && <QueueView />}
    {capability === "decision-review" && <DecisionView />}
    {capability === "summary" && <SummaryView />}
    {capability === "verification" && <VerificationView />}
    {capability === "audit" && <AuditView />}
    {["risk", "explain", "consensus", "conditions"].includes(capability) && (
      <section className={styles.card}>
        <h2>{titles[capability][0]}</h2>
        {demo
          ? <Unavailable title="Live evaluation paused in demo mode">Switch to live mode to submit an evaluation. No fictional risk, explanation, consensus, or verification result is generated.</Unavailable>
          : <StructuredEvaluation capability={capability as "risk" | "explain" | "consensus" | "conditions"} />}
      </section>
    )}
  </>;
}

function ComplaintView({ demo }: { demo: boolean }) {
  const [selected, setSelected] = useState(0);
  const item = demoComplaints[selected];
  return <div className={styles.split}>
    <section className={styles.card}><h2>Complaint classification</h2><Unavailable title="Classification unavailable">Complaint submission and saved predictions are not connected. No complaint text will be submitted or saved.</Unavailable>
      <div className={styles.field}><label htmlFor="complaint-case">Case ID</label><input id="complaint-case" disabled placeholder="Case selection unavailable" /></div>
      <div className={styles.field}><label htmlFor="complaint-text">Original complaint · English</label><textarea id="complaint-text" disabled placeholder="Classification will be available when the service is connected." /></div>
      <Button disabled>Classify complaint</Button>
      <h3>Machine Prediction</h3><p className={styles.muted}>English-input oriented, closed-set and uncalibrated. The four class probabilities are advisory model outputs, not legal confidence or proof of an allegation. There is no “Other” class.</p>
      <h3>Officer review</h3><p>Review recording is unavailable. No assessment is labelled “Officer Reviewed”.</p>
    </section>
    <section className={styles.card}><h2>{demo ? "Fictional assessment history" : "Assessment history"}</h2>
      {demo ? <>
        <div className={styles.field}><label htmlFor="complaint-assessment">Assessment</label><select id="complaint-assessment" value={selected} onChange={event => setSelected(Number(event.target.value))}>{demoComplaints.map((record, index) => <option value={index} key={record.assessmentId}>{record.assessmentId} · {record.assessedAt.slice(0, 10)}</option>)}</select></div>
        <Facts values={[["Case ID", item.caseId], ["Assessment ID", item.assessmentId], ["Model version", item.modelVersion], ["Assessed at", formatGovernanceTime(item.assessedAt)], ["Officer-review state", "Unavailable"]]} />
        <h3>Original complaint</h3><p>{item.complaint}</p><h3>Machine Prediction · fictional example</h3><p>{item.predictedCategory}</p>
        <div className={styles.scroll}><table className={styles.table}><thead><tr><th scope="col">Category</th><th scope="col">Model probability</th></tr></thead><tbody>{complaintCategories.map((label, index) => <tr key={label}><th scope="row">{label}</th><td>{(item.probabilities[index] * 100).toFixed(0)}%</td></tr>)}</tbody></table></div>
        <p className={styles.muted}>Fixed presentation examples. These values were not produced by running a model or submitting your text.</p>
      </> : <Unavailable>Saved complaint assessments cannot currently be retrieved.</Unavailable>}
    </section>
  </div>;
}

function AnomalyView({ demo }: { demo: boolean }) {
  const item = demoAnomaly;
  return <div className={styles.split}>
    <section className={styles.card}><h2>Completed workflow assessment</h2><Unavailable title="Workflow assessment unavailable">Completed event history and stored anomaly assessments are not connected. A partial workflow cannot be submitted.</Unavailable>
      {demo && <><h3>Fictional completed-trace example</h3><Facts values={[["Case ID", item.caseId], ["Workflow-run ID", item.workflowRunId], ["Assessment ID", item.assessmentId], ["Evidence snapshot", item.evidenceVersion], ["Completion context", "Fictional completed trace"], ["Model version", item.modelVersion], ["Assessed at", formatGovernanceTime(item.assessedAt)]]} /></>}
      <h3>Rule-based findings</h3><p>Unavailable for this workflow run.</p><h3>Complaint predictions</h3><p>No linked complaint assessment for this workflow run. <Link href="/component-04/complaints">Open complaint intelligence</Link>.</p>
      <h3>Lease approval timing</h3><p>This is a separate assessment. <Link href="/component-04/timing">View the timing evidence required</Link>.</p>
    </section>
    <section className={styles.card}><h2>Statistical anomaly result</h2>
      {demo ? <><StatusBadge tone="warning">{item.flagged ? "Flagged for review" : "Not flagged by this model"}</StatusBadge><Facts values={[["Anomaly score", item.anomalyScore], ["Threshold", item.threshold], ["Flagged", String(item.flagged)]]} /><p className={styles.muted}>Illustrative rule: higher scores are more unusual; a score greater than the threshold is flagged.</p><h3>Feature values · fictional</h3><Facts values={Object.entries(item.features).map(([key, value]) => [key, value])} /></> : <Unavailable>No statistical result has been retrieved.</Unavailable>}
      <h3>Model limitations</h3><p>A synthetic-model assessment requires departmental validation. Statistical anomalies do not establish unsupported rejection, evidence access, officer misconduct, corruption or legal compliance.</p><p className={styles.muted}>The score is not a risk percentage or confidence. Individual features are observations, not causal explanations.</p>
    </section>
  </div>;
}

function ScreeningView({ demo }: { demo: boolean }) {
  return <div className={styles.split}>
    <section className={styles.card}><h2>Recorded evidence</h2><p className={styles.muted}>This screening is evidence based. It is separate from the Isolation Forest model.</p>
      {demo && <Facts values={[["Case ID", "DEMO-SCREENING-CASE-001"], ["Assessment ID", "DEMO-SCREENING-001"], ["Input version", "DEMO-EVIDENCE-v1"], ["Recorded at", formatGovernanceTime("2026-10-01T09:00:00Z")]]} />}
      <Checks items={screeningIndicators.map(([code, label]) => ({ label, status: demo ? "Missing" : "Unavailable", note: code }))} />
      <p className={styles.muted}>Only an authorized evidence process can verify these records. No verification controls are available here.</p>
    </section>
    <section className={styles.card}><h2>Screening outcome</h2>{demo ? <><StatusBadge>InsufficientInformation</StatusBadge><p>The fictional example has no supplied evidence. This is not a clear finding or an accusation.</p></> : <Unavailable>Recorded screening assessments are not connected.</Unavailable>}
      <h3>Commissioner referral</h3><Facts values={[["Referral policy decision", "Unavailable"], ["Delivery state", "Unavailable"], ["Referral ID", "Not supplied"], ["C3 acknowledgement", "Not received"], ["Normal workflow-run ID", "Not supplied"], ["Commissioner review-process ID", "Not supplied"]]} />
      <Unavailable title="Referral unavailable">No referral delivery or acknowledgement can be confirmed. ReviewRequired alone does not establish that a referral was created.</Unavailable>
      <p className={styles.muted}>The intended referral ends the exact normal workflow run and starts a separate Commissioner review process. Neither transition is confirmed here.</p>
    </section>
  </div>;
}

function TimingView() {
  return <div className={styles.split}><section className={styles.card}><h2>Timing evidence</h2><Facts values={[["Confirmed complete-proposal start", "Unavailable"], ["Expected applicant handover", "Unavailable"], ["Actual applicant handover", "Unavailable"], ["Ongoing-case assessment time", "Unavailable"], ["Evidence provenance", "Not supplied"]]} /></section>
    <section className={styles.card}><h2>Timing status</h2><Unavailable title="Timing cannot currently be assessed">Authoritative start and applicant-handover evidence are not connected.</Unavailable><p>The expectation is two calendar months. It is not 60 days, two working months or a proven statutory deadline.</p><p className={styles.muted}>Date conversion to Asia/Colombo, month-end handling and the outcome must come from the timing service. Lateness does not automatically trigger a referral.</p></section></div>;
}
function QueueView() {
  return <section className={styles.card}><h2>Commissioner review cases</h2><Unavailable title="Review queue unavailable">The Commissioner review queue and referral acknowledgements are not connected. No cases are represented as referred or ready for decision.</Unavailable>
    <div className={styles.scroll}><table className={styles.table}><thead><tr>{["Case", "Applicant", "Reason for review", "Risk", "Current stage", "Age"].map(label => <th scope="col" key={label}>{label}</th>)}</tr></thead><tbody><tr><td colSpan={6}>Queue availability is unknown; this does not mean the queue is empty.</td></tr></tbody></table></div>
    <p className={styles.muted}>The system does not make the Commissioner’s legal or administrative decision.</p><Button href="/component-04/commissioner/review" variant="secondary">Review evidence requirements</Button>
  </section>;
}
function DecisionView() {
  return <div className={styles.split}><section className={styles.card}><h2>Case governance summary</h2><Checks items={["Regulatory compliance", "Conflict detection", "Institutional verification", "Conditions", "Governance risk"].map(label => ({ label, status: "Unavailable" }))} /></section>
    <section className={styles.card}><h2>Evidence for decision</h2><Unavailable title="Decision recording unavailable">An authorized decision service and an acknowledged Commissioner review-process identity are required. No workflow action can be confirmed.</Unavailable>
      <Facts values={[["Case ID", "Not supplied"], ["Review-process ID", "Not supplied"], ["Referral ID", "Not supplied"], ["Human-reviewed findings", "Unavailable"], ["Authorized decision", "Not recorded here"]]} />
      <h3>Record authorized decision</h3><div className={styles.actions}>{["Continue processing", "Return for clarification", "Refer for independent review", "Legal review required"].map(label => <Button key={label} variant="secondary" disabled>{label}</Button>)}</div>
    </section></div>;
}
function SummaryView() {
  return <div className={styles.split}><section className={styles.card}><h2>Check progress</h2><Checks items={["Regulatory compliance", "Conflict detection", "Early evidence screening", "Institutional consensus", "Conditional verification", "Workflow statistical anomaly"].map(label => ({ label, status: "Unavailable" }))} /><Button href="/component-04/cases" variant="secondary">Inspect individual assessments</Button></section>
    <section className={styles.card}><h2>Why this status?</h2><Unavailable title="Final summary unavailable">No authoritative summary linking the case, workflow run and assessment snapshots has been retrieved. Separate examples cannot be combined into a final outcome.</Unavailable><h3>Evidence used</h3><Checks items={["Completed checks", "Pending checks", "Machine predictions", "Human-reviewed findings", "Referral delivery state", "Authorized decisions"].map(label => ({ label, status: "Unavailable" }))} /><div className={styles.actions}><Button disabled>Finish workflow and record details</Button></div><p className={styles.muted}>Final lease-decision and workflow execution remain with their authorized process.</p></section></div>;
}
function VerificationView() {
  return <div className={styles.split}><section className={styles.card}><h2>Governance record</h2><Unavailable title="Verification integration unavailable">No record has been verified. A displayed hash alone would not establish blockchain verification.</Unavailable><Facts values={[["Record identifier", "Unavailable"], ["Final governance status", "Unavailable"], ["Recorded authority", "Unavailable"], ["Decision recorded at", "Unavailable"]]} /></section>
    <section className={styles.card}><h2>Verification details</h2><Facts values={[["Record hash", "Unavailable"], ["Verification status", "Unavailable"], ["Ledger / transaction reference", "Unavailable"], ["Recorded timestamp", "Unavailable"], ["Verification timestamp", "Unavailable"]]} /><h3>Audit evidence</h3><p>No immutable audit event or verification receipt is available.</p><div className={styles.actions}><Button disabled>Verify record</Button><Button variant="secondary" disabled>Download verification receipt</Button></div></section></div>;
}
function AuditView() {
  return <section className={styles.card}>
    <h2>Audit records</h2>
    <Unavailable title="Audit history unavailable">The backend does not publish an audit-history read endpoint. No records have been loaded.</Unavailable>
    <div className={styles.scroll}>
      <table className={styles.table} aria-label="Governance audit history">
        <thead><tr><th scope="col">Record</th><th scope="col">Engine</th><th scope="col">Action</th><th scope="col">Status</th><th scope="col">Recorded at</th></tr></thead>
        <tbody><tr><td colSpan={5}>Audit history availability is unknown; this does not mean there are no audit records.</td></tr></tbody>
      </table>
    </div>
    <p className={styles.muted}>Audit history, receipt and verification queries exist internally but are not exposed through the ASP.NET browser API. A hash mismatch would indicate an integrity difference, not legal invalidity or misconduct.</p>
  </section>;
}
