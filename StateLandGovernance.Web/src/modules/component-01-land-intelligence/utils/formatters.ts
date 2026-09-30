import { areaUnitLabels } from "./enumMappings";
import type { AreaUnit } from "../types/landIntelligence";

/** Formats the rule-based suitability score (0–100 scale from the API). Not a probability. */
export function formatRuleScore(score: number): string {
  if (Number.isNaN(score)) {
    return "Unavailable";
  }
  return score.toFixed(1);
}

/** @deprecated Prefer formatRuleScore — retained for callers that expect a labelled score. */
export function formatPercent(score: number): string {
  return formatRuleScore(score);
}

export function formatArea(value: number, unit: AreaUnit): string {
  if (value === undefined || value === null || Number.isNaN(value)) {
    return "Unavailable";
  }
  const unitLabel = areaUnitLabels[unit] ?? "Unknown";
  return `${value} ${unitLabel}`;
}

export function displayOrUnavailable(value: string | null | undefined): string {
  if (value === undefined || value === null || value.trim() === "") {
    return "Unavailable";
  }
  return value;
}

export function displayOrUnknown(value: string | null | undefined): string {
  if (value === undefined || value === null || value.trim() === "") {
    return "Unknown";
  }
  return value;
}

export function suitabilityBadgeLabel(score: number, hardRejected: boolean): string {
  if (hardRejected) {
    return "Hard constraint rejected";
  }
  if (score >= 80) {
    return "Higher rule-based score";
  }
  if (score >= 60) {
    return "Mid rule-based score";
  }
  return "Lower rule-based score";
}
