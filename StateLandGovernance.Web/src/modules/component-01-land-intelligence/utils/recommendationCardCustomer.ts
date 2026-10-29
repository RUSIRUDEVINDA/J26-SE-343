import type {
  CriterionEvaluationResponse,
  LandParcelRecommendationResponse,
  RecommendationEvidenceResponse,
  RestrictionSummaryResponse,
} from "../types/landIntelligence";
import { formatRuleScore, recommendationResultStatus } from "./formatters";

const MAX_MATCH_REASONS = 3;

/** Criteria hidden on the customer results card (still scored server-side). */
const HIDDEN_CUSTOMER_CRITERION_KEYS = new Set(["spatial-constraints"]);

export function customerVisibleCriteria(
  criteria: CriterionEvaluationResponse[],
): CriterionEvaluationResponse[] {
  return criteria.filter((c) => !HIDDEN_CUSTOMER_CRITERION_KEYS.has(c.key));
}

/** Strips scoring-formula tails from API criterion summaries for the results card. */
export function customerCriterionSummary(
  criterion: CriterionEvaluationResponse,
): string {
  if (criterion.wasAssessed === false) {
    const reason =
      criterion.notAssessedReason?.trim() || criterion.summary?.trim();
    return reason || "Not assessed";
  }

  let summary = criterion.summary?.trim() ?? "";
  const unroundedIdx = summary.indexOf("(unrounded for scoring)");
  if (unroundedIdx >= 0) {
    summary = summary.slice(0, unroundedIdx).trim().replace(/\.\s*$/, "");
  }
  summary = summary.replace(/\s*Area match score[\s\S]*$/i, "").trim();

  if (!summary) {
    return criterion.name?.trim() || "—";
  }
  return summary;
}

const TARGET_AREA_SUMMARY_PATTERN =
  /target area\s+([\d.]+)\s*ha\s*;\s*parcel area\s+([\d.]+)\s*ha/i;

function formatHectaresForDisplay(raw: string): string {
  const value = Number.parseFloat(raw);
  if (Number.isNaN(value)) {
    return raw.trim();
  }
  return `${value.toFixed(3)} ha`;
}

/** Customer-facing criterion detail lines (e.g. target vs parcel area on separate lines). */
export function customerCriterionSummaryLines(
  criterion: CriterionEvaluationResponse,
): string[] {
  let raw = criterion.summary?.trim() ?? "";
  const unroundedIdx = raw.indexOf("(unrounded for scoring)");
  if (unroundedIdx >= 0) {
    raw = raw.slice(0, unroundedIdx).trim().replace(/\.\s*$/, "");
  }
  const match = raw.match(TARGET_AREA_SUMMARY_PATTERN);
  if (match) {
    return [
      `Target area ${formatHectaresForDisplay(match[1])}`,
      `Parcel area ${formatHectaresForDisplay(match[2])}`,
    ];
  }

  return [customerCriterionSummary(criterion)];
}

function customerPlainReasonFromCriterion(
  criterion: CriterionEvaluationResponse,
): string | null {
  if (criterion.wasAssessed === false || !criterion.isMet) {
    return null;
  }

  const summary = criterion.summary?.trim() ?? "";

  switch (criterion.key) {
    case "target-area":
      return "Parcel size is close to the target area in your search.";
    case "purpose-alignment": {
      const landUseMatch = summary.match(
        /Source-reported land use \(([^)]+)\)[^()]*requested purpose \(([^)]+)\)/i,
      );
      if (landUseMatch) {
        return `Recorded land use (${landUseMatch[1]}) fits your stated purpose (${landUseMatch[2]}).`;
      }
      const domainMatch = summary.match(
        /Parcel current use \(([^)]+)\) aligns with requested purpose/i,
      );
      if (domainMatch) {
        return `Recorded use (${domainMatch[1]}) matches your stated purpose.`;
      }
      return "Recorded land use fits your stated purpose.";
    }
    case "location-preference":
      return "Location matches the administrative area you specified in your search.";
    case "land-use":
      return "Land use matches what you asked for in this search.";
    case "land-category":
      return "Land category matches what you asked for in this search.";
    case "regulatory":
      return "No regulatory issues were flagged for this parcel in the criteria checked.";
    case "custom-criteria":
      return criterion.name
        ? `${criterion.name} met your search requirements.`
        : "Additional search requirements were met.";
    default:
      return criterion.name
        ? `${criterion.name} met your search criteria.`
        : null;
  }
}

