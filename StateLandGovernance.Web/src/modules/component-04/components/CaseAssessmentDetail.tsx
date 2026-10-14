"use client";
import Link from "next/link";
import { Button } from "@/shared/components/Button";
import { LoadingState } from "@/shared/components/LoadingState";
import { EmptyState } from "@/shared/components/EmptyState";
import { useGovernanceData } from "../hooks/useGovernanceData";
import { formatGovernanceTime } from "../adapters/presentation";
import { Facts, Checks, Unavailable } from "./GovernancePanels";
import { useGovernanceMode } from "./GovernanceWorkspace";
import { LiveComplianceEvaluation, LiveConflictEvaluation } from "./LiveEvaluationPanels";
import styles from "./GovernanceWorkspace.module.css";

export function CaseAssessmentDetail({ assessmentId, section = "overview" }: { assessmentId?: string; section?: string }) {
  const { state, retry } = useGovernanceData();
  const mode = useGovernanceMode();
  const selected = state.status === "success" ? state.data.cases.find(item => item.assessmentId === assessmentId) : undefined;
  const title = section === "compliance" ? "Regulatory Compliance Check" : section === "conflicts" ? "Conflict Detection Check" : "Case Assessment Dossier";
  if (state.status === "loading") return <LoadingState message="Loading assessment…" />;
  return <>
    <header className={styles.heading}><h1>{title}</h1><p>Recorded governance findings and the evidence available for this assessment.</p></header>
    {state.status === "error" && <><Unavailable>{state.message}</Unavailable><Button onClick={retry}>Retry loading</Button></>}
    {state.status === "unavailable" && <Unavailable>{state.message} Opening a dossier does not run a new evaluation.</Unavailable>}
    {mode === "live" && section === "compliance" && <LiveComplianceEvaluation />}
    {mode === "live" && section === "conflicts" && <LiveConflictEvaluation />}
    {state.status === "success" && !selected && <section className={styles.card}>
      <EmptyState title={assessmentId ? "Assessment not found" : "Choose a case assessment"} description="Open an exact assessment to keep findings and evidence versions separate." />
      <ul>{state.data.cases.map(item => <li key={item.assessmentId}><Link href={`/component-04/cases/assessment/${item.assessmentId}/${section}`}>{item.caseId} · {item.assessmentId}</Link></li>)}</ul>
    </section>}
    {selected && <>
      <nav className={styles.tabs} aria-label="Assessment views">{["overview", "compliance", "conflicts"].map(tab => <Link key={tab} aria-current={section === tab ? "page" : undefined} href={`/component-04/cases/assessment/${selected.assessmentId}/${tab}`}>{tab === "overview" ? "Assessment summary" : tab === "compliance" ? "Compliance" : "Conflicts"}</Link>)}</nav>
      <div className={styles.split}>
        <section className={styles.card}><h2>{section === "overview" ? "Assessment identity" : "Check progress"}</h2>
          <Facts values={[["Case ID", selected.caseId], ["Assessment ID", selected.assessmentId], ["Workflow-run ID", selected.workflowRunId ?? "Not supplied"], ["Evidence snapshot", selected.version], ["Assessed at", formatGovernanceTime(selected.evaluatedAt)], ["Applicant", selected.applicantReference], ["Parcel", selected.parcelReference]]} />
          <Checks items={section === "compliance" ? [{ label: "Recorded compliance outcome", status: selected.complianceStatus }, { label: "Individual rule findings", status: "Unavailable", note: "The existing demo snapshot contains a summary only." }] : section === "conflicts" ? [{ label: "Recorded conflict outcome", status: selected.conflictStatus }, { label: "Decision and evidence references", status: "Unavailable" }] : [
            { label: "Regulatory compliance", status: selected.complianceStatus }, { label: "Conflict detection", status: selected.conflictStatus },
            { label: "Early evidence screening", status: selected.earlyScreeningStatus }, { label: "Completed-workflow anomaly", status: selected.anomalyStatus },
            { label: "Institutional consensus", status: selected.consensusOutcome }, { label: "Conditional verification", status: "Unavailable" },
          ]} />
        </section>
        <section className={styles.card}><h2>Recorded findings</h2><Unavailable title="Fictional assessment summary">The example is for visual review. It is not a source-backed departmental evaluation.</Unavailable>
          <h3>What was recorded</h3><p>{selected.summaryFinding}</p>
          <h3>Why this result?</h3><p>{selected.rationale}</p>
          <h3>Recorded next step</h3><p>{selected.recommendedNextStep}</p>
          <h3>Evidence references</h3><p className={styles.muted}>Detailed source records and rule references are not included in this demo snapshot. No missing evidence is treated as verified.</p>
          <h3>Other checks</h3><p>Completed checks, pending checks and unavailable assessments remain separate. The displayed statuses do not authorize a lease decision.</p>
          <div className={styles.actions}><Button href="/component-04/complaints" variant="secondary">Complaint intelligence</Button><Button href="/component-04/cases" variant="ghost">Back to assessments</Button></div>
        </section>
      </div>
    </>}
  </>;
}
