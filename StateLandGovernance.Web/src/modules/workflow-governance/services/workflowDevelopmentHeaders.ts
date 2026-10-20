import { getLandIntelligenceOwnerId } from "@/modules/component-01-land-intelligence/utils/landIntelligenceOwner";

/** Explicit localhost Development-only counterpart of the existing C3 server header scheme. */
export function workflowDevelopmentHeaders(role: "officer" | "applicant" | "none"): RequestInit | undefined {
  if (process.env.NODE_ENV !== "development" || process.env.NEXT_PUBLIC_WORKFLOW_GOVERNANCE_DEV_AUTH !== "true"
    || typeof window === "undefined" || !["localhost", "127.0.0.1"].includes(window.location.hostname) || role === "none") return undefined;
  return { headers: { "X-Actor-Role": role === "officer" ? "Officer" : "Applicant",
    "X-Actor-Id": role === "officer" ? "22222222-2222-2222-2222-222222222222" : getLandIntelligenceOwnerId() } };
}

export function workflowAuthRole(role: "officer" | "applicant" | "none"): "officer" | "applicant" | "none" {
  // An explicitly selected C3 local session does not mint unrelated C1 tokens.
  return workflowDevelopmentHeaders(role) ? "none" : role;
}
