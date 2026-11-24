"use client";

import { apiGet, apiPost } from "@/lib/apiClient";
import { useEffect, useState, useRef } from "react";
import Link from "next/link";
import { Button } from "@/shared/components/Button";
import { getCaseScreening, runCaseScreening } from "@/modules/workflow-governance/services/caseScreeningApi";
import type { CaseScreeningState } from "@/modules/workflow-governance/types/caseScreening";
import styles from "./GovernanceWorkspace.module.css";
import { useGovernanceMode } from "./GovernanceWorkspace";

import { AssessmentReport } from "./AssessmentReport";
import { checkScreeningPrerequisites, type PrerequisiteFeedback } from "../services/screeningPrerequisites";

const sections = ["Regulatory", "Conflict", "Early Governance", "Summary"] as const;
export function ConsolidatedCaseScreening({ caseId, snapshotId }: { caseId: string; snapshotId?: string }) {
  function openCaseView(view: "facts" | "completeness") {
    const target = `/workflow-governance/cases/${caseId}`;
    if(window.location.pathname === target) window.dispatchEvent(new CustomEvent("open-case-view",{detail:view}));
    else window.location.assign(`${target}?view=${view}`);
  }
  const mode = useGovernanceMode();
  const request = useRef(0);
  const scope = useRef(caseId); scope.current = caseId;
  useEffect(() => { request.current++; sessionStorage.setItem("c4-selected-case", caseId); setExplicitCoverageConsent(false); setDispatchNotice(""); setScenario(undefined); setParcel(""); setTab("Regulatory");setBusy(false);setMockSelected(false);setSyntheticCoverage(false);setNoConflictSelected(false);setDesignationReason("");setConfirmSynthetic(false); }, [caseId]);
  const [state, setState] = useState<CaseScreeningState | null>(null);
  const [parcel, setParcel] = useState("");
  const [scenario, setScenario] = useState<{available:boolean;canDesignate?:boolean;label:string;spatialParcelId:string;syntheticEvidenceParcelId?:string;noConflictSpatialParcelId?:string;noConflictSyntheticEvidenceParcelId?:string;researchAccount:string;researchLabel:string}>();
  const [explicitCoverageConsent,setExplicitCoverageConsent]=useState(false);
  const [noConflictSelected,setNoConflictSelected]=useState(false);
  const [mockSelected,setMockSelected]=useState(false);const [syntheticCoverage,setSyntheticCoverage]=useState(false);
  const [designationReason,setDesignationReason]=useState("");const [confirmSynthetic,setConfirmSynthetic]=useState(false);
  async function loadScenarios(){
    try {const dynamic=await apiGet<typeof scenario>(`/api/development-auth/cases/${caseId}/screening-scenarios`,undefined,"officer");if(scope.current===caseId)setScenario(dynamic);}
    catch {try{const legacy=await apiGet<typeof scenario>(`/api/development-auth/screening-scenarios?caseId=${caseId}`,undefined,"officer");if(scope.current===caseId)setScenario(legacy);}catch{if(scope.current===caseId)setScenario(undefined);}}
  }
  useEffect(()=>{void loadScenarios();},[caseId]);
  async function designate(){setBusy(true);setError(null);try{
    const source=await apiGet<{proposalSelection:{documentId:string;versionId:string}}>(`/api/workflow-governance/cases/${caseId}`,undefined,"officer");
    await apiPost(`/api/development-auth/cases/${caseId}/screening-scenarios/designation`,{documentId:source.proposalSelection.documentId,versionId:source.proposalSelection.versionId,reason:designationReason,confirmSynthetic},undefined,"officer");await loadScenarios();
  }catch(e){setError(e instanceof Error?e.message:"Synthetic designation rejected");}finally{setBusy(false);}}

  useEffect(()=>{if(scenario && parcel){setMockSelected([scenario.spatialParcelId,scenario.syntheticEvidenceParcelId,scenario.noConflictSpatialParcelId,scenario.noConflictSyntheticEvidenceParcelId].includes(parcel));setSyntheticCoverage(parcel===scenario.syntheticEvidenceParcelId||parcel===scenario.noConflictSyntheticEvidenceParcelId);setNoConflictSelected(parcel===scenario.noConflictSpatialParcelId||parcel===scenario.noConflictSyntheticEvidenceParcelId);}},[scenario,parcel]);
  const [tab, setTab] = useState<(typeof sections)[number]>("Regulatory");
  const [dispatchNotice,setDispatchNotice]=useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [prerequisiteFeedback, setPrerequisiteFeedback] = useState<PrerequisiteFeedback>({kind:"idle",message:""});
  useEffect(()=>{request.current++;setPrerequisiteFeedback({kind:"idle",message:""});},[caseId,snapshotId,parcel]);
  useEffect(() => {
    let active = true;
    setState(null); setError(null);
    if (mode !== "live") return () => { active = false; };
    getCaseScreening(caseId, snapshotId).then(data => { if (active) { setState(data); setParcel(data.parcelId || ""); } })
      .catch(err => { if (active) setError(err instanceof Error ? err.message : "Screening state is unavailable."); });
    return () => { active = false; };
  }, [caseId, snapshotId, mode]);
  useEffect(() => {
    if (mode !== "live" || busy) return;
    const recheck = () => {
      setPrerequisiteFeedback({kind:"idle",message:""});
      const sequence=++request.current;
      getCaseScreening(caseId, state?.verifiedFactSnapshotId || snapshotId, scenario?.available && !mockSelected && !parcel ? "00000000-0000-0000-0000-000000000000" : parcel || undefined)
        .then(data=>{if(scope.current===caseId && sequence===request.current)setState(data);}).catch(err => {
          if(scope.current!==caseId||sequence!==request.current)return;
          setError(err instanceof Error ? err.message : "Currentness cannot be verified.");
          setState(previous => previous ? { ...previous, isCurrent: false, status: "Stale" } : null);
        });
    };
    window.addEventListener("focus", recheck);
    return () => window.removeEventListener("focus", recheck);
  }, [caseId, snapshotId, state?.verifiedFactSnapshotId, parcel, mode, scenario?.available, mockSelected, busy]);
  async function refresh(run = false) {
    const sequence=++request.current;
    setBusy(true); setError(null);
    try {
      const load = () => getCaseScreening(caseId, state?.verifiedFactSnapshotId || snapshotId, scenario?.available && !mockSelected && !parcel ? "00000000-0000-0000-0000-000000000000" : parcel || undefined);
      const current = run ? await load() : await checkScreeningPrerequisites(load, feedback => {
        if(scope.current===caseId && sequence===request.current)setPrerequisiteFeedback(feedback);
      });
      if(scope.current!==caseId||sequence!==request.current)return;
      setState(current);
      if (run && current.missingInputs.length === 0) {const started=Date.now();const result=await runCaseScreening(current);if(scope.current===caseId&&sequence===request.current){setState(result);setDispatchNotice(Date.parse(result.latestAssessment?.assessment.evaluatedAtUtc||"")<started?"Reused persisted assessment returned. No fresh engine execution is claimed.":"Assessment returned and recorded by C3.");}}
    } catch (err) {
      if(scope.current!==caseId||sequence!==request.current)return;
      setError(err instanceof Error ? err.message : "Screening failed. No result is treated as current.");
      setState(previous => previous ? { ...previous, isCurrent: false, status: "Stale" } : null);
    } finally { if(scope.current===caseId)setBusy(false); }
  }
  const selectedSpatialId=noConflictSelected?scenario?.noConflictSpatialParcelId:scenario?.spatialParcelId;
  const selectedCoverageId=noConflictSelected?scenario?.noConflictSyntheticEvidenceParcelId:scenario?.syntheticEvidenceParcelId;
  const assessment = state?.latestAssessment?.assessment;
  const section = tab === "Regulatory" ? assessment?.regulatoryCheck : tab === "Conflict" ? assessment?.conflictCheck
    : tab === "Early Governance" ? assessment?.earlyGovernanceCheck : undefined;
  const findings = assessment?.consolidatedFindings.filter(f => tab === "Summary" || f.dimension === (tab === "Early Governance" ? "EarlyGovernance" : tab));
  if (mode !== "live") return <p role="status">Actual case screening is available in Live workspace only. No development fixture is substituted.</p>;
  return <section className={`${styles.stack} ${styles.screening}`} aria-label="Consolidated case screening">
    <header className={styles.heading}><h1>Case screening</h1>{assessment?.applicationReference&&<p>Case {assessment.applicationReference}</p>}<p>One consolidated C4 request returns Regulatory, Conflict and Early Governance sections. These tabs do not issue separate screening calls.</p></header>
    <details><summary>Case and snapshot identifiers</summary><p>Case ID: <code>{caseId}</code><br />Exact verified snapshot: <code>{state?.verifiedFactSnapshotId || snapshotId || "Not published / linked"}</code></p></details>
    <div className={styles.actions}><Link href={`/workflow-governance/cases/${caseId}`}>Officer case workspace</Link><Link href="/component-04/screening">Screening queue</Link><Link href={`/component-04/screening/cases/${caseId}${state?.verifiedFactSnapshotId ? `?snapshotId=${state.verifiedFactSnapshotId}` : ""}`}>C4 view of this case</Link></div>
    {error && <p role="alert">{error}</p>}
    {!state && !error && <p role="status">Loading source-backed screening readiness…</p>}
    {state && <>
      <p role="status"><strong>{assessment?`${state.isCurrent?"Overall assessment":"Historical assessment"}: ${assessment.overallStatus}`:state.status}</strong> · {state.isCurrent ? "Current for the displayed input bindings" : "No current accepted screening result"}</p>
      {state.isDevelopmentSpatialMock && <details><summary>Demonstration inputs · Live engine results</summary><p>{state.spatialProvenanceLabel}. Uploaded OCR/Gemini facts remain unchanged. No real parcel or complaint clearance is established.</p></details>}
      {scenario?.canDesignate&&!scenario.available&&<fieldset><legend>Development synthetic source designation</legend><p>Only designate an explicitly synthetic research Proposal you have inspected. This records the exact current document/version, officer and reason; filenames confer no authority. Facts, reviews and completeness remain unchanged.</p><label><input type="checkbox" checked={confirmSynthetic} onChange={e=>setConfirmSynthetic(e.target.checked)}/> I reviewed the source and confirm this is a synthetic research test, not a real application.</label><label>Designation reason <textarea value={designationReason} onChange={e=>setDesignationReason(e.target.value)}/></label><Button disabled={busy||!confirmSynthetic||!designationReason.trim()} onClick={()=>void designate()}>Designate current Proposal for Development screening</Button></fieldset>}
      {scenario?.available && <details className={styles.card}><summary>Development input controls</summary><fieldset><legend>Explicit Development scenario selection</legend>
        <label>Spatial input scenario <select aria-label="Spatial input scenario" value={mockSelected?(noConflictSelected?"no-conflict":"mock"):"real"} onChange={e=>{
          request.current++; const mock=e.target.value!=="real";const noConflict=e.target.value==="no-conflict";setNoConflictSelected(noConflict);setMockSelected(mock);setSyntheticCoverage(false);setExplicitCoverageConsent(false);setParcel(mock?(noConflict?scenario.noConflictSpatialParcelId!:scenario.spatialParcelId):"");setState(s=>s?{...s,isCurrent:false,status:s.latestAssessment?"Stale":"BlockedIncomplete",missingInputs:["Check prerequisites for the newly selected Development scenario."]}:s);
        }}><option value="real">Actual C1 parcel evidence</option><option value="mock">{scenario.label}</option>{scenario.noConflictSpatialParcelId&&<option value="no-conflict">Development mock 12-acre no-conflict inputs (synthetic; engine outcome not predetermined)</option>}</select></label>
        {mockSelected&&<p>Test parcel context selected; no real C1 association.</p>}
        {mockSelected&&selectedCoverageId&&<label><input type="checkbox" checked={syntheticCoverage&&explicitCoverageConsent} onChange={e=>{request.current++;setExplicitCoverageConsent(e.target.checked);setSyntheticCoverage(e.target.checked);setParcel(e.target.checked?selectedCoverageId!:selectedSpatialId!);setState(s=>s?{...s,isCurrent:false,status:s.latestAssessment?"Stale":"BlockedIncomplete",missingInputs:["Check prerequisites for the newly selected Development scenario."]}:s);}}/> Explicitly use synthetic governance, complaint and concurrent-application coverage for this explicitly designated synthetic research case. Not real clearance.</label>}
        {!selectedCoverageId&&<p>Governance and complaint coverage remain unknown for this case. Authorized, source-backed coverage is required before screening can proceed. {scenario.researchLabel}</p>}
        {scenario.syntheticEvidenceParcelId&&<p role="alert">Research NPD policy only. Source facts and completeness remain unchanged.</p>}
      </fieldset></details>}
      <form onSubmit={event => { event.preventDefault(); void refresh(); }} className={styles.actions}>
        {!mockSelected && <label>C1 parcel ID <input aria-label="C1 parcel ID" value={parcel} placeholder="Actual parcel UUID" onChange={e => { request.current++;setParcel(e.target.value); setState(previous => previous ? { ...previous, isCurrent: false, status: "Stale" } : null); }} /></label>}
        <Button type="submit" variant="secondary" disabled={busy}>{prerequisiteFeedback.kind==="loading"?"Checking prerequisites…":"Check prerequisites"}</Button>
      <Button disabled={busy || state.missingInputs.length > 0 || !state.verifiedFactSnapshotId || Boolean(mockSelected&&selectedCoverageId&&!explicitCoverageConsent)} onClick={() => void refresh(true)}>{busy ? "Checking…" : "Run consolidated screening"}</Button>
      </form>
      {prerequisiteFeedback.kind!=="idle"&&<p role={prerequisiteFeedback.kind==="error"?"alert":"status"} aria-live="polite">{prerequisiteFeedback.message}</p>}
      {state.missingInputs.length > 0 && <section className={styles.card}><h2>Screening blocked / incomplete</h2><ul>{state.missingInputs.map(reason => <li key={reason}>{reason} {(/completeness|template|classification/i.test(reason)) ? <button onClick={()=>openCaseView("completeness")}>Open completeness review</button> : /snapshot|investment|verified fact/i.test(reason) ? <button onClick={()=>openCaseView("facts")}>Open fact review</button> : null}</li>)}</ul><p>No C4 evaluation was substituted for missing evidence.</p></section>}
      {dispatchNotice&&<p role="status">{dispatchNotice}</p>}
      <nav className={styles.tabs} aria-label="Screening sections">{sections.map(name => <button key={name} type="button" aria-current={tab === name ? "step" : undefined} onClick={() => setTab(name)}>{name}</button>)}</nav>
      {assessment ? <><AssessmentReport key={`${assessment.assessmentId}-${tab}`} assessment={assessment} section={tab} historical={!state.isCurrent}/></> : <section className={styles.card}><h2>{tab}</h2><p>Not evaluated. Complete the prerequisites before running screening.</p></section>}

    </>}
    <p>Findings inform authorized officer review. They do not approve, reject or complete the application.</p>
  </section>;
}