export function customerRecommendationIntro(
  recommendation: LandParcelRecommendationResponse,
): string {
  const label = recommendation.cadastralNumber?.trim() || "This parcel";
  const scoreText = formatRuleScore(recommendation.suitabilityScore);

  if (recommendation.hardConstraintRejected) {
    return `${label} did not pass a required check for this search and is not recommended.`;
  }

  const status = recommendationResultStatus(
    recommendation.suitabilityScore,
    recommendation.hardConstraintRejected,
  );

  if (status === "recommended") {
    return `${label} is recommended with a ${scoreText}/100 match to the criteria you entered.`;
  }

  return `${label} appears in your results with a ${scoreText}/100 match; some criteria may need review before you proceed.`;
}

export function buildCustomerMatchReasons(
  recommendation: LandParcelRecommendationResponse,
): string[] {
  const reasons: string[] = [];
  const seen = new Set<string>();

  for (const criterion of recommendation.matchingCriteria) {
    const line = customerPlainReasonFromCriterion(criterion);
    if (!line || seen.has(line)) {
      continue;
    }
    seen.add(line);
    reasons.push(line);
    if (reasons.length >= MAX_MATCH_REASONS) {
      return reasons;
    }
  }

  return reasons;
}

const MATERIAL_RESTRICTION_MIN_SEVERITY = 2;

export function verifiedMaterialRestrictions(
  restrictions: RestrictionSummaryResponse[],
): RestrictionSummaryResponse[] {
  return restrictions.filter(
    (item) =>
      item.dataProvenance?.isVerified === true &&
      item.severity >= MATERIAL_RESTRICTION_MIN_SEVERITY,
  );
}

export function customerRestrictionSummary(
  recommendation: LandParcelRecommendationResponse,
): { kind: "verified"; items: RestrictionSummaryResponse[] } | { kind: "not-checked" } {
  const verified = verifiedMaterialRestrictions(recommendation.restrictions);
  if (verified.length > 0) {
    return { kind: "verified", items: verified };
  }
  return { kind: "not-checked" };
}

export function customerLeaseAvailabilityLabel(
  recommendation: LandParcelRecommendationResponse,
): string {
  const label = recommendation.actualLeaseAvailabilityLabel?.trim();
  if (!label) {
    return "Not verified";
  }
  return label;
}

const ML_EVIDENCE_CRITERION_NAMES = new Set([
  "MlSuitabilityPrediction",
  "ExperimentalColomboMlPrediction",
  "ExperimentalColomboMlAbstention",
  "ExperimentalColomboMlUnavailable",
]);

const ML_EVIDENCE_SOURCES = new Set([
  "RandomForestSuitabilityModel",
  "ExperimentalColomboOsmRf",
]);

const HIDDEN_CUSTOMER_EVIDENCE_RELATED = new Set([
  "spatial constraints",
  "project lease-ready (dataset assumption)",
  "hardconstraintejection",
  "gis-derived intelligence",
]);

const RULE_DUPLICATE_EVIDENCE_SOURCES = new Set([
  "RuleBasedCriterionEvaluator",
  "Unknown data",
]);

function isMlRecommendationEvidence(
  item: RecommendationEvidenceResponse,
): boolean {
  if (
    item.relatedCriterionName &&
    ML_EVIDENCE_CRITERION_NAMES.has(item.relatedCriterionName)
  ) {
    return true;
  }
  return ML_EVIDENCE_SOURCES.has(item.source);
}

