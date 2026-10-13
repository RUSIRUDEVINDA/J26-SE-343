"use client";

import { FormEvent, useState } from "react";
import { Button } from "@/shared/components/Button";
import { StatusBadge } from "@/shared/components/StatusBadge";
import { formatGovernanceTime } from "../adapters/presentation";
import {
  describeGovernanceRequestError,
  evaluateCompliance,
  evaluateConflicts,
  type ComplianceEvaluationResponse,
  type ConflictEvaluationResponse,
  type GovernanceDecisionSnapshot,
} from "../services/governanceService";
import { Facts } from "./GovernancePanels";
import {
  getComplianceEvaluationPreset,
  PROPOSAL_PRESET_LIST,
  PROPOSAL_PRESETS,
  type ProposalPresetKey,
} from "../services/proposalPresets";
import styles from "./GovernanceWorkspace.module.css";

function complianceTone(status: string): "neutral" | "success" | "warning" | "danger" {
  if (status === "Compliant") return "success";
  if (status === "NonCompliant") return "danger";
  if (status === "Conditional" || status === "RequiresReview" || status === "RequiresHumanReview" || status === "InsufficientInformation") return "warning";
  return "neutral";
}

function conflictTone(status: string): "neutral" | "success" | "warning" | "danger" {
  if (status === "NoConflicts") return "success";
  if (status === "ConflictsDetected") return "danger";
  return "neutral";
}

function RequestError({ title, message }: { title: string; message: string }) {
  return <div className={styles.notice} role="alert"><strong>{title}</strong><p>{message}</p></div>;
}

export function LiveComplianceEvaluation() {
  const initialPreset = getComplianceEvaluationPreset("proposal-1");
  const [selectedPreset, setSelectedPreset] = useState<ProposalPresetKey>("proposal-1");
  const [actionName, setActionName] = useState(initialPreset.actionName);
  const [caseId, setCaseId] = useState(initialPreset.caseId);
  const [inputJson, setInputJson] = useState(initialPreset.inputJson);
  const [pending, setPending] = useState(false);
  const [result, setResult] = useState<ComplianceEvaluationResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  function applyPreset(presetKey: ProposalPresetKey) {
    const preset = getComplianceEvaluationPreset(presetKey);
    setSelectedPreset(presetKey);
    setActionName(preset.actionName);
    setCaseId(preset.caseId);
    setInputJson(preset.inputJson);
    setResult(null);
    setError(null);
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (pending) return;

    let input: Record<string, unknown>;
    try {
      const parsed: unknown = JSON.parse(inputJson);
      if (parsed === null || Array.isArray(parsed) || typeof parsed !== "object") throw new SyntaxError();
      input = parsed as Record<string, unknown>;
    } catch {
      setError("Proposal input must be a valid JSON object. No evaluation was submitted.");
      setResult(null);
      return;
    }

    setPending(true);
    setError(null);
    setResult(null);
    try {
      const response = await evaluateCompliance({
        actionName: actionName.trim(),
        caseId: caseId.trim(),
        input,
      });
      setResult(response);
    } catch (requestError) {
      setError(describeGovernanceRequestError(requestError));
    } finally {
      setPending(false);
    }
  }

  return <section className={styles.card} aria-labelledby="live-compliance-title">
    <h2 id="live-compliance-title">Live compliance evaluation</h2>
    <p className={styles.muted}>This calls the full NPD proposal contract on the published ASP.NET Core route. It evaluates the JSON against the active rule catalogue and creates an evaluation record. Submissions are not retried automatically.</p>
    <div className={styles.notice} style={{ marginBottom: "1rem" }}>
      <strong>Select a Proposal Example to auto-fill input:</strong>
      <div className={styles.actions} style={{ marginTop: "0.5rem" }}>
        {PROPOSAL_PRESET_LIST.map(preset => <Button
          key={preset.key}
          type="button"
          variant={selectedPreset === preset.key ? "primary" : "secondary"}
          onClick={() => applyPreset(preset.key)}
          disabled={pending}
        >{preset.shortLabel}</Button>)}
      </div>
      <p className={styles.muted} style={{ marginTop: "0.5rem", marginBottom: 0 }}><strong>{PROPOSAL_PRESETS[selectedPreset].title}:</strong> {PROPOSAL_PRESETS[selectedPreset].summary}</p>
    </div>
    <form onSubmit={submit}>
      <div className={styles.field}><label htmlFor="compliance-action-name">Action name</label><input id="compliance-action-name" value={actionName} onChange={event => setActionName(event.target.value)} required disabled={pending} /></div>
      <div className={styles.field}><label htmlFor="compliance-case-id">Case ID</label><input id="compliance-case-id" value={caseId} onChange={event => setCaseId(event.target.value)} required disabled={pending} /></div>
      <div className={styles.field}><label htmlFor="compliance-input">Proposal input (JSON)</label><textarea id="compliance-input" value={inputJson} onChange={event => setInputJson(event.target.value)} required disabled={pending} style={{ minHeight: "360px", fontFamily: "monospace", fontSize: "0.82rem" }} /></div>
      <Button type="submit" disabled={pending}>{pending ? "Evaluating…" : "Run compliance evaluation"}</Button>
    </form>
    {error && <RequestError title="Compliance evaluation unavailable" message={error} />}
    {result && <ComplianceResult result={result} />}
  </section>;
}

