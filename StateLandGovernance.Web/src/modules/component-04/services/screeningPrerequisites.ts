import type { CaseScreeningState } from "../../workflow-governance/types/caseScreening";

export type PrerequisiteFeedback = {
  kind: "idle" | "loading" | "ready" | "blocked" | "unconfirmed" | "error";
  message: string;
};

export async function checkScreeningPrerequisites(
  load: () => Promise<unknown>,
  publish: (feedback: PrerequisiteFeedback) => void,
): Promise<CaseScreeningState> {
  publish({ kind: "loading", message: "Checking prerequisites…" });
  try {
    const value = await load();
    if (!value || typeof value !== "object") throw new Error("The readiness endpoint returned no screening state. Please retry.");
    const state = value as CaseScreeningState;
    if (!state.caseId || !Number.isInteger(state.caseRevision) ||
      !["BlockedIncomplete", "ReadyForScreening", "Assessed", "Stale", "Pending"].includes(state.status) ||
      !Array.isArray(state.missingInputs) || state.missingInputs.some(reason => typeof reason !== "string" || !reason.trim()))
      throw new Error("The readiness endpoint returned an incomplete screening state. Please retry.");
    if (state.missingInputs.length > 0)
      publish({ kind: "blocked", message: "Prerequisites missing: " + state.missingInputs.join(" ") });
    else if (state.status === "ReadyForScreening" && state.verifiedFactSnapshotId)
      publish({ kind: "ready", message: "Ready to screen" });
    else
      publish({ kind: "unconfirmed", message: `Readiness not confirmed. Returned status: ${state.status}. No screening was run.` });
    return state;
  } catch (error) {
    publish({ kind: "error", message: error instanceof Error ? error.message : "Could not check prerequisites. Please retry." });
    throw error;
  }
}
