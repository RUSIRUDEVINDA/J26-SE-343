"use client";
import { useCallback, useEffect, useState } from "react";
import { apiGet, apiPost } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { workflowDevelopmentHeaders, workflowAuthRole } from "../services/workflowDevelopmentHeaders";

type State = { caseRevision: number; canPrepare: boolean; canDispatch: boolean; missingInputs: string[];
  package?: { id: string; recipientContract: string; acknowledgementId?: string; preparedAtUtc: string;
    dispatchHistory: { attemptId: string; acceptedByTransport: boolean; attemptedAtUtc: string }[] } };
export function GovernedHandoffWorkspace({ caseId }: { caseId: string }) {
  const base = `/api/workflow-governance/cases/${caseId}/handoff`;
  const [state, setState] = useState<State>(); const [error, setError] = useState(""); const [busy, setBusy] = useState(false);
  const refresh = useCallback(async () => { try { setState(await apiGet<State>(base, workflowDevelopmentHeaders("officer"), workflowAuthRole("officer"))); }
    catch (e) { setError(e instanceof Error ? e.message : "Handoff unavailable"); } }, [base]);
  useEffect(() => { void refresh(); }, [refresh]);
  async function act(action: "prepare" | "dispatch") {
    if (!state) return; setBusy(true); setError("");
    try { await apiPost(`${base}/${action}`, action === "prepare" ? { expectedCaseRevision: state.caseRevision,
      packageId: crypto.randomUUID(), idempotencyKey: crypto.randomUUID() } : { expectedCaseRevision: state.caseRevision, packageId: state.package!.id },
      workflowDevelopmentHeaders("officer"), workflowAuthRole("officer")); }
    catch (e) { setError(e instanceof Error ? e.message : "Handoff action rejected"); } finally { await refresh(); setBusy(false); }
  }
  return <section aria-label="Governed downstream handoff"><h3>Downstream handoff</h3>
    <p>Preparation does not establish delivery. Only the authenticated recipient adapter can supply acknowledgement.</p>
    {error && <p role="alert">{error}</p>}
    <ul>{state?.missingInputs.map(m => <li key={m}>{m}</li>)}</ul>
    {state?.package && <><p>Persisted package {state.package.id} · {state.package.recipientContract}</p>
      <p>{state.package.acknowledgementId ? `Acknowledged: ${state.package.acknowledgementId}` : "Awaiting downstream acknowledgement"}</p>
      <ul>{state.package.dispatchHistory.map(d => <li key={d.attemptId}>{d.attemptedAtUtc}: {d.acceptedByTransport ? "Transport accepted" : "Transport failed"}</li>)}</ul></>}
    <Button disabled={busy || !state?.canPrepare} onClick={() => void act("prepare")}>Prepare with verified authority</Button>
    <Button disabled={busy || !state?.canDispatch} onClick={() => void act("dispatch")}>Dispatch exact persisted package</Button>
  </section>;
}