function ComplianceResult({ result }: { result: ComplianceEvaluationResponse }) {
  return <div aria-live="polite">
    <h3>Live result</h3><StatusBadge tone={complianceTone(result.status)}>{result.status}</StatusBadge>
    <Facts values={[["Evaluation ID", result.deterministicEvaluationId ?? "Not supplied"], ["Violations", String(result.violations.length)], ["Conditions", String(result.conditions.length)], ["Detailed findings", String(result.findings?.length ?? 0)]]} />
    {result.violations.length > 0 && <><h3>Violations</h3><ul>{result.violations.map(item => <li key={`${item.ruleCode}-${item.message}`}><strong>{item.ruleCode}</strong> · {item.message}</li>)}</ul></>}
    {result.conditions.length > 0 && <><h3>Conditions</h3><ul>{result.conditions.map(item => <li key={`${item.description}-${item.requiredByDate ?? "none"}`}>{item.description}{item.requiredByDate ? ` · required by ${formatGovernanceTime(item.requiredByDate)}` : ""}</li>)}</ul></>}
    {(result.findings?.length ?? 0) > 0 && <><h3>Rule findings</h3><div className={styles.scroll}><table className={styles.table}><thead><tr><th scope="col">Rule</th><th scope="col">Category / applicability</th><th scope="col">Status / severity</th><th scope="col">Observed / expected</th><th scope="col">Evidence / calculation</th><th scope="col">Source reference</th><th scope="col">Review flags</th><th scope="col">Recommended action</th></tr></thead><tbody>{result.findings?.map(finding => <tr key={`${finding.ruleCode}-${finding.ruleVersion}`}><th scope="row">{finding.ruleCode} · {finding.ruleVersion}</th><td>{finding.category} · {finding.applicability}</td><td>{finding.status} · {finding.severity}</td><td>{finding.observedValueSummary} / {finding.expectedRequirement}</td><td>{finding.evidenceStatus} / {finding.calculationStatus}</td><td>{[finding.sourceAuthority, finding.sourceDocument, finding.sourceSection].filter(Boolean).join(" · ") || "Not supplied"}</td><td>{finding.isBlocking ? "Blocking" : "Non-blocking"} · {finding.requiresHumanReview ? "Human review required" : "No human-review flag"}</td><td>{finding.recommendedAction}</td></tr>)}</tbody></table></div></>}
  </div>;
}

