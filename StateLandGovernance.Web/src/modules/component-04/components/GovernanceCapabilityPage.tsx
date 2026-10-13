"use client";
import React, { useEffect, useState } from "react";
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
  getScreeningHistoryByCase,
  getComplaintHistoryByCase,
  getWorkflowAnomalyHistoryByCase,
  getLeaseTimingHistoryByCase,
  getReferralHistoryByCase,
  getAuditHistoryByCase,
  describeGovernanceRequestError,
  parseEarlyScreeningSeedInput,
  type ComplaintClassificationResponse,
  type EarlyScreeningResponse,
  type AuditVerificationReceiptResponse,
  type PersistedHistoryItem,
  type AuditCaseHistoryItem,
} from "../services/governanceService";
import {
  getComplaintPreset,
  getScreeningPresetJson,
  PROPOSAL_PRESET_LIST,
  PROPOSAL_PRESETS,
  type ProposalPresetKey,
} from "../services/proposalPresets";
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

function ProposalPresetSelector({
  selected,
  onSelect,
  disabled = false,
}: {
  selected: ProposalPresetKey;
  onSelect: (key: ProposalPresetKey) => void;
  disabled?: boolean;
}) {
  const active = PROPOSAL_PRESETS[selected];
  return (
    <div className={styles.notice} style={{ marginBottom: "1rem" }}>
      <strong>Select a Proposal Example:</strong>
      <div className={styles.actions} style={{ marginTop: "0.5rem" }}>
        {PROPOSAL_PRESET_LIST.map(preset => (
          <Button
            key={preset.key}
            type="button"
            variant={selected === preset.key ? "primary" : "secondary"}
            onClick={() => onSelect(preset.key)}
            disabled={disabled}
          >
            {preset.shortLabel}
          </Button>
        ))}
      </div>
      <p className={styles.muted} style={{ marginTop: "0.5rem", marginBottom: 0 }}>
        <strong>{active.title}:</strong> {active.summary}
      </p>
    </div>
  );
}

export function GovernanceCapabilityPage({ capability }: { capability: Capability }) {
  const demo = useGovernanceMode() === "demo";
  return <>
    <header className={styles.heading}><h1>{titles[capability][0]}</h1><p>{titles[capability][1]}</p></header>
    {capability === "complaints" && <ComplaintView demo={demo} />}
    {capability === "anomalies" && <AnomalyView demo={demo} />}
    {capability === "screening" && <ScreeningView demo={demo} />}
    {capability === "timing" && <TimingView key={`timing-${demo}`} demo={demo} />}
    {capability === "commissioner" && <QueueView key={`commissioner-${demo}`} demo={demo} />}
    {capability === "decision-review" && <DecisionView key={`decision-review-${demo}`} demo={demo} />}
    {capability === "summary" && <SummaryView key={`summary-${demo}`} demo={demo} />}
    {capability === "verification" && <VerificationView demo={demo} />}
    {capability === "audit" && <AuditView demo={demo} />}
    {["risk", "explain", "consensus", "conditions"].includes(capability) && (
      <section className={styles.card}>
        <h2>{titles[capability][0]}</h2>
        {demo
          ? <Unavailable title="Live evaluation paused in demo mode">Switch to live mode to submit an evaluation. No fictional risk, explanation, consensus, or verification result is generated.</Unavailable>
          : <StructuredEvaluation key={capability} capability={capability as "risk" | "explain" | "consensus" | "conditions"} />}
      </section>
    )}
  </>;
}

