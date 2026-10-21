"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { listCases } from "@/modules/workflow-governance/services/workflowGovernanceApi";
import type { LeaseCaseSummary } from "@/modules/workflow-governance/types/caseManagement";
import styles from "./GovernanceWorkspace.module.css";
import { useGovernanceMode } from "./GovernanceWorkspace";

export function CaseScreeningQueue() {
  const mode = useGovernanceMode();
  const [cases, setCases] = useState<LeaseCaseSummary[]>([]);
  const [skip, setSkip] = useState(0);
  const [total, setTotal] = useState(0);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    let active = true;
    if (mode !== "live") return () => { active = false; };
    listCases(skip, 20).then(page => { if (active) { setCases(page.items); setTotal(page.totalCount); setError(null); } })
      .catch(err => { if (active) setError(err instanceof Error ? err.message : "Queue unavailable."); });
    return () => { active = false; };
  }, [skip, mode]);
  if (mode !== "live") return <p role="status">Actual C3 cases are available in Live workspace only. No development fixture is substituted.</p>;
  return <section className={styles.stack}><header className={styles.heading}><h1>C3–C4 screening queue</h1><p>Actual C3 cases and their exact linked verified snapshots. Opening a case checks prerequisites; it does not run screening.</p></header>
    <p><Link href="/component-04/development">Development input scenarios</Link> - only explicitly designated synthetic cases with active assignments can submit test inputs.</p>
    {error && <p role="alert">{error}</p>}{!error&&cases.length===0&&<p>No cases loaded for this identity. Only assigned coordinating work is shown; institution reviewers use My restricted institutional requests.</p>}
    <div className={styles.scroll}><table className={styles.table}><thead><tr><th>Application / Case ID</th><th>Verified snapshot</th><th>Review</th></tr></thead><tbody>{cases.map(item => {
      const snapshot = item.currentVerifiedFactSnapshotId && item.currentVerifiedFactSnapshotId !== "00000000-0000-0000-0000-000000000000" ? item.currentVerifiedFactSnapshotId : undefined;
      return <tr key={item.id}><td>{item.applicationReference}<details><summary>Case identifier</summary><code>{item.id}</code></details></td><td>{snapshot?<details><summary>Published snapshot</summary>{snapshot}</details>:"Not published / linked"}</td><td><Link href={`/component-04/screening/cases/${item.id}${snapshot ? `?snapshotId=${snapshot}` : ""}`}>Open screening</Link> · <Link href={`/workflow-governance/cases/${item.id}`}>C3 workspace</Link></td></tr>;
    })}</tbody></table></div>
    <div className={styles.actions}><button disabled={skip === 0} onClick={() => setSkip(Math.max(0, skip - 20))}>Previous cases</button><span>{total} cases</span><button disabled={skip + 20 >= total} onClick={() => setSkip(skip + 20)}>Next cases</button></div>
  </section>;
}
