import type { GisIntelligenceCoverageStatus } from "../types/landIntelligence";

export function gisCoverageStatusLabel(
  status: GisIntelligenceCoverageStatus,
): string {
  switch (status) {
    case "Available":
      return "Available";
    case "Partial":
      return "Partial";
    default:
      return "Unavailable";
  }
}

export function gisCoverageBadgeTone(
  status: GisIntelligenceCoverageStatus,
): "success" | "warning" | "neutral" {
  switch (status) {
    case "Available":
      return "success";
    case "Partial":
      return "warning";
    default:
      return "neutral";
  }
}
