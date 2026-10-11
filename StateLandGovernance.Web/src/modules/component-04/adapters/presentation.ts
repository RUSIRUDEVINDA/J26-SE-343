import type { GovernanceCaseAssessment, GovernanceFilter } from "../types/governance";

export function formatGovernanceTime(value: string): string {
  // Naive model event timestamps must not be silently interpreted as UTC.
  if (!/(Z|[+-]\d{2}:\d{2})$/i.test(value)) return `${value} (timezone not supplied)`;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Timestamp unavailable";
  return `${new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Colombo", dateStyle: "medium", timeStyle: "short" }).format(date)} · Asia/Colombo`;
}
export function filterAssessments(cases: GovernanceCaseAssessment[], query: string, filter: GovernanceFilter) {
  const expected = { ALL: null, REVIEW_REQUIRED: "ReviewRequired", CLEAR: "Clear", INSUFFICIENT: "InsufficientInformation" }[filter];
  const search = query.trim().toLowerCase();
  return cases.filter(item => (!expected || item.overallStatus === expected) &&
    [item.caseId, item.assessmentId, item.parcelReference, item.applicantReference].some(value => value.toLowerCase().includes(search)));
}
export function conflictTone(status: string): "success" | "danger" | "neutral" {
  return status === "Clear" || status === "NoConflicts" ? "success" : status === "ConflictDetected" || status === "ConflictsDetected" ? "danger" : "neutral";
}
