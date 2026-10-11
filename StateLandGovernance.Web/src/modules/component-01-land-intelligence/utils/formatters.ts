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

export type RecommendationResultStatus =
  | "recommended"
  | "needs-review"
  | "hard-rejected";

export function recommendationResultStatus(
  score: number,
  hardRejected: boolean,
): RecommendationResultStatus {
  if (hardRejected) {
    return "hard-rejected";
  }
  if (score >= 80) {
    return "recommended";
  }
  return "needs-review";
}

export function suitabilityBadgeLabel(score: number, hardRejected: boolean): string {
  switch (recommendationResultStatus(score, hardRejected)) {
    case "hard-rejected":
      return "Hard constraint rejected";
    case "recommended":
      return "Recommended";
    default:
      return "Needs review";
  }
}

/** Extracts source-reported land-use label from purpose-alignment criterion text when present. */
export function extractSourceReportedLandUseFromCriteria(
  summaries: Array<string | null | undefined>,
): string | null {
  for (const summary of summaries) {
    if (!summary) {
      continue;
    }
    const match = summary.match(
      /Source-reported land use \(([^)]+)\)/i,
    );
    if (match?.[1] && match[1].trim().toLowerCase() !== "unavailable") {
      return match[1].trim();
    }
  }
  return null;
}

/** Splits a long explanation into short bullets for progressive disclosure. */
export function explanationToBullets(explanation: string): {
  lead: string;
  positives: string[];
  concerns: string[];
} {
  const text = explanation.trim();
  if (!text) {
    return { lead: "Unavailable", positives: [], concerns: [] };
  }

  const positiveMatch = text.match(
    /Positive factors:\s*([\s\S]+?)(?:\.\s*Negative factors:|\.\s*This parcel|$)/i,
  );
  const negativeMatch = text.match(
    /Negative factors:\s*([\s\S]+?)(?:\.\s*This parcel|$)/i,
  );

  const splitFactors = (raw: string | undefined) =>
    (raw ?? "")
      .split(";")
      .map((part) => part.replace(/\.\s*$/, "").trim())
      .filter(Boolean);

  const lead =
    text.split(/Positive factors:/i)[0]?.trim().replace(/\.$/, "") || text;

  return {
    lead,
    positives: splitFactors(positiveMatch?.[1]),
    concerns: splitFactors(negativeMatch?.[1]),
  };
}
