"use client";

import { useCallback, useEffect, useState } from "react";
import { StatusBadge } from "@/shared/components/StatusBadge";
import {
  assessPersistedWorkflowProjection,
  describeGovernanceRequestError,
  getPersistedWorkflowAnomalyHistory,
  getWorkflowProjectionJourney,
  type PersistedWorkflowAnomalyAssessment,
  type WorkflowProjectionJourney,
} from "../services/governanceService";
import styles from "./WorkflowAnomalyJourney.module.css";

const DEFAULT_CASE_ID = "PP1-ANOMALY-COMMISSIONER-001";
const MINIMUM_VISIBLE_INFERENCE_MS = 1400;

export function WorkflowAnomalyJourney({
  initialCaseId = DEFAULT_CASE_ID,
}: {
  initialCaseId?: string;
}) {
  const [projection, setProjection] = useState<WorkflowProjectionJourney | null>(null);
  const [assessment, setAssessment] = useState<PersistedWorkflowAnomalyAssessment | null>(null);
  const [state, setState] = useState<"loading" | "ready" | "error">("loading");
  const [error, setError] = useState<string | null>(null);
  const [inferenceRunning, setInferenceRunning] = useState(false);
  const [processedEventCount, setProcessedEventCount] = useState(0);

  const load = useCallback(async (requestedCaseId: string) => {
    setState("loading");
    setError(null);
    try {
      const persistedProjection = await getWorkflowProjectionJourney(requestedCaseId);
      const history = await getPersistedWorkflowAnomalyHistory(requestedCaseId);
      setProjection(persistedProjection);
      setAssessment(history.find(item => item.workflowRunId === persistedProjection.workflowRunId) ?? null);
      setState("ready");
    } catch (requestError) {
      setProjection(null);
      setAssessment(null);
      setError(describeGovernanceRequestError(requestError));
      setState("error");
    }
  }, []);

  useEffect(() => {
    let cancelled = false;
    queueMicrotask(() => { if (!cancelled) void load(initialCaseId); });
    return () => { cancelled = true; };
  }, [initialCaseId, load]);

  useEffect(() => {
    if (!inferenceRunning || !projection?.events.length) return;
    const intervalMilliseconds = Math.max(
      80,
      Math.floor(MINIMUM_VISIBLE_INFERENCE_MS / projection.events.length),
    );
    const timer = window.setInterval(() => {
      setProcessedEventCount(current => Math.min(current + 1, projection.events.length - 1));
    }, intervalMilliseconds);
    return () => window.clearInterval(timer);
  }, [inferenceRunning, projection]);

  async function runAssessment() {
    if (!projection || state === "loading" || inferenceRunning) return;
    setInferenceRunning(true);
    setProcessedEventCount(0);
    setError(null);
    try {
      const [nextAssessment] = await Promise.all([
        assessPersistedWorkflowProjection(projection),
        new Promise<void>(resolve => window.setTimeout(resolve, MINIMUM_VISIBLE_INFERENCE_MS)),
      ]);
      setProcessedEventCount(projection.events.length);
      setAssessment(nextAssessment);
      setState("ready");
    } catch (requestError) {
      setError(describeGovernanceRequestError(requestError));
      setState("error");
    } finally {
      setInferenceRunning(false);
    }
  }

  return <section className={styles.journey} aria-labelledby="workflow-anomaly-journey-title">
    <header className={styles.titleBar}>
      <div><p className={styles.eyebrow}>Commissioner review aid</p><h2 id="workflow-anomaly-journey-title">Workflow anomaly journey</h2></div>
      <StatusBadge tone="primary">Advisory ML</StatusBadge>
    </header>
    {state === "loading" && <p role="status" className={styles.loading}>Retrieving the persisted projection and assessment. No intermediate analysis stages are simulated.</p>}
    {error && <div role="alert" className={styles.error}><strong>Journey unavailable</strong><p>{error}</p></div>}
    {projection && <WorkflowAnomalyJourneyView
      projection={projection}
      assessment={assessment}
      onAssess={runAssessment}
      busy={state === "loading" || inferenceRunning}
      processing={inferenceRunning}
      processedEventCount={processedEventCount}
    />}
  </section>;
}

