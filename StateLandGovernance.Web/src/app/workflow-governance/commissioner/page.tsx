"use client";
import Link from "next/link";
import { useEffect, useState } from "react";
import { apiGet } from "@/lib/apiClient";
import { workflowDevelopmentHeaders, workflowAuthRole } from "@/modules/workflow-governance/services/workflowDevelopmentHeaders";
export default function CommissionerWorkflowDashboard() {
  const [ids, setIds] = useState<string[]>(); const [error, setError] = useState("");
  useEffect(() => { apiGet<string[]>("/api/workflow-governance/cases/workflow-execution/rejected", workflowDevelopmentHeaders("officer"), workflowAuthRole("officer"))
    .then(setIds).catch(e => setError(e instanceof Error ? e.message : "Authenticated Commissioner capability and case scopes required")); }, []);
  return <section><h1>Commissioner rejected-case reports</h1><p>Persisted rejection reports inform review. Visibility does not override an external rejection or authorize approval.</p>
    {error && <p role="alert">{error}</p>}{ids && (ids.length ? <ul>{ids.map(id => <li key={id}><Link href={`/workflow-governance/cases/${id}`}>{id}</Link> — institutional rejection recorded</li>)}</ul>
      : <p>No rejected execution cases in your authorized case scopes.</p>)}</section>;
}
