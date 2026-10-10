"use client";

import { useState, type FormEvent } from "react";
import { Button } from "@/shared/components/Button";
import { StatusBadge } from "@/shared/components/StatusBadge";
import { formatGovernanceTime } from "../adapters/presentation";
import {
  describeGovernanceRequestError,
  evaluateConsensus,
  evaluateGovernanceRisk,
  explainGovernance,
  GovernanceMalformedResponseError,
  verifyConditions,
  type ConditionalVerificationResponse,
  type GovernanceConsensusResponse,
  type GovernanceEvaluationRequest,
  type GovernanceExplanationResponse,
  type GovernanceRiskResponse,
} from "../services/governanceService";
import styles from "./GovernanceWorkspace.module.css";

type EvaluationKind = "risk" | "explain" | "consensus" | "conditions";
type EvaluationResponse =
  | GovernanceRiskResponse
  | GovernanceExplanationResponse
  | GovernanceConsensusResponse
  | ConditionalVerificationResponse;

const specifications: Record<EvaluationKind, { path: string; label: string; guidance: string }> = {
  risk: {
    path: "/api/governance-intelligence/evaluate-risk",
    label: "Governance risk evaluation",
    guidance: "Input JSON: subjectId and the decisionHistory, complianceViolations, governanceConflicts, complaints and institutionalValidations evidence arrays from the published DTO.",
  },
  explain: {
    path: "/api/governance-intelligence/explain",
    label: "Governance explanation",
    guidance: "Input JSON: subjectId and optional complianceEvidence, conflictEvidence and riskEvidence summaries from the published DTO.",
  },
  consensus: {
    path: "/api/governance-intelligence/evaluate-consensus",
    label: "Institutional consensus evaluation",
    guidance: "Input JSON: subjectId, positions and policy. Policy modes: Unanimous, SimpleMajority, Supermajority or ThresholdCount. Position values: Approve, Reject, ConditionalApprove, Abstain or Pending.",
  },
  conditions: {
    path: "/api/governance-intelligence/verify-conditions",
    label: "Conditional verification",
    guidance: "Input JSON: subjectId and one or more conditions; optional evidence and policy. Evidence status values: Satisfied, Failed or Pending.",
  },
};

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function parseRequest(actionName: string, inputText: string, kind: EvaluationKind): GovernanceEvaluationRequest {
  const input: unknown = JSON.parse(inputText);
  if (!isRecord(input)) throw new Error("Input must be a JSON object.");
  const record = input;
  if (!actionName.trim()) throw new Error("Action name is required.");
  if (typeof record.subjectId !== "string" || !record.subjectId.trim()) {
    throw new Error("Input must include a non-empty subjectId.");
  }
  if (kind === "risk") {
    const arrays = ["decisionHistory", "complianceViolations", "governanceConflicts", "complaints", "institutionalValidations"];
    if (!arrays.every(key => Array.isArray(record[key]))) throw new Error(`Input must include each evidence array: ${arrays.join(", ")}.`);
  }
  if (kind === "explain") {
    const evidence = ["complianceEvidence", "conflictEvidence", "riskEvidence"];
    if (!evidence.every(key => record[key] === undefined || record[key] === null || (typeof record[key] === "object" && !Array.isArray(record[key])))) {
      throw new Error("Explanation evidence fields must be objects or null.");
    }
  }
  if (kind === "consensus" && (!Array.isArray(record.positions) || !record.policy || typeof record.policy !== "object" || Array.isArray(record.policy))) {
    throw new Error("Input must include a positions array and a policy object.");
  }
  if (kind === "conditions" && (!Array.isArray(record.conditions) || record.conditions.length === 0)) {
    throw new Error("Input must include at least one condition.");
  }
  return { actionName: actionName.trim(), input: record };
}