function ComplaintView({ demo }: { demo: boolean }) {
  const [selected, setSelected] = useState(0);
  const [presetKey, setPresetKey] = useState<ProposalPresetKey>("proposal-3");
  const initial = getComplaintPreset("proposal-3");
  const [caseId, setCaseId] = useState(initial.caseId);
  const [complaintText, setComplaintText] = useState(initial.complaintText);
  const [pending, setPending] = useState(false);
  const [result, setResult] = useState<ComplaintClassificationResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const item = demoComplaints[selected];

  function selectPreset(nextKey: ProposalPresetKey) {
    const preset = getComplaintPreset(nextKey);
    setPresetKey(nextKey);
    setCaseId(preset.caseId);
    setComplaintText(preset.complaintText);
    setResult(null);
    setError(null);
  }

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
        <>
          <ProposalPresetSelector selected={presetKey} onSelect={selectPreset} disabled={pending} />
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
        </>
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
  const [presetKey, setPresetKey] = useState<ProposalPresetKey>("proposal-1");
  if (demo) {
    return <Unavailable title="Live persisted journey paused in demo mode">
      Switch to Live workspace to retrieve a persisted projection and assessment. No mock fallback is used for this Commissioner-facing journey.
    </Unavailable>;
  }

  const activeCaseId = PROPOSAL_PRESETS[presetKey].anomalyCaseId;
  return <>
    <ProposalPresetSelector selected={presetKey} onSelect={setPresetKey} />
    <WorkflowAnomalyJourney key={activeCaseId} initialCaseId={activeCaseId} />
  </>;
}