function stripEvidenceProvenanceSuffix(text: string): string {
  return text.replace(/\s*\(Source:[^)]*\)\s*$/i, "").trim();
}

function stripEvidenceScoringTails(text: string): string {
  let summary = stripEvidenceProvenanceSuffix(text);
  const unroundedIdx = summary.indexOf("(unrounded for scoring)");
  if (unroundedIdx >= 0) {
    summary = summary.slice(0, unroundedIdx).trim().replace(/\.\s*$/, "");
  }
  summary = summary.replace(/\s*Area match score[\s\S]*$/i, "").trim();
  return summary;
}

function visibleCriterionNames(
  recommendation: LandParcelRecommendationResponse,
): Set<string> {
  const all = [
    ...recommendation.matchingCriteria,
    ...recommendation.failedCriteria,
  ];
  return new Set(
    customerVisibleCriteria(all).map((c) => c.name.trim().toLowerCase()),
  );
}

function isRestrictionEvidence(
  item: RecommendationEvidenceResponse,
  recommendation: LandParcelRecommendationResponse,
): boolean {
  const related = item.relatedCriterionName?.trim() ?? "";
  if (!related) {
    return false;
  }
  return recommendation.restrictions.some(
    (r) => r.restrictionType.trim() === related,
  );
}

function isRuleCriterionDuplicate(
  item: RecommendationEvidenceResponse,
  visibleNames: Set<string>,
): boolean {
  const related = item.relatedCriterionName?.trim().toLowerCase() ?? "";
  if (!related || !visibleNames.has(related)) {
    return false;
  }
  return RULE_DUPLICATE_EVIDENCE_SOURCES.has(item.source);
}

export function customerVisibleEvidence(
  recommendation: LandParcelRecommendationResponse,
): RecommendationEvidenceResponse[] {
  const visibleNames = visibleCriterionNames(recommendation);

  return recommendation.evidence.filter((item) => {
    if (isMlRecommendationEvidence(item)) {
      return false;
    }

    const relatedKey = item.relatedCriterionName?.trim().toLowerCase() ?? "";
    if (
      relatedKey &&
      HIDDEN_CUSTOMER_EVIDENCE_RELATED.has(relatedKey) &&
      !(
        relatedKey === "hardconstraintejection" &&
        recommendation.hardConstraintRejected
      )
    ) {
      return false;
    }

    if (item.source === "ProjectLeaseReadyDesignation") {
      return false;
    }

    if (isRestrictionEvidence(item, recommendation)) {
      return false;
    }

    if (item.description.includes("(criterion not assessed)")) {
      return false;
    }

    if (isRuleCriterionDuplicate(item, visibleNames)) {
      return false;
    }

    return true;
  });
}

export function customerEvidenceTitle(
  item: RecommendationEvidenceResponse,
): string {
  const related = item.relatedCriterionName?.trim();
  if (related) {
    return related;
  }

  const provenanceName = item.dataProvenance?.sourceName?.trim();
  if (provenanceName) {
    return provenanceName;
  }

  const source = item.source?.trim() ?? "";
  if (source && !RULE_DUPLICATE_EVIDENCE_SOURCES.has(source)) {
    return source;
  }

  return "Supporting record";
}

export function customerEvidenceDescriptionLines(
  item: RecommendationEvidenceResponse,
): string[] {
  const cleaned = stripEvidenceScoringTails(item.description?.trim() ?? "");
  if (!cleaned) {
    return ["—"];
  }

  let raw = cleaned;
  const targetMatch = raw.match(TARGET_AREA_SUMMARY_PATTERN);
  if (targetMatch) {
    return [
      `Target area ${formatHectaresForDisplay(targetMatch[1])}`,
      `Parcel area ${formatHectaresForDisplay(targetMatch[2])}`,
    ];
  }

  return [cleaned];
}