export function StructuredEvaluation({ capability }: { capability: EvaluationKind }) {
  const spec = specifications[capability];
  const [actionName, setActionName] = useState("");
  const [input, setInput] = useState("");
  const [pending, setPending] = useState(false);
  const [result, setResult] = useState<EvaluationResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (pending) return;

    let request: GovernanceEvaluationRequest;
    try {
      request = parseRequest(actionName, input, capability);
    } catch (parseError) {
      setError(parseError instanceof SyntaxError ? "Input must be valid JSON. No evaluation was submitted." : describeGovernanceRequestError(parseError));
      setResult(null);
      return;
    }

    setPending(true);
    setError(null);
    setResult(null);
    try {
      if (capability === "risk") setResult(await evaluateGovernanceRisk(request));
      else if (capability === "explain") setResult(await explainGovernance(request));
      else if (capability === "consensus") setResult(await evaluateConsensus(request));
      else setResult(await verifyConditions(request));
    } catch (requestError) {
      setError(describeGovernanceRequestError(requestError));
    } finally {
      setPending(false);
    }
  }

  return <>
    <p className={styles.muted}>Published route: <code>{spec.path}</code>. This is a user-initiated ASP.NET Core evaluation. It records a new evaluation; it does not retrieve history or act on a workflow. No automatic retry is performed.</p>
    <form onSubmit={submit} aria-busy={pending}>
      <div className={styles.field}>
        <label htmlFor={`${capability}-action-name`}>Action name</label>
        <input id={`${capability}-action-name`} value={actionName} onChange={event => setActionName(event.target.value)} required disabled={pending} />
      </div>
      <div className={styles.field}>
        <label htmlFor={`${capability}-input`}>Evaluation input (JSON)</label>
        <textarea id={`${capability}-input`} value={input} onChange={event => setInput(event.target.value)} required disabled={pending} aria-describedby={`${capability}-input-guidance`} />
      </div>
      <p id={`${capability}-input-guidance`} className={styles.muted}>{spec.guidance}</p>
      <Button type="submit" disabled={pending}>{pending ? "Evaluating…" : `Run ${spec.label.toLowerCase()}`}</Button>
    </form>
    {error && <div className={styles.notice} role="alert"><strong>{spec.label} unavailable</strong><p>{error}</p></div>}
    {result && <EvaluationResult kind={capability} result={result} />}
    <p className={styles.muted}>Results are advisory. They are not a legal conclusion, a final lease decision, evidence that an allegation is true, or proof of misconduct.</p>
  </>;
}