function ScreeningView({ demo }: { demo: boolean }) {
  const [presetKey, setPresetKey] = useState<ProposalPresetKey>("proposal-1");
  const [inputJson, setInputJson] = useState(() => getScreeningPresetJson("proposal-1"));
  const [pending, setPending] = useState(false);
  const [result, setResult] = useState<EarlyScreeningResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  function selectPreset(nextKey: ProposalPresetKey) {
    setPresetKey(nextKey);
    setInputJson(getScreeningPresetJson(nextKey));
    setResult(null);
    setError(null);
  }

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
        <>
          <ProposalPresetSelector selected={presetKey} onSelect={selectPreset} disabled={pending} />
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
        </>
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

const timingLabel = (value: unknown) =>
  ({
    1: "WithinExpectation (<= 2 calendar months)",
    2: "ExceededExpectation (> 2 calendar months)",
    3: "BasedOnAvailableEvidence (Ongoing / within expectation)",
    4: "BasedOnAvailableEvidence (Ongoing / exceeded expectation)",
    5: "Unavailable",
  }[Number(value)] ?? String(value ?? "Unavailable"));

const referralLabel = (value: unknown) =>
  ({
    1: "Pending",
    2: "Delivering",
    3: "DeliveryFailed",
    4: "Acknowledged",
  }[Number(value)] ?? String(value ?? "Unavailable"));

function TimingView({ demo }: { demo: boolean }) {
  const [presetKey, setPresetKey] = useState<ProposalPresetKey>("proposal-3");
  const [item, setItem] = useState<PersistedHistoryItem | null>(null);
  const [loading, setLoading] = useState(!demo);

  const activeCaseId = PROPOSAL_PRESETS[presetKey].caseId;

  useEffect(() => {
    if (demo) {
      return;
    }
    let active = true;
    getLeaseTimingHistoryByCase(activeCaseId, 5)
      .then(items => {
        if (active) setItem(items[0] ?? null);
      })
      .catch(() => {
        if (active) setItem(null);
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [activeCaseId, demo]);

  const visibleItem = demo || loading ? null : item;
  const str = (key: string) => (visibleItem?.[key] == null ? "Unavailable" : String(visibleItem[key]));

  return <>
    {!demo && <ProposalPresetSelector selected={presetKey} onSelect={key => { setLoading(true); setPresetKey(key); }} disabled={loading} />}
    <div className={styles.split}>
      <section className={styles.card}>
        <h2>Timing evidence ({demo ? "Demo mode" : activeCaseId})</h2>
        <Facts
          values={[
            ["Case ID", demo ? "Unavailable" : str("caseId")],
            ["Confirmed complete-proposal start", demo ? "Unavailable" : str("completeProposalRoadmapStartDate")],
            ["Expected applicant handover (2 calendar months)", demo ? "Unavailable" : str("expectedApprovalHandoverDate")],
            ["Actual applicant handover", demo ? "Unavailable" : str("approvalHandoverDate")],
            ["Ongoing-case assessment date (Asia/Colombo)", demo ? "Unavailable" : str("assessmentDateColombo")],
            ["Elapsed calendar days", demo ? "Unavailable" : str("elapsedCalendarDays")],
            ["Days beyond expected", demo ? "Unavailable" : str("daysBeyondExpected")],
            ["Source workflow projection", demo ? "Not supplied" : str("workflowProjectionId")],
          ]}
        />
      </section>
      <section className={styles.card}>
        <h2>Timing status</h2>
        {!demo && visibleItem ? (
          <>
            <StatusBadge tone={Number(visibleItem.timingState) === 1 ? "success" : "warning"}>
              {timingLabel(visibleItem.timingState)}
            </StatusBadge>
            <Facts
              values={[
                ["Assessment ID", str("assessmentId")],
                ["Policy version", str("policyVersion")],
                ["Calculated at", formatGovernanceTime(str("calculatedAtUtc"))],
              ]}
            />
          </>
        ) : (
          <Unavailable title="Timing cannot currently be assessed">
            Authoritative start and applicant-handover evidence are not connected.
          </Unavailable>
        )}
        <p>The expectation is two calendar months. It is not 60 days, two working months or a proven statutory deadline.</p>
        <p className={styles.muted}>Date conversion to Asia/Colombo, month-end handling and the outcome must come from the timing service. Lateness does not automatically trigger a referral.</p>
      </section>
    </div>
  </>;
}

function QueueView({ demo }: { demo: boolean }) {
  const [rows, setRows] = useState<Array<{ preset: typeof PROPOSAL_PRESET_LIST[number]; referral?: PersistedHistoryItem; screeningStatus: string }>>([]);
  const [loading, setLoading] = useState(!demo);

  useEffect(() => {
    if (demo) {
      return;
    }
    let active = true;
    Promise.all(
      PROPOSAL_PRESET_LIST.map(async preset => {
        try {
          const [referrals, screenings] = await Promise.all([
            getReferralHistoryByCase(preset.caseId, 5),
            getScreeningHistoryByCase(preset.caseId, 5),
          ]);
          const scrResult = screenings[0]?.result as Record<string, unknown> | undefined;
          return {
            preset,
            referral: referrals[0],
            screeningStatus: scrResult?.overallStatus ? String(scrResult.overallStatus) : "Unavailable",
          };
        } catch {
          return { preset, referral: undefined, screeningStatus: "Unavailable" };
        }
      }),
    )
      .then(result => {
        if (active) setRows(result);
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [demo]);

  const visibleRows = demo || loading ? [] : rows;
  const referredRows = visibleRows.filter(r => r.referral);

  return <section className={styles.card}>
    <h2>Commissioner review cases</h2>
    {demo || referredRows.length === 0 ? (
      <Unavailable title="Review queue unavailable">
        The Commissioner review queue and referral acknowledgements are not connected. No cases are represented as referred or ready for decision.
      </Unavailable>
    ) : (
      <div className={styles.notice} style={{ marginBottom: "1rem" }}>
        <strong>Persisted Component 4 Referral Records (PostgreSQL):</strong> Showing {referredRows.length} referred PP1 proposals with acknowledged <code>SyntheticPp1Handoff</code> records alongside non-referred baseline Proposal 1. Actual Component 3 workflow termination is not executed by Component 4.
      </div>
    )}
    <div className={styles.scroll}>
      <table className={styles.table}>
        <thead>
          <tr>{["Case", "Applicant / Provenance", "Reason for review", "Screening", "Current referral state", "Handoff reference"].map(label => <th scope="col" key={label}>{label}</th>)}</tr>
        </thead>
        <tbody>
          {!demo && visibleRows.length > 0 ? (
            visibleRows.map(({ preset, referral, screeningStatus }) => (
              <tr key={preset.caseId}>
                <th scope="row">{preset.caseId}</th>
                <td>SyntheticPp1Seed · pp1-seed-contract-v2</td>
                <td>{referral ? "Early screening ReviewRequired (NormalFlowExitRequested)" : "Not referred (Screening Clear)"}</td>
                <td>{screeningStatus}</td>
                <td>{referral ? referralLabel(referral.deliveryState) : "Not Required"}</td>
                <td>{referral ? String(referral.commissionerReviewProcessReference ?? referral.referralId) : "—"}</td>
              </tr>
            ))
          ) : (
            <tr><td colSpan={6}>{loading ? "Loading persisted referral records…" : "Queue availability is unknown; this does not mean the queue is empty."}</td></tr>
          )}
        </tbody>
      </table>
    </div>
    <p className={styles.muted}>The system does not make the Commissioner’s legal or administrative decision.</p>
    <Button href="/component-04/commissioner/review" variant="secondary">Review evidence requirements</Button>
  </section>;
}

function DecisionView({ demo }: { demo: boolean }) {
  const [presetKey, setPresetKey] = useState<ProposalPresetKey>("proposal-3");
  const [dossier, setDossier] = useState<{
    screening?: PersistedHistoryItem;
    referral?: PersistedHistoryItem;
    complaint?: PersistedHistoryItem;
    anomaly?: PersistedHistoryItem;
    timing?: PersistedHistoryItem;
  }>({});
  const [loading, setLoading] = useState(!demo);

  const activeCaseId = PROPOSAL_PRESETS[presetKey].caseId;

  useEffect(() => {
    if (demo) {
      return;
    }
    let active = true;
    Promise.allSettled([
      getScreeningHistoryByCase(activeCaseId, 5),
      getReferralHistoryByCase(activeCaseId, 5),
      getComplaintHistoryByCase(activeCaseId, 5),
      getWorkflowAnomalyHistoryByCase(activeCaseId, 5),
      getLeaseTimingHistoryByCase(activeCaseId, 5),
    ])
      .then(results => {
        if (!active) return;
        const pick = (idx: number) => (results[idx].status === "fulfilled" ? results[idx].value[0] : undefined);
        setDossier({
          screening: pick(0),
          referral: pick(1),
          complaint: pick(2),
          anomaly: pick(3),
          timing: pick(4),
        });
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [activeCaseId, demo]);

  const visibleDossier = demo || loading ? {} : dossier;
  const scrResult = visibleDossier.screening?.result as Record<string, unknown> | undefined;
  const screeningStatus = scrResult?.overallStatus ? String(scrResult.overallStatus) : "Unavailable";
  const hasPersisted = Boolean(visibleDossier.screening || visibleDossier.referral || visibleDossier.complaint || visibleDossier.anomaly || visibleDossier.timing);

  return <>
    {!demo && <ProposalPresetSelector selected={presetKey} onSelect={key => { setLoading(true); setPresetKey(key); }} disabled={loading} />}
    <div className={styles.split}>
      <section className={styles.card}>
        <h2>Case governance summary ({demo ? "Demo mode" : activeCaseId})</h2>
        <Checks
          items={
            !demo && hasPersisted
              ? [
                  { label: "Early governance screening", status: screeningStatus, note: visibleDossier.screening ? String(visibleDossier.screening.assessmentId) : undefined },
                  { label: "Commissioner referral handoff", status: visibleDossier.referral ? referralLabel(visibleDossier.referral.deliveryState) : "NotRequired" },
                  { label: "Complaint ML classification", status: visibleDossier.complaint ? String(visibleDossier.complaint.predictedCategory) : "NotApplicable" },
                  { label: "Workflow anomaly ML (v1)", status: visibleDossier.anomaly ? (visibleDossier.anomaly.flagged ? "Flagged for review" : "No statistical escalation") : "NotEligible" },
                  { label: "Lease approval timing", status: visibleDossier.timing ? timingLabel(visibleDossier.timing.timingState) : "Unavailable" },
                ]
              : ["Regulatory compliance", "Conflict detection", "Institutional verification", "Conditions", "Governance risk"].map(label => ({ label, status: "Unavailable" }))
          }
        />
      </section>
      <section className={styles.card}>
        <h2>Evidence for decision</h2>
        <Unavailable title="Decision recording unavailable">
          An authorized decision service and an acknowledged Commissioner review-process identity are required. No workflow action can be confirmed.
        </Unavailable>
        <Facts
          values={[
            ["Case ID", !demo && hasPersisted ? activeCaseId : "Not supplied"],
            ["Review-process ID", !demo && visibleDossier.referral ? String(visibleDossier.referral.commissionerReviewProcessReference ?? "Unavailable") : "Not supplied"],
            ["Referral ID", !demo && visibleDossier.referral ? String(visibleDossier.referral.referralId) : "Not supplied"],
            ["Human-reviewed findings", "Unavailable"],
            ["Authorized decision", "Not recorded here"],
          ]}
        />
        <h3>Record authorized decision</h3>
        <div className={styles.actions}>
          {["Continue processing", "Return for clarification", "Refer for independent review", "Legal review required"].map(label => (
            <Button key={label} variant="secondary" disabled>{label}</Button>
          ))}
        </div>
      </section>
    </div>
  </>;
}

function SummaryView({ demo }: { demo: boolean }) {
  const [presetKey, setPresetKey] = useState<ProposalPresetKey>("proposal-3");
  const [dossier, setDossier] = useState<{
    screening?: PersistedHistoryItem;
    referral?: PersistedHistoryItem;
    complaint?: PersistedHistoryItem;
    anomaly?: PersistedHistoryItem;
    timing?: PersistedHistoryItem;
    audit?: AuditCaseHistoryItem;
    receipt?: AuditVerificationReceiptResponse;
  }>({});
  const [loading, setLoading] = useState(!demo);

  const activeCaseId = PROPOSAL_PRESETS[presetKey].caseId;

  useEffect(() => {
    if (demo) {
      return;
    }
    let active = true;
    Promise.allSettled([
      getScreeningHistoryByCase(activeCaseId, 5),
      getReferralHistoryByCase(activeCaseId, 5),
      getComplaintHistoryByCase(activeCaseId, 5),
      getWorkflowAnomalyHistoryByCase(activeCaseId, 5),
      getLeaseTimingHistoryByCase(activeCaseId, 5),
      getAuditHistoryByCase(activeCaseId, 5),
    ])
      .then(async results => {
        if (!active) return;
        const pick = (idx: number) => (results[idx].status === "fulfilled" ? (results[idx].value as PersistedHistoryItem[])[0] : undefined);
        const audits = results[5].status === "fulfilled" ? (results[5].value as AuditCaseHistoryItem[]) : [];
        let receipt: AuditVerificationReceiptResponse | undefined;
        if (audits[0]) {
          try {
            receipt = await getAuditReceipt(audits[0].auditRecordId);
          } catch {
            receipt = undefined;
          }
        }
        if (!active) return;
        setDossier({
          screening: pick(0),
          referral: pick(1),
          complaint: pick(2),
          anomaly: pick(3),
          timing: pick(4),
          audit: audits[0],
          receipt,
        });
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [activeCaseId, demo]);

  const scrResult = dossier.screening?.result as Record<string, unknown> | undefined;
  const screeningStatus = scrResult?.overallStatus ? String(scrResult.overallStatus) : "Unavailable";
  const hasPersisted = Boolean(dossier.screening || dossier.referral || dossier.complaint || dossier.anomaly || dossier.timing || dossier.audit);

  return <>
    {!demo && <ProposalPresetSelector selected={presetKey} onSelect={key => { setLoading(true); setPresetKey(key); }} disabled={loading} />}
    <div className={styles.split}>
      <section className={styles.card}>
        <h2>Check progress ({demo ? "Demo mode" : activeCaseId})</h2>
        <Checks
          items={
            !demo && hasPersisted
              ? [
                  { label: "Early evidence screening", status: screeningStatus },
                  { label: "Complaint ML classification (v2)", status: dossier.complaint ? String(dossier.complaint.predictedCategory) : "NotApplicable" },
                  { label: "Workflow statistical anomaly (v1)", status: dossier.anomaly ? `Score ${dossier.anomaly.anomalyScore} (${dossier.anomaly.flagged ? "Flagged" : "Clear"})` : "NotEligible" },
                  { label: "Lease approval timing", status: dossier.timing ? timingLabel(dossier.timing.timingState) : "Unavailable" },
                  { label: "Commissioner referral handoff", status: dossier.referral ? referralLabel(dossier.referral.deliveryState) : "NotRequired" },
                  { label: "Audit / Besu blockchain receipt", status: dossier.receipt ? `${dossier.receipt.anchorStatus} · ${dossier.receipt.verificationStatus} (Block ${dossier.receipt.blockNumber ?? "N/A"})` : "No audited core op" },
                ]
              : ["Regulatory compliance", "Conflict detection", "Early evidence screening", "Institutional consensus", "Conditional verification", "Workflow statistical anomaly"].map(label => ({ label, status: "Unavailable" }))
          }
        />
        <Button href="/component-04/cases" variant="secondary">Inspect individual assessments</Button>
      </section>
      <section className={styles.card}>
        <h2>Why this status?</h2>
        <Unavailable title="Final summary unavailable">
          No authoritative summary linking the case, workflow run and assessment snapshots has been retrieved. Separate examples cannot be combined into a final outcome.
        </Unavailable>
        <h3>Evidence used</h3>
        <Checks
          items={[
            { label: "Completed checks", status: !demo && hasPersisted ? "Persisted in PostgreSQL" : "Unavailable" },
            { label: "Pending checks", status: !demo && hasPersisted ? (dossier.anomaly ? "None" : "Workflow ongoing / ineligible for post-workflow anomaly") : "Unavailable" },
            { label: "Machine predictions", status: !demo && hasPersisted ? `Complaint: ${dossier.complaint ? "Computed (v2)" : "N/A"} · Anomaly: ${dossier.anomaly ? "Computed (v1)" : "Ineligible"}` : "Unavailable" },
            { label: "Human-reviewed findings", status: "Unavailable" },
            { label: "Referral delivery state", status: !demo && dossier.referral ? `${referralLabel(dossier.referral.deliveryState)} (SyntheticPp1Handoff)` : "Unavailable" },
            { label: "Authorized decisions", status: "Unavailable" },
          ]}
        />
        <div className={styles.actions}>
          <Button disabled>Finish workflow and record details</Button>
        </div>
        <p className={styles.muted}>Final lease-decision and workflow execution remain with their authorized process.</p>
      </section>
    </div>
  </>;
}

const DEFAULT_PP1_AUDIT_RECORD_ID = "083d4e8b-6a13-4791-8d65-e05b4f3515ea";

function VerificationView({ demo }: { demo: boolean }) {
  const [recordId, setRecordId] = useState(DEFAULT_PP1_AUDIT_RECORD_ID);
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
        <>
          <div className={styles.notice} style={{ marginBottom: "1rem" }}>
            <strong>Proposal 3 (PP1-FINAL-COMBINED-001) Anchored Audit Record:</strong>
            <div className={styles.actions} style={{ marginTop: "0.5rem" }}>
              <Button
                type="button"
                variant="secondary"
                onClick={() => setRecordId(DEFAULT_PP1_AUDIT_RECORD_ID)}
                disabled={pending}
              >
                Use PP1-FINAL-COMBINED-001 (083d4e8b-6a13-4791-8d65-e05b4f3515ea)
              </Button>
            </div>
          </div>
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
        </>
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
  const [recordId, setRecordId] = useState(DEFAULT_PP1_AUDIT_RECORD_ID);
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
      <>
        <div className={styles.notice} style={{ marginBottom: "1rem" }}>
          <strong>Proposal 3 (PP1-FINAL-COMBINED-001) Read-Only Receipt Lookup:</strong>
          <div className={styles.actions} style={{ marginTop: "0.5rem" }}>
            <Button
              type="button"
              variant="secondary"
              onClick={() => setRecordId(DEFAULT_PP1_AUDIT_RECORD_ID)}
              disabled={pending}
            >
              Use PP1-FINAL-COMBINED-001 (083d4e8b-6a13-4791-8d65-e05b4f3515ea)
            </Button>
          </div>
        </div>
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
      </>
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