export function LiveConflictEvaluation() {
  const [actionName, setActionName] = useState("");
  const [decisionsJson, setDecisionsJson] = useState("");
  const [pending, setPending] = useState(false);
  const [result, setResult] = useState<ConflictEvaluationResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (pending) return;

    let decisions: GovernanceDecisionSnapshot[];
    try {
      const parsed: unknown = JSON.parse(decisionsJson);
      if (!isDecisionSnapshotArray(parsed)) throw new Error();
      decisions = parsed;
    } catch {
      setError("Decision snapshots must be a JSON array. No evaluation was submitted.");
      setResult(null);
      return;
    }

    setPending(true);
    setError(null);
    setResult(null);
    try {
      setResult(await evaluateConflicts({ actionName: actionName.trim(), decisions }));
    } catch (requestError) {
      setError(describeGovernanceRequestError(requestError));
    } finally {
      setPending(false);
    }
  }

  return <section className={styles.card} aria-labelledby="live-conflict-title">
    <h2 id="live-conflict-title">Live conflict evaluation</h2>
    <p className={styles.muted}>This calls the published ASP.NET Core evaluation route with decision snapshots supplied by the user. It does not load decisions from another component or manufacture spatial results. Submissions are not retried automatically.</p>
    <form onSubmit={submit}>
      <div className={styles.field}><label htmlFor="conflict-action-name">Action name</label><input id="conflict-action-name" value={actionName} onChange={event => setActionName(event.target.value)} required disabled={pending} /></div>
      <div className={styles.field}><label htmlFor="conflict-decisions">Decision snapshots (JSON array)</label><textarea id="conflict-decisions" value={decisionsJson} onChange={event => setDecisionsJson(event.target.value)} required disabled={pending} aria-describedby="conflict-contract" /></div>
      <p id="conflict-contract" className={styles.muted}>Each snapshot must provide decisionId, subjectId, institutionName, authorityLevel, decisionType, proposedUse, effectiveFrom, effectiveTo and regulatoryReference. Optional fields follow the published API contract.</p>
      <Button type="submit" disabled={pending}>{pending ? "Detecting conflicts…" : "Detect conflicts"}</Button>
    </form>
    {error && <RequestError title="Conflict evaluation unavailable" message={error} />}
    {result && <ConflictResult result={result} />}
  </section>;
}

function isDecisionSnapshotArray(value: unknown): value is GovernanceDecisionSnapshot[] {
  const requiredStringFields = ["decisionId", "subjectId", "institutionName", "authorityLevel", "decisionType", "proposedUse", "regulatoryReference"];
  return Array.isArray(value) && value.every(item => {
    if (!isRecord(item) || !requiredStringFields.every(field => typeof item[field] === "string")) return false;
    return typeof item.effectiveFrom === "string" && !Number.isNaN(Date.parse(item.effectiveFrom))
      && typeof item.effectiveTo === "string" && !Number.isNaN(Date.parse(item.effectiveTo));
  });
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function ConflictResult({ result }: { result: ConflictEvaluationResponse }) {
  return <div aria-live="polite">
    <h3>Live result</h3><StatusBadge tone={conflictTone(result.status)}>{result.status}</StatusBadge>
    {result.conflicts.length === 0 ? <p>No conflicts were returned by this evaluation.</p> : <div className={styles.scroll}><table className={styles.table}><thead><tr><th scope="col">Conflict</th><th scope="col">Severity / detection state</th><th scope="col">Subject</th><th scope="col">Decisions / institutions</th><th scope="col">Explanation / evidence rule</th><th scope="col">Recommended action</th><th scope="col">Detected at</th></tr></thead><tbody>{result.conflicts.map(conflict => <tr key={conflict.conflictId}><th scope="row">{conflict.conflictType} · {conflict.conflictId}</th><td>{conflict.severity} · {conflict.detectionStatus}</td><td>{conflict.subjectId}</td><td>{[...conflict.involvedDecisionIds, ...conflict.involvedInstitutions].join(", ")}</td><td>{conflict.explanation} · {conflict.evidenceRule}</td><td>{conflict.recommendedAction}</td><td>{formatGovernanceTime(conflict.detectionTimestamp)}</td></tr>)}</tbody></table></div>}
  </div>;
}