export function WorkflowAnomalyJourneyView({
  projection,
  assessment,
  onAssess,
  busy = false,
  processing = false,
  processedEventCount = 0,
}: {
  projection: WorkflowProjectionJourney;
  assessment: PersistedWorkflowAnomalyAssessment | null;
  onAssess?: () => void;
  busy?: boolean;
  processing?: boolean;
  processedEventCount?: number;
}) {
  const features = assessment ? [
    ["Event count", assessment.eventCount],
    ["Elapsed days", assessment.elapsedDays],
    ["Maximum gap hours", assessment.maxGapHours],
    ["Mean gap hours", assessment.meanGapHours],
    ["Unique activities", assessment.uniqueActivities],
    ["Repeated activity count", assessment.repeatedActivityCount],
    ["Resource handoffs", assessment.resourceHandoffs],
    ["Institution switches", assessment.institutionSwitches],
    ["Distinct resources", assessment.distinctResources],
  ] as const : [];

  return <div className={styles.content}>
    <section className={styles.caseHeader} aria-label="Case and model identity">
      <div><span>Case ID</span><strong>{projection.caseId}</strong></div>
      <div><span>Workflow Run ID</span><strong>{projection.workflowRunId}</strong></div>
      <div><span>Trace state</span><strong>{projection.workflowState} · {projection.assessmentEligibility}</strong></div>
      <div><span>Provenance</span><strong>{projection.isSynthetic ? "Synthetic upstream evidence" : "Persisted upstream evidence"}</strong></div>
      <div><span>Assessment time</span><strong>{assessment?.assessedAtUtc ?? "Not yet assessed"}</strong></div>
      <div><span>Model version</span><strong>{assessment?.modelVersion ?? "Not yet applied"}</strong></div>
    </section>

    <div className={styles.provenance} role="note">
      <strong>{projection.provenanceKind}</strong> · {projection.syntheticSeedId ?? "Seed ID unavailable"} · {projection.syntheticSeedVersion ?? "Seed version unavailable"}
      <p>Synthetic C1/C3 handoff evidence is shown separately from real Component 4 feature extraction, frozen-model inference, and PostgreSQL persistence.</p>
    </div>

    {assessment && !processing && projection.assessmentEligibility === "Eligible" && onAssess && <section className={styles.pending} aria-label="Run a fresh workflow anomaly assessment">
      <h3>Run the workflow anomaly model again</h3>
      <p>The result below is persisted history. Running again performs a new frozen-model inference and persists a new assessment for this workflow trace.</p>
      <button className={styles.action} onClick={onAssess} disabled={busy}>{busy ? "Running frozen-model assessment…" : "Run and persist a new frozen-model assessment"}</button>
    </section>}

    <section aria-labelledby="institutional-route-title">
      <h3 id="institutional-route-title">Institutional workflow timeline</h3>
      {processing && <p className={styles.processingStatus} role="status" aria-live="polite">
        Applying the workflow anomaly model to trace event {Math.min(processedEventCount + 1, projection.events.length)} of {projection.events.length}…
      </p>}
      <ol className={styles.timeline}>
        {projection.events.map((event, index) => {
          const conditional = event.status.toLowerCase() === "notapplicable";
          const repeated = /correction|re-submission|repeated/i.test(event.activity);
          const active = processing && index === Math.min(processedEventCount, projection.events.length - 1);
          const processed = !processing || index < processedEventCount;
          const markerClass = active
            ? styles.processing
            : processed
              ? conditional ? styles.notApplicable : styles.completed
              : styles.pendingStep;
          const markerLabel = active
            ? `Analyzing event ${index + 1} of ${projection.events.length}`
            : processed
              ? conditional ? `Event ${index + 1} not applicable` : `Event ${index + 1} processed`
              : `Event ${index + 1} waiting`;
          return <li key={`${event.eventSequence}-${event.activity}`} className={markerClass}>
            <div className={styles.marker} aria-label={markerLabel}>
              {active ? <span className={styles.spinner} aria-hidden="true" /> : processed ? conditional ? "—" : "✓" : index + 1}
            </div>
            <article>
              <div className={styles.eventHeading}><strong>{event.eventSequence}. {event.activity}</strong><StatusBadge tone={conditional ? "neutral" : "success"}>{conditional ? "Not applicable" : event.status}</StatusBadge></div>
              <p>{event.institution} · {event.actorRole} · {event.resource}</p>
              <p><time dateTime={event.timestamp}>{event.timestamp}</time> · {gapText(projection.events[index - 1]?.timestamp, event.timestamp)}</p>
              {repeated && <span className={styles.correction}>Correction / repeated-stage marker</span>}
            </article>
          </li>;
        })}
      </ol>
    </section>

    {!assessment && <section className={styles.pending} aria-label="Assessment state">
      <h3>Persisted anomaly assessment not found</h3>
      <p>The projection is visible, but no matching persisted assessment has been confirmed. Officer review has not been performed.</p>
      {projection.assessmentEligibility === "Eligible" && onAssess && <button className={styles.action} onClick={onAssess} disabled={busy}>Run and persist frozen-model assessment</button>}
    </section>}

    {assessment && !processing && <>
      <section aria-labelledby="analysis-checklist-title">
        <h3 id="analysis-checklist-title">Confirmed analysis checklist</h3>
        <ol className={styles.checklist}>{[
          "Workflow projection retrieved", "Case/run linkage verified", "Complete trace confirmed",
          "Event order and timestamps validated", "Nine workflow features extracted", "Frozen model applied",
          "Frozen threshold compared", "Assessment persisted",
        ].map(item => <li key={item}><span aria-hidden="true">✓</span>{item}</li>)}</ol>
      </section>

      <section className={`${styles.result} ${assessment.flagged ? styles.flagged : styles.clear}`} aria-live="polite">
        <div><p className={styles.eyebrow}>Persisted advisory result</p><h3>{assessment.flagged ? "Flagged for review" : "No statistical escalation"}</h3></div>
        <StatusBadge tone={assessment.flagged ? "warning" : "success"}>{assessment.flagged ? "Commissioner review" : "No model escalation"}</StatusBadge>
        <dl>
          <div><dt>Anomaly score</dt><dd>{assessment.anomalyScore}</dd></div>
          <div><dt>Frozen threshold</dt><dd>{assessment.threshold}</dd></div>
          <div><dt>Strict decision rule</dt><dd>Backend result: anomaly score &gt; threshold</dd></div>
          <div><dt>Model version</dt><dd>{assessment.modelVersion}</dd></div>
          <div><dt>Assessment timestamp</dt><dd>{assessment.assessedAtUtc}</dd></div>
          <div><dt>Officer review state</dt><dd>Not available / not yet performed</dd></div>
        </dl>
        <p className={styles.guidance}>{assessment.flagged
          ? "Review the workflow history and supporting records before allowing normal processing to continue."
          : "No statistical escalation was produced. This does not prove that the workflow is compliant or free from governance concerns."}</p>
      </section>

      <section aria-labelledby="feature-context-title">
        <h3 id="feature-context-title">Observed workflow characteristics</h3>
        <div className={styles.tableWrap}><table><thead><tr><th scope="col">Characteristic</th><th scope="col">Persisted value</th></tr></thead><tbody>
          {features.map(([label, value]) => <tr key={label}><th scope="row">{label}</th><td>{value}</td></tr>)}
        </tbody></table></div>
        <p>These values provide review context and are not proven causes of the model result.</p>
      </section>
    </>}

    <p className={styles.disclaimer} role="note">The anomaly score is not a probability or confidence value. It does not establish corruption, misconduct, legal non-compliance or officer guilt.</p>
  </div>;
}

function gapText(previous: string | undefined, current: string): string {
  if (!previous) return "First recorded stage";
  const asInstant = (value: string) => Date.parse(/(?:Z|[+-]\d{2}:\d{2})$/i.test(value) ? value : `${value}Z`);
  const milliseconds = asInstant(current) - asInstant(previous);
  if (!Number.isFinite(milliseconds) || milliseconds < 0) return "Gap unavailable";
  const hours = milliseconds / 3_600_000;
  return `Gap since previous stage: ${hours % 24 === 0 ? `${hours / 24} days` : `${hours} hours`}`;
}
