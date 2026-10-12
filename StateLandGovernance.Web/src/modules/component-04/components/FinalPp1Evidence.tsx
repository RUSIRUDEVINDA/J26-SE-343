"use client";

import { useEffect, useState } from "react";
import {
  getAuditHistoryByCase, getAuditReceipt, getComplaintHistoryByCase,
  getLeaseTimingHistoryByCase, getReferralHistoryByCase, getScreeningHistoryByCase,
  getWorkflowAnomalyHistoryByCase, type AuditCaseHistoryItem,
  type AuditVerificationReceiptResponse, type PersistedHistoryItem,
} from "../services/governanceService";
import { formatGovernanceTime } from "../adapters/presentation";
import styles from "./GovernanceWorkspace.module.css";

const finalCases = [
  "PP1-FINAL-CLEAR-001", "PP1-FINAL-REFERRAL-001", "PP1-FINAL-DELAYED-001",
  "PP1-FINAL-COMPLAINT-001", "PP1-FINAL-COMBINED-001",
] as const;

type Section = { status: "available" | "empty" | "error"; items: PersistedHistoryItem[]; message?: string };
type Dossier = { screening: Section; complaint: Section; anomaly: Section; timing: Section; referral: Section; audit: Section; auditItem?: AuditCaseHistoryItem; receipt?: AuditVerificationReceiptResponse };

const empty = (): Section => ({ status: "empty", items: [] });
const text = (item: PersistedHistoryItem | undefined, key: string) => item?.[key] == null ? "Unavailable" : String(item[key]);
const nested = (item: PersistedHistoryItem | undefined, key: string) =>
  item?.[key] && typeof item[key] === "object" ? item[key] as Record<string, unknown> : undefined;
const timingState = (value: unknown) => ({ 1: "Within expectation", 2: "Exceeded expectation", 3: "Ongoing / within expectation", 4: "Ongoing / exceeded expectation", 5: "Unavailable" }[Number(value)] ?? "Unavailable");
const referralState = (value: unknown) => ({ 1: "Pending", 2: "Delivering", 3: "Delivery failed", 4: "Acknowledged" }[Number(value)] ?? "Unavailable");

async function loadCase(caseId: string): Promise<Dossier> {
  const calls = [getScreeningHistoryByCase, getComplaintHistoryByCase, getWorkflowAnomalyHistoryByCase, getLeaseTimingHistoryByCase, getReferralHistoryByCase] as const;
  const results = await Promise.allSettled(calls.map(call => call(caseId, 10)));
  const section = (index: number): Section => results[index].status === "fulfilled"
    ? { status: results[index].value.length ? "available" : "empty", items: results[index].value }
    : { status: "error", items: [], message: "Live endpoint unavailable for this section." };
  const dossier: Dossier = { screening: section(0), complaint: section(1), anomaly: section(2), timing: section(3), referral: section(4), audit: empty() };
  try {
    const audits = await getAuditHistoryByCase(caseId, 10);
    dossier.audit = { status: audits.length ? "available" : "empty", items: audits as unknown as PersistedHistoryItem[] };
    dossier.auditItem = audits[0];
    if (audits[0]) dossier.receipt = await getAuditReceipt(audits[0].auditRecordId);
  } catch { dossier.audit = { status: "error", items: [], message: "Audit history is unavailable." }; }
  return dossier;
}

function Missing({ section, label }: { section: Section; label: string }) {
  if (section.status === "error") return <p role="status">{section.message}</p>;
  if (section.status === "empty") return <p>{label}</p>;
  return null;
}

