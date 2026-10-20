export type ReviewFilter = "Needs review" | "All" | "Missing" | "Reviewed";
export const reviewFilters: ReviewFilter[] = ["Needs review", "All", "Missing", "Reviewed"];
export function matchesFactFilter(status: string, filter: ReviewFilter): boolean {
  const reviewed = ["Confirmed", "Corrected", "Rejected", "Verified", "Unsupported"].includes(status);
  const missing = ["NoCandidate", "Missing", "NotExtracted"].includes(status);
  return filter === "All" || (filter === "Reviewed" ? reviewed : filter === "Missing" ? missing : !reviewed);
}
export function matchesObservationFilter(state: string, filter: ReviewFilter): boolean {
  // Present is a finding, not proof of officer approval.
  return filter === "All" || (filter === "Reviewed" ? false : filter === "Missing" ? state === "Missing" : state !== "Present");
}
