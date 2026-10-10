"use client";
import { useEffect, useState } from "react";
import { useGovernanceMode } from "../components/GovernanceWorkspace";
import { getGovernanceService, GovernanceUnavailableError } from "../services/governanceService";
import type { GovernanceDashboardData } from "../types/governance";
type State = { status: "loading" } | { status: "success"; data: GovernanceDashboardData } | { status: "unavailable" | "error"; message: string };
export function useGovernanceData() {
  const mode = useGovernanceMode();
  const [attempt, setAttempt] = useState(0);
  const [state, setState] = useState<State>({ status: "loading" });
  useEffect(() => {
    let active = true;
    getGovernanceService(mode).getDashboard().then(data => {
      if (active) setState({ status: "success", data });
    }).catch((error: unknown) => {
      if (active) setState({ status: error instanceof GovernanceUnavailableError ? "unavailable" : "error", message: error instanceof Error ? error.message : "Unable to load governance records." });
    });
    return () => { active = false; };
  }, [mode, attempt]);
  return { state, retry: () => { setState({ status: "loading" }); setAttempt(value => value + 1); } };
}
