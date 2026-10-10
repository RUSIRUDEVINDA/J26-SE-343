"use client";
import { useState } from "react";
import { Button } from "@/shared/components/Button";
import { LoadingState } from "@/shared/components/LoadingState";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { AssessmentTable } from "./AssessmentTable";
import { AssessmentDetailDrawer } from "./AssessmentDetailDrawer";
import { useGovernanceData } from "../hooks/useGovernanceData";
import { filterAssessments } from "../adapters/presentation";
import type { GovernanceFilter } from "../types/governance";
import styles from "./GovernanceWorkspace.module.css";

export function CaseAssessments() {
  const { state, retry } = useGovernanceData();
  const [query, setQuery] = useState("");
  const [filter, setFilter] = useState<GovernanceFilter>("ALL");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const cases = state.status === "success" ? filterAssessments(state.data.cases, query, filter) : [];
  const selected = cases.find(item => item.assessmentId === selectedId);
  return <div className={styles.stack}>
    <header className={styles.heading}><h1>Case Assessments</h1><p>Inspect recorded findings by assessment identity. The demo list contains one fictional snapshot per case.</p></header>
    {state.status === "loading" && <LoadingState message="Loading assessments…" />}
    {state.status === "unavailable" && <p className={styles.notice} role="status">{state.message}</p>}
    {state.status === "error" && <><ErrorMessage title="Unable to load assessments" message={state.message} /><Button onClick={retry}>Retry loading</Button></>}
    {state.status === "success" && <>
      <AssessmentTable cases={cases} selectedCaseId={selected?.assessmentId ?? null} onSelectCase={item => setSelectedId(item.assessmentId)} searchQuery={query} onSearchChange={value => { setQuery(value); setSelectedId(null); }} activeFilter={filter} onFilterChange={value => { setFilter(value); setSelectedId(null); }} />
      {selected && <aside aria-label="Selected assessment"><AssessmentDetailDrawer caseAssessment={selected} onClose={() => setSelectedId(null)} /></aside>}
    </>}
  </div>;
}
