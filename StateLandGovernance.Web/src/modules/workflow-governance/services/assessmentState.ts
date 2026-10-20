export function assessmentState(assessment: unknown, current: boolean): "NotAssessed" | "Current" | "Stale" {
  return assessment == null ? "NotAssessed" : current ? "Current" : "Stale";
}