function EvaluationResult({ kind, result }: { kind: EvaluationKind; result: EvaluationResponse }) {
  if (kind === "risk" && "overallRiskScore" in result) {
    return <section aria-live="polite">
      <h3>Live risk assessment</h3>
      <StatusBadge tone={result.severity === "High" || result.severity === "Critical" ? "warning" : "neutral"}>{result.severity}</StatusBadge>
      <dl className={styles.facts}>
        <dt>Subject</dt><dd>{result.subjectId}</dd>
        <dt>Overall risk score</dt><dd>{result.overallRiskScore}</dd>
        <dt>Human review required</dt><dd>{result.requiresHumanReview ? "Yes" : "No"}</dd>
        <dt>Evaluated at</dt><dd>{formatGovernanceTime(result.evaluationTimestamp)}</dd>
      </dl>
      <p>{result.disclaimer}</p>
      <h4>Triggered indicators</h4>
      {result.triggeredIndicators.length === 0 ? <p>No indicators were returned by this evaluation.</p> : <ul>{result.triggeredIndicators.map(indicator => <li key={indicator.indicatorId}>
        <strong>{indicator.category} · {indicator.severity}</strong> — {indicator.evidenceSummary}
        <p>{indicator.explanation} Rule: {indicator.triggeredRule}. Recommended action: {indicator.recommendedAction}</p>
        <small>Score contribution: {indicator.scoreContribution}; involved actors: {indicator.involvedActors.join(", ") || "None supplied"}</small>
      </li>)}</ul>}
    </section>;
  }
  if (kind === "explain" && "explanations" in result) {
    return <section aria-live="polite">
      <h3>Live explanation</h3>
      <StatusBadge tone={result.requiresHumanReview ? "warning" : "neutral"}>{result.overallSeverity}</StatusBadge>
      <dl className={styles.facts}>
        <dt>Explanation ID</dt><dd>{result.explanationId}</dd>
        <dt>Subject</dt><dd>{result.subjectId}</dd>
        <dt>Human review required</dt><dd>{result.requiresHumanReview ? "Yes" : "No"}</dd>
        <dt>Evaluated at</dt><dd>{formatGovernanceTime(result.evaluationTimestamp)}</dd>
      </dl>
      {result.explanations.length === 0 ? <p>No explanation items were returned.</p> : result.explanations.map(item => <article className={styles.notice} key={item.itemId}>
        <h4>{item.title}</h4><p>{item.plainLanguageExplanation}</p>
        <dl className={styles.facts}>
          <dt>Reason code</dt><dd>{item.reasonCode}</dd><dt>Source engine</dt><dd>{item.sourceEngine}</dd>
          <dt>Outcome / severity</dt><dd>{item.outcomeStatus} / {item.severity}</dd>
          <dt>Evidence</dt><dd>{item.evidenceSummaries.join("; ") || "None supplied"}</dd>
          <dt>Recommended action</dt><dd>{item.recommendedAction}</dd>
          <dt>Human attention required</dt><dd>{item.requiresHumanAttention ? "Yes" : "No"}</dd>
        </dl>
      </article>)}
      <p>{result.disclaimer}</p>
    </section>;
  }
  if (kind === "consensus" && "consensusEvaluationId" in result) {
    return <section aria-live="polite">
      <h3>Live consensus result</h3><StatusBadge>{result.outcome}</StatusBadge>
      <dl className={styles.facts}>
        <dt>Evaluation ID</dt><dd>{result.consensusEvaluationId}</dd><dt>Subject</dt><dd>{result.subjectId}</dd>
        <dt>Expected / submitted / participating</dt><dd>{result.totalExpectedInstitutions} / {result.submittedCount} / {result.participatingCount}</dd>
        <dt>Approvals / rejections / conditional approvals</dt><dd>{result.approvalCount} / {result.rejectionCount} / {result.conditionalApprovalCount}</dd>
        <dt>Abstentions / pending / missing</dt><dd>{result.abstentionCount} / {result.pendingCount} / {result.missingCount}</dd>
        <dt>Blocking institutions</dt><dd>{result.blockingInstitutionCount}</dd>
        <dt>Quorum satisfied</dt><dd>{result.quorumSatisfied ? "Yes" : "No"}</dd>
        <dt>Mandatory institutions satisfied</dt><dd>{result.mandatoryInstitutionsSatisfied ? "Yes" : "No"}</dd>
        <dt>Consensus threshold satisfied</dt><dd>{result.consensusThresholdSatisfied ? "Yes" : "No"}</dd>
        <dt>Evaluated at</dt><dd>{formatGovernanceTime(result.evaluationTimestamp)}</dd>
      </dl>
      <p>{result.summaryExplanation}</p><p>{result.recommendedAction}</p>
    </section>;
  }
  if (kind === "conditions" && "conditionStatuses" in result) {
    return <section aria-live="polite">
      <h3>Live conditional verification</h3><StatusBadge>{result.outcome}</StatusBadge>
      <dl className={styles.facts}>
        <dt>Verification ID</dt><dd>{result.verificationId}</dd><dt>Subject</dt><dd>{result.subjectId}</dd>
        <dt>Conditions / mandatory</dt><dd>{result.totalConditionsCount} / {result.mandatoryConditionsCount}</dd>
        <dt>Mandatory satisfied / unsatisfied</dt><dd>{result.satisfiedMandatoryCount} / {result.unsatisfiedMandatoryCount}</dd>
        <dt>Optional satisfied</dt><dd>{result.satisfiedOptionalCount}</dd>
        <dt>Pending / missing / expired</dt><dd>{result.pendingConditionsCount} / {result.missingConditionsCount} / {result.expiredConditionsCount}</dd>
        <dt>Evaluated at</dt><dd>{formatGovernanceTime(result.evaluationTimestamp)}</dd>
      </dl>
      <p>{result.summaryExplanation}</p><p>{result.recommendedAction}</p>
      {result.conditionStatuses.length > 0 && <div className={styles.scroll}><table className={styles.table}>
        <thead><tr><th scope="col">Condition</th><th scope="col">Status</th><th scope="col">Mandatory</th><th scope="col">Satisfied</th><th scope="col">Failure reason / notes</th></tr></thead>
        <tbody>{result.conditionStatuses.map(item => <tr key={item.conditionId}><th scope="row">{item.conditionId}</th><td>{item.status}</td><td>{item.isMandatory ? "Yes" : "No"}</td><td>{item.isSatisfied ? "Yes" : "No"}</td><td>{[item.failureReason, item.notes].filter(Boolean).join(" · ") || "None returned"}</td></tr>)}</tbody>
      </table></div>}
    </section>;
  }
  throw new GovernanceMalformedResponseError();
}
