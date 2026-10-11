"use client";
import Link from "next/link";
import { Button } from "@/shared/components/Button";
import { LoadingState } from "@/shared/components/LoadingState";
import { EmptyState } from "@/shared/components/EmptyState";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { useGovernanceData } from "../hooks/useGovernanceData";
import { formatGovernanceTime } from "../adapters/presentation";
import styles from "./GovernanceWorkspace.module.css";

export function GovernanceDashboard() {
  const { state, retry } = useGovernanceData();
  const data = state.status === "success" ? state.data : null;
  const engines: { label: string; key: string; href: string; evaluationAvailable: boolean }[] = [
    { label: "Regulatory compliance", key: "compliance", href: "/component-04/cases/compliance", evaluationAvailable: true },
    { label: "Conflict detection", key: "conflicts", href: "/component-04/cases/conflicts", evaluationAvailable: true },
    { label: "Governance risk", key: "risk", href: "/component-04/risk", evaluationAvailable: true },
    { label: "Explainability", key: "explain", href: "/component-04/explain", evaluationAvailable: true },
    { label: "Institutional consensus", key: "consensus", href: "/component-04/consensus", evaluationAvailable: true },
    { label: "Conditional verification", key: "conditions", href: "/component-04/conditions", evaluationAvailable: true },
    { label: "Completed-workflow anomaly", key: "anomalies", href: "/component-04/anomalies", evaluationAvailable: false },
    { label: "Audit history", key: "audit", href: "/component-04/audit", evaluationAvailable: false },
    { label: "Blockchain verification", key: "verification", href: "/component-04/verification", evaluationAvailable: false },
  ];
  return <>
    <header className={styles.heading}><h1>Governance Intelligence</h1><p>Decision-support overview across compliance, conflicts, risk, institutional verification and conditions.</p></header>
    {state.status === "loading" && <LoadingState message="Loading governance assessments…" />}
    {state.status === "error" && <><ErrorMessage title="Unable to load assessments" message={state.message} /><Button onClick={retry}>Retry loading</Button></>}
    {state.status === "unavailable" && <p className={styles.notice} role="status">{state.message} You can explore fictional examples using the demo control above.</p>}
    <section className={styles.stats} aria-label="Governance overview">
      {[
        ["Cases in this view", data?.summary.totalCases],
        ["Review required", data?.summary.reviewRequiredCount],
        ["Insufficient information", data?.summary.insufficientInfoCount],
        ["Recorded clear status", data?.summary.clearCount],
        ["Average governance risk", null],
      ].map(([label, value]) => <article className={styles.stat} key={label}><p>{label}</p><strong>{value ?? "—"}</strong>{value == null && <small>Unavailable</small>}</article>)}
    </section>
    <div className={styles.dashboardSplit}>
      <section className={styles.card}><h2>Engine Summary</h2><p className={styles.muted}>Assessment areas and recorded availability</p>
        <div className={styles.scroll}><table className={styles.table}><thead><tr><th scope="col">Engine</th><th scope="col">Results</th><th scope="col">Review</th></tr></thead><tbody>
          {engines.map(({ label, key, href, evaluationAvailable }) => {
            const isDemoSummary = data !== null && (key === "compliance" || key === "conflicts");
            const result = isDemoSummary ? "Fictional summary" : evaluationAvailable ? "Evaluation route published" : "Unavailable";
            return <tr key={key}><th scope="row">{label}</th><td>{result}</td><td><Link href={href}>Open</Link></td></tr>;
          })}
        </tbody></table></div>
        <p className={styles.muted}>A clear result in one check does not mean all checks are complete. Risk scores and statistical anomaly scores are separate measures.</p>
      </section>
      <div className={styles.stack}><section className={styles.card}><h2>Recent Cases</h2>
        {data?.cases.length ? <><p className={styles.muted}>Latest fictional assessment per case. Open a case to inspect its assessment identity and timestamp.</p><div className={styles.scroll}><table className={styles.table}><thead><tr><th scope="col">Case</th><th scope="col">Status</th><th scope="col">Updated</th></tr></thead><tbody>{data.cases.slice(0, 3).map(item => <tr key={item.assessmentId}><td><Link href={`/component-04/cases/assessment/${encodeURIComponent(item.assessmentId)}`}>{item.caseId}</Link></td><td>{item.overallStatus}</td><td>{formatGovernanceTime(item.evaluatedAt)}</td></tr>)}</tbody></table></div><div className={styles.actions}><Button href="/component-04/cases" variant="secondary">View all assessments</Button></div></> : <EmptyState title={data ? "No assessments recorded" : "Recent cases unavailable"} description={data ? "There are no records to display." : "Live case history is not connected."} />}
      </section><section className={styles.card}><h2>Governance Notice</h2><p>Outputs support authorized officers and do not constitute final legal approval or rejection.</p><p className={styles.muted}>Machine predictions require human review. No workflow is ended, referred or approved by opening these pages.</p></section></div>
    </div>
  </>;
}