export function FinalPp1Evidence() {
  const [data, setData] = useState<Record<string, Dossier>>({});
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    let active = true;
    Promise.allSettled(finalCases.map(async caseId => [caseId, await loadCase(caseId)] as const)).then(results => {
      if (!active) return;
      const next: Record<string, Dossier> = {};
      for (const result of results) if (result.status === "fulfilled") next[result.value[0]] = result.value[1];
      setData(next);
      if (Object.keys(next).length !== finalCases.length) setError("One or more final dossiers could not be loaded; available evidence remains visible.");
    });
    return () => { active = false; };
  }, []);

  return <section className={styles.card} aria-labelledby="pp1-final-title">
    <h2 id="pp1-final-title">PP1 Final Seed Scenarios</h2>
    <p className={styles.muted}>Synthetic PP1 Seed · pp1-seed-contract-v2 · PP1 Demonstration</p>
    <p>Composite of latest available Component 4 assessments</p>
    <p className={styles.notice}>Synthetic upstream C1/C3 inputs; real Component 4 calculations, PostgreSQL persistence and ML inference. Component 3 termination was not executed. Besu evidence is shown only where persisted.</p>
    {error && <p role="alert">{error}</p>}
    {!Object.keys(data).length && <p role="status">Loading final PP1 evidence…</p>}
    <div className={styles.stack}>{finalCases.map(caseId => {
      const dossier = data[caseId]; if (!dossier) return null;
      const screening = dossier.screening.items[0]; const screeningResult = nested(screening, "result");
      const complaint = dossier.complaint.items[0]; const anomaly = dossier.anomaly.items[0]; const timing = dossier.timing.items[0]; const referral = dossier.referral.items[0];
      const probabilities = nested(complaint, "classProbabilities"); const features = nested(anomaly, "featureValues");
      const indicators = screeningResult && Array.isArray(screeningResult.indicatorResults) ? screeningResult.indicatorResults as PersistedHistoryItem[] : [];
      return <details key={caseId} className={styles.card} data-testid={`final-${caseId}`}>
        <summary><strong>{caseId}</strong> — {screeningResult ? String(screeningResult.overallStatus) : "Evidence status unavailable"}</summary>
        <p className={styles.muted}>Synthetic PP1 Seed · pp1-seed-contract-v2 · PP1 Demonstration</p>
        <section><h3>Screening</h3><Missing section={dossier.screening} label="No screening assessment is available." />{screening && <><p>Status: {screeningResult ? String(screeningResult.overallStatus) : "Unavailable"}</p><p>Assessment: {text(screening, "assessmentId")} · {formatGovernanceTime(text(screening, "createdAtUtc"))}</p><p>Incomplete evidence: {screeningResult ? String(screeningResult.hasIncompleteEvidence) : "Unavailable"}</p>{indicators.length > 0 && <ul>{indicators.map((indicator, index) => <li key={`${text(indicator, "indicatorType")}-${index}`}>{text(indicator, "indicatorType")}: {text(indicator, "evidenceState")} · {text(indicator, "evidenceReference")}</li>)}</ul>}</>}</section>
        <section><h3>Complaint — Machine Prediction</h3><Missing section={dossier.complaint} label="Not Applicable — no persisted complaint assessment for this final scenario." />{complaint && <><p>Fictional / synthetic complaint · Model {text(complaint, "modelVersion")} · {text(complaint, "predictedCategory")}</p><p>Assessment: {text(complaint, "assessmentId")} · {formatGovernanceTime(text(complaint, "assessedAtUtc"))}</p>{probabilities && <ul>{Object.entries(probabilities).map(([key, value]) => <li key={key}>{key}: {(Number(value) * 100).toFixed(2)}% model probability</li>)}</ul>}<p>{text(complaint, "advisoryNote")}</p><p>{text(complaint, "closedSetNote")}</p></>}</section>
        <section><h3>Workflow anomaly</h3><Missing section={dossier.anomaly} label="Not Eligible — no persisted completed-trace assessment is available." />{anomaly && <><p>{text(anomaly, "modelVersion")} · Score {text(anomaly, "anomalyScore")} · Threshold {text(anomaly, "threshold")} · Flagged {text(anomaly, "flagged")}</p><p>Assessment: {text(anomaly, "assessmentId")} · Workflow run: {text(anomaly, "workflowRunId")} · {formatGovernanceTime(text(anomaly, "assessedAtUtc"))}</p>{features && <ul>{Object.entries(features).map(([key, value]) => <li key={key}>{key}: {String(value)}</li>)}</ul>}<p>Statistical workflow unusualness is separate from deterministic screening and is not a probability or finding of misconduct.</p></>}</section>
        <section><h3>Timing</h3><Missing section={dossier.timing} label="Timing evidence is unavailable." />{timing && <p>Roadmap {text(timing, "completeProposalRoadmapStartDate")} · Expected {text(timing, "expectedApprovalHandoverDate")} · Actual/assessment {text(timing, "approvalHandoverDate")} / {text(timing, "assessmentDateColombo")} · Asia/Colombo · Status {timingState(timing.timingState)} · {text(timing, "daysBeyondExpected")} days beyond expectation · Policy {text(timing, "policyVersion")} · Assessment {text(timing, "assessmentId")} · Source projection {text(timing, "workflowProjectionId")}</p>}</section>
        <section><h3>Referral</h3><Missing section={dossier.referral} label="Not Required — no persisted referral exists for this case." />{referral && <><p>Referral {text(referral, "referralId")} · State {referralState(referral.deliveryState)} · NormalFlowExitRequested · SyntheticPp1Handoff · {text(referral, "commissionerReviewProcessReference")} · {formatGovernanceTime(text(referral, "acknowledgedAtUtc"))}</p><p><strong>Component 4 requested normal-flow exit and received a synthetic PP1 handoff acknowledgement. Actual Component 3 workflow termination was not executed.</strong></p></>}</section>
        <section><h3>Audit / blockchain</h3><Missing section={dossier.audit} label="No audited operation is available for this case." />{dossier.auditItem && dossier.receipt && <><p>Audit {dossier.auditItem.auditRecordId} · {dossier.auditItem.operationType} · {dossier.receipt.recordVersion} · {dossier.receipt.anchorStatus}</p><p>Last Verification Status: {dossier.receipt.verificationStatus} · Last Verified At: {formatGovernanceTime(dossier.receipt.verifiedAtUtc ?? "")}</p><p>Block {dossier.receipt.blockNumber ?? "Unavailable"}</p><p style={{ overflowWrap: "anywhere" }}>Transaction: {dossier.receipt.ledgerTransactionReference ?? "Unavailable"}<br />Stored hash: {dossier.receipt.recordHash}</p></>}</section>
      </details>;
    })}</div>
  </section>;
}
