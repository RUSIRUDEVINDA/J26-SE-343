"use client";
import React, { useState } from "react";
import { Button } from "@/shared/components/Button";
import { StatusBadge } from "@/shared/components/StatusBadge";
import { useGovernanceMode } from "./GovernanceWorkspace";
import { Unavailable, Facts, Checks } from "./GovernancePanels";
import { complaintCategories, demoComplaints, screeningIndicators } from "../services/demoEvidence";
import { formatGovernanceTime } from "../adapters/presentation";
import styles from "./GovernanceWorkspace.module.css";
import { StructuredEvaluation } from "./LiveGovernanceEvaluations";
import {
  classifyComplaint,
  screenEarlyGovernance,
  getAuditReceipt,
  verifyAuditIntegrity,
  describeGovernanceRequestError,
  parseEarlyScreeningSeedInput,
  type ComplaintClassificationResponse,
  type EarlyScreeningResponse,
  type AuditVerificationReceiptResponse,
} from "../services/governanceService";
import { WorkflowAnomalyJourney } from "./WorkflowAnomalyJourney";


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
    {capability === "verification" && <VerificationView demo={demo} />}
    {capability === "audit" && <AuditView demo={demo} />}
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
  const [caseId, setCaseId] = useState("CASE-COMPLAINT-001");
  const [complaintText, setComplaintText] = useState("The divisional secretariat officer accepted unauthorized payments directly without issuing formal government treasury receipts and bypassed gazetted valuation.");
  const [pending, setPending] = useState(false);
  const [result, setResult] = useState<ComplaintClassificationResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const item = demoComplaints[selected];

  async function handleClassify(e: React.FormEvent) {
    e.preventDefault();
    if (pending) return;
    setPending(true);
    setError(null);
    setResult(null);
    try {
      const res = await classifyComplaint({
        caseId: caseId.trim(),
        complaintText: complaintText.trim(),
      });
      setResult(res);
    } catch (err) {
      setError(describeGovernanceRequestError(err));
    } finally {
      setPending(false);
    }
  }

  return <div className={styles.split}>
    <section className={styles.card}>
      <h2>Complaint classification</h2>
      {demo ? (
        <Unavailable title="Live classification paused in demo mode">
          Switch to live mode to submit complaint text to the real trained Python classifier.
        </Unavailable>
      ) : (
        <form onSubmit={handleClassify}>
          <div className={styles.field}>
            <label htmlFor="complaint-case">Case ID</label>
            <input
              id="complaint-case"
              value={caseId}
              onChange={e => setCaseId(e.target.value)}
              required
              disabled={pending}
            />
          </div>
          <div className={styles.field}>
            <label htmlFor="complaint-text">Original complaint · English</label>
            <textarea
              id="complaint-text"
              value={complaintText}
              onChange={e => setComplaintText(e.target.value)}
              required
              disabled={pending}
            />
          </div>
          <Button type="submit" disabled={pending}>
            {pending ? "Classifying with ML…" : "Classify complaint"}
          </Button>
          {error && <div className={styles.notice} role="alert"><p>{error}</p></div>}
          {result && (
            <div aria-live="polite" style={{ marginTop: "1.2rem" }}>
              <h3>Machine Prediction · Real ML Output</h3>
              <StatusBadge tone="warning">{result.predictedCategory}</StatusBadge>
              <Facts
                values={[
                  ["Assessment ID", result.assessmentId],
                  ["Case ID", result.caseId],
                  ["Model version", result.modelVersion],
                  ["Assessed at", formatGovernanceTime(result.assessedAtUtc)],
                  ["Advisory Note", result.advisoryNote],
                  ["Closed-set note", result.closedSetNote],
                ]}
              />
              <h4>Original complaint</h4>
              <p>{result.complaintText}</p>
              <div className={styles.scroll}>
                <table className={styles.table}>
                  <thead>
                    <tr><th scope="col">Category</th><th scope="col">Model probability</th></tr>
                  </thead>
                  <tbody>
                    {complaintCategories.map(cat => (
                      <tr key={cat}>
                        <th scope="row">{cat}</th>
                        <td>{(result.classProbabilities[cat] * 100).toFixed(1)}% ({result.classProbabilities[cat].toFixed(4)})</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </form>
      )}
      <h3>Model Context</h3>
      <p className={styles.muted}>
        Trained logistic regression + TF-IDF model on frozen 4-class taxonomy. Probabilities are advisory machine predictions, not legal conclusions.
      </p>
    </section>
    <section className={styles.card}>
      <h2>{demo ? "Fictional assessment history" : "Assessment Reference"}</h2>
      {demo ? (
        <>
          <div className={styles.field}>
            <label htmlFor="complaint-assessment">Assessment</label>
            <select id="complaint-assessment" value={selected} onChange={event => setSelected(Number(event.target.value))}>
              {demoComplaints.map((record, index) => (
                <option value={index} key={record.assessmentId}>{record.assessmentId} · {record.assessedAt.slice(0, 10)}</option>
              ))}
            </select>
          </div>
          <Facts values={[["Case ID", item.caseId], ["Assessment ID", item.assessmentId], ["Model version", item.modelVersion], ["Assessed at", formatGovernanceTime(item.assessedAt)], ["Officer-review state", "Unavailable"]]} />
          <h3>Original complaint</h3><p>{item.complaint}</p>
          <h3>Machine Prediction · fictional example</h3><p>{item.predictedCategory}</p>
          <div className={styles.scroll}>
            <table className={styles.table}>
              <thead><tr><th scope="col">Category</th><th scope="col">Model probability</th></tr></thead>
              <tbody>{complaintCategories.map((label, index) => <tr key={label}><th scope="row">{label}</th><td>{(item.probabilities[index] * 100).toFixed(0)}%</td></tr>)}</tbody>
            </table>
          </div>
        </>
      ) : (
        <>
          <p>Successful live predictions are persisted by the Component 4 handler in PostgreSQL. The returned assessment ID above is the persisted identity; this screen does not invent one.</p>
          <div className={styles.actions}>
            <Button href="/component-04/cases" variant="secondary">View Live Case Dossiers</Button>
          </div>
        </>
      )}
    </section>
  </div>;
}

function AnomalyView({ demo }: { demo: boolean }) {
  if (demo) {
    return <Unavailable title="Live persisted journey paused in demo mode">
      Switch to Live workspace to retrieve a persisted projection and assessment. No mock fallback is used for this Commissioner-facing journey.
    </Unavailable>;
  }

  return <WorkflowAnomalyJourney allowCaseLookup />;
}

function ScreeningView({ demo }: { demo: boolean }) {
  const defaultScreeningJson = JSON.stringify({
    caseId: "SIM-C4-2026-001",
    inputVersion: "v1",
    indicators: [
      {
        indicatorType: "LegalDispute",
        evidenceState: "VerifiedAbsent",
        evidenceReference: "COURT-SEARCH-REF-2026-01",
        recordedAtUtc: new Date().toISOString()
      },
      {
        indicatorType: "UnauthorizedOccupation",
        evidenceState: "VerifiedAbsent",
        evidenceReference: "GRAMA-NILADHARI-REPORT-004",
        recordedAtUtc: new Date().toISOString()
      }
    ]
  }, null, 2);

  const [inputJson, setInputJson] = useState(defaultScreeningJson);
  const [pending, setPending] = useState(false);
  const [result, setResult] = useState<EarlyScreeningResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleScreen(e: React.FormEvent) {
    e.preventDefault();
    if (pending) return;
    setPending(true);
    setError(null);
    setResult(null);
    try {
      const parsed = parseEarlyScreeningSeedInput(JSON.parse(inputJson));
      const res = await screenEarlyGovernance({
        assessmentId: crypto.randomUUID(),
        workflowRunId: crypto.randomUUID(),
        caseId: parsed.caseId,
        inputVersion: parsed.inputVersion,
        indicators: parsed.indicators
      });
      setResult(res);
    } catch (err) {
      setError(describeGovernanceRequestError(err));
    } finally {
      setPending(false);
    }
  }

  return <div className={styles.split}>
    <section className={styles.card}>
      <h2>Early Governance Screening</h2>
      <p className={styles.muted}>Evaluates the 7 statutory governance concern indicators using deterministic rule engines and caller assertions.</p>
      {demo ? (
        <>
          <Facts values={[["Case ID", "DEMO-SCREENING-CASE-001"], ["Assessment ID", "DEMO-SCREENING-001"], ["Input version", "DEMO-EVIDENCE-v1"], ["Recorded at", formatGovernanceTime("2026-10-01T09:00:00Z")]]} />
          <Checks items={screeningIndicators.map(([code, label]) => ({ label, status: "Missing", note: code }))} />
        </>
      ) : (
        <form onSubmit={handleScreen}>
          <div className={styles.field}>
            <label htmlFor="screening-input-json">Screening Evidence JSON</label>
            <textarea
              id="screening-input-json"
              value={inputJson}
              onChange={e => setInputJson(e.target.value)}
              required
              disabled={pending}
              style={{ minHeight: "180px", fontFamily: "monospace", fontSize: "0.8rem" }}
            />
          </div>
          <Button type="submit" disabled={pending}>
            {pending ? "Screening Indicators…" : "Run Early Governance Screening"}
          </Button>
          {error && <div className={styles.notice} role="alert"><p>{error}</p></div>}
        </form>
      )}
    </section>
    <section className={styles.card}>
      <h2>Screening outcome</h2>
      {demo ? (
        <>
          <StatusBadge>InsufficientInformation</StatusBadge>
          <p>The fictional example has no supplied evidence. This is not a clear finding or an accusation.</p>
        </>
      ) : result ? (
        <div aria-live="polite">
          <StatusBadge tone={result.overallStatus === "Clear" ? "success" : "warning"}>
            {result.overallStatus}
          </StatusBadge>
          <Facts
            values={[
              ["Case ID", result.caseId],
              ["Input Version", result.inputVersion],
              ["Overall Status", result.overallStatus],
              ["Incomplete Evidence", result.hasIncompleteEvidence ? "Yes" : "No"],
            ]}
          />
          <h3>Evaluated Indicators</h3>
          <div className={styles.scroll}>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th scope="col">Indicator</th>
                  <th scope="col">Evidence State</th>
                  <th scope="col">Review Required</th>
                  <th scope="col">Reason / Message</th>
                </tr>
              </thead>
              <tbody>
                {result.indicatorResults.map(item => (
                  <tr key={item.indicatorType}>
                    <th scope="row">{item.indicatorType}</th>
                    <td>{item.evidenceState}</td>
                    <td>{item.requiresReview ? "Review Required" : item.needsEvidence ? "Evidence Needed" : "Clear"}</td>
                    <td>{item.message}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Unavailable title="Referral delivery not confirmed">This is a real Component 4 screening result. The response does not confirm a Component 3 Commissioner handoff or acknowledgement, so no successful referral is claimed.</Unavailable>
        </div>
      ) : (
        <Unavailable title="Awaiting screening">
          Submit the screening evidence payload to evaluate the seven indicators through the .NET screening engine.
        </Unavailable>
      )}
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

function VerificationView({ demo }: { demo: boolean }) {
  const [recordId, setRecordId] = useState("818f49f5-d954-4a4a-9305-a18133d1e2a2");
  const [pending, setPending] = useState(false);
  const [receipt, setReceipt] = useState<AuditVerificationReceiptResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleVerify(e: React.FormEvent) {
    e.preventDefault();
    if (pending) return;
    setPending(true);
    setError(null);
    setReceipt(null);
    try {
      const res = await verifyAuditIntegrity(recordId.trim());
      setReceipt(res);
    } catch (err) {
      setError(describeGovernanceRequestError(err));
    } finally {
      setPending(false);
    }
  }

  return <div className={styles.split}>
    <section className={styles.card}>
      <h2>Blockchain Verification</h2>
      <p className={styles.muted}>Verifies off-chain audit record canonical hash against the immutable Hyperledger Besu QBFT smart contract.</p>
      {demo ? (
        <Unavailable title="Verification paused in demo mode">
          Switch to live mode to verify an authentic audit record on the blockchain.
        </Unavailable>
      ) : (
        <form onSubmit={handleVerify}>
          <div className={styles.field}>
            <label htmlFor="verify-record-id">Audit Record GUID</label>
            <input
              id="verify-record-id"
              value={recordId}
              onChange={e => setRecordId(e.target.value)}
              required
              disabled={pending}
            />
          </div>
          <Button type="submit" disabled={pending}>
            {pending ? "Verifying on Ledger…" : "Verify Record on Blockchain"}
          </Button>
          {error && <div className={styles.notice} role="alert"><p>{error}</p></div>}
        </form>
      )}
      <h3>Trust Architecture</h3>
      <p className={styles.muted}>
        Hyperledger Besu (Chain ID 1337). Off-chain data remains private; only canonical SHA-256 digests are anchored and verified.
      </p>
    </section>
    <section className={styles.card}>
      <h2>Verification details</h2>
      {demo ? (
        <Facts values={[["Record hash", "Unavailable"], ["Verification status", "Unavailable"], ["Ledger / transaction reference", "Unavailable"], ["Recorded timestamp", "Unavailable"], ["Verification timestamp", "Unavailable"]]} />
      ) : receipt ? (
        <div aria-live="polite">
          <StatusBadge tone={receipt.verificationStatus === "Match" ? "success" : "warning"}>
            {receipt.verificationStatus}
          </StatusBadge>
          <Facts
            values={[
              ["Record ID", receipt.recordId],
              ["Engine Type", receipt.engineType],
              ["Action Name", receipt.actionName],
              ["Canonical Hash", receipt.recordHash],
              ["Anchor Status", receipt.anchorStatus],
              ["Verification Status", receipt.verificationStatus],
              ["Ledger / transaction reference", receipt.ledgerTransactionReference ?? "N/A"],
              ["Block Number", receipt.blockNumber !== null ? String(receipt.blockNumber) : "N/A"],
              ["Anchored At", receipt.anchoredAtUtc ? formatGovernanceTime(receipt.anchoredAtUtc) : "N/A"],
              ["Verified At", receipt.verifiedAtUtc ? formatGovernanceTime(receipt.verifiedAtUtc) : "N/A"],
              ["Explanation", receipt.explanation],
            ]}
          />
        </div>
      ) : (
        <Unavailable title="Awaiting verification">
          Enter an audit record ID on the left and submit to query the live smart contract.
        </Unavailable>
      )}
    </section>
  </div>;
}

function AuditView({ demo }: { demo: boolean }) {
  const [recordId, setRecordId] = useState("818f49f5-d954-4a4a-9305-a18133d1e2a2");
  const [pending, setPending] = useState(false);
  const [receipt, setReceipt] = useState<AuditVerificationReceiptResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleFetchReceipt(e: React.FormEvent) {
    e.preventDefault();
    if (pending) return;
    setPending(true);
    setError(null);
    setReceipt(null);
    try {
      const res = await getAuditReceipt(recordId.trim());
      setReceipt(res);
    } catch (err) {
      setError(describeGovernanceRequestError(err));
    } finally {
      setPending(false);
    }
  }

  return <section className={styles.card}>
    <h2>Audit Record Receipt</h2>
    <p className={styles.muted}>Retrieves cryptographic digest and anchor state for any recorded governance action.</p>
    {demo ? (
      <Unavailable title="Audit lookup paused in demo mode">
        Switch to live mode to inspect authentic audit records.
      </Unavailable>
    ) : (
      <form onSubmit={handleFetchReceipt} style={{ marginBottom: "1.5rem" }}>
        <div className={styles.field}>
          <label htmlFor="audit-record-id">Audit Record GUID</label>
          <input
            id="audit-record-id"
            value={recordId}
            onChange={e => setRecordId(e.target.value)}
            required
            disabled={pending}
          />
        </div>
        <Button type="submit" disabled={pending}>
          {pending ? "Fetching Receipt…" : "Retrieve Audit Receipt"}
        </Button>
        {error && <div className={styles.notice} role="alert"><p>{error}</p></div>}
      </form>
    )}
    {receipt && (
      <div aria-live="polite">
        <StatusBadge tone={receipt.anchorStatus === "Anchored" ? "success" : "neutral"}>
          {receipt.anchorStatus}
        </StatusBadge>
        <Facts
          values={[
            ["Record ID", receipt.recordId],
            ["Engine Type", receipt.engineType],
            ["Action Name", receipt.actionName],
            ["Governance Status", receipt.governanceStatus],
            ["Decision Recorded At", formatGovernanceTime(receipt.decisionRecordedAt)],
            ["Canonical Hash", receipt.recordHash],
            ["Anchor Status", receipt.anchorStatus],
            ["Verification Status", receipt.verificationStatus],
            ["Ledger / transaction reference", receipt.ledgerTransactionReference ?? "N/A"],
            ["Block Number", receipt.blockNumber !== null ? String(receipt.blockNumber) : "N/A"],
            ["Anchored At", receipt.anchoredAtUtc ? formatGovernanceTime(receipt.anchoredAtUtc) : "N/A"],
            ["Verified At", receipt.verifiedAtUtc ? formatGovernanceTime(receipt.verifiedAtUtc) : "N/A"],
            ["Explanation", receipt.explanation],
          ]}
        />
      </div>
    )}
  </section>;
}

