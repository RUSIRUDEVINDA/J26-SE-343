"use client";

import { useId, useState } from "react";
import { ChevronDown, MapPin } from "lucide-react";
import { Button } from "@/shared/components/Button";
import { StatusBadge } from "@/shared/components/StatusBadge";
import type {
  CriterionEvaluationResponse,
  LandParcelRecommendationResponse,
  RestrictionSeverity,
  ResearchSupplementParcelResponse,
} from "../types/landIntelligence";
import { restrictionSeverityLabels } from "../utils/enumMappings";
import {
  displayOrUnknown,
  extractSourceReportedLandUseFromCriteria,
  formatParcelAreaHectares,
  formatCriterionScore,
  formatRuleScore,
  recommendationResultStatus,
  suitabilityBadgeLabel,
} from "../utils/formatters";
import {
  buildCustomerMatchReasons,
  customerCriterionSummaryLines,
  customerRecommendationIntro,
  customerVisibleCriteria,
} from "../utils/recommendationCardCustomer";
import { EvidenceList } from "./EvidenceList";
import styles from "./RecommendationCard.module.css";

type DetailTab = "why" | "criteria" | "restrictions" | "evidence";

function statusTone(
  status: ReturnType<typeof recommendationResultStatus>,
): "success" | "warning" | "danger" {
  if (status === "hard-rejected") {
    return "danger";
  }
  if (status === "recommended") {
    return "success";
  }
  return "warning";
}

function scoreFillClass(
  status: ReturnType<typeof recommendationResultStatus>,
): string {
  if (status === "hard-rejected") {
    return styles.scoreFillRejected;
  }
  if (status === "recommended") {
    return styles.scoreFillRecommended;
  }
  return styles.scoreFillNeedsReview;
}

function criterionStatusLabel(criterion: CriterionEvaluationResponse): {
  label: string;
  tone: "success" | "warning" | "danger" | "neutral";
} {
  if (criterion.wasAssessed === false) {
    return { label: "Not assessed", tone: "neutral" };
  }
  if (criterion.isMet) {
    return { label: "Met", tone: "success" };
  }
  return { label: "Not met", tone: "danger" };
}

function restrictionSeverityClass(severity: RestrictionSeverity): string {
  switch (severity) {
    case 1:
      return styles.restrictionItemLow;
    case 2:
      return styles.restrictionItemMedium;
    case 3:
      return styles.restrictionItemHigh;
    case 4:
      return styles.restrictionItemProhibitive;
    default:
      return "";
  }
}

export function RecommendationCard({
  recommendation,
  searchDistrict,
  researchCover,
}: {
  recommendation: LandParcelRecommendationResponse;
  searchDistrict?: string | null;
  researchCover?: ResearchSupplementParcelResponse;
}) {
  const [expanded, setExpanded] = useState(false);
  const [tab, setTab] = useState<DetailTab>("why");
  const panelId = useId();
  const status = recommendationResultStatus(
    recommendation.suitabilityScore,
    recommendation.hardConstraintRejected,
  );
  const badge = suitabilityBadgeLabel(
    recommendation.suitabilityScore,
    recommendation.hardConstraintRejected,
  );
  const scorePct = Math.max(
    0,
    Math.min(100, recommendation.suitabilityScore),
  );
  const sourceLandUse = extractSourceReportedLandUseFromCriteria([
    ...recommendation.matchingCriteria.map((c) => c.summary),
    ...recommendation.failedCriteria.map((c) => c.summary),
  ]);
  const recommendationIntro = customerRecommendationIntro(recommendation);
  const matchReasons = buildCustomerMatchReasons(recommendation);
  const visibleCriteria = customerVisibleCriteria([
    ...recommendation.matchingCriteria,
    ...recommendation.failedCriteria,
  ]);
  const hasRestrictions = recommendation.restrictions.length > 0;
  const displayOrder = recommendation.displayOrder ?? recommendation.rank;
  const ruleScoreRank = recommendation.ruleScoreRank ?? recommendation.rank;
  const isTied = recommendation.isTiedOnRuleScore === true;
  const tieCount = recommendation.ruleScoreTieCount ?? 0;
  const orderingLabel = isTied
    ? `Tied · rule score rank ${ruleScoreRank} (${tieCount} parcel${tieCount === 1 ? "" : "s"})`
    : `Rule score rank ${ruleScoreRank}`;
  const orderingAria = `${orderingLabel}. Display order ${displayOrder} (suitability score ranking; cadastral number breaks ties).`;

  return (
    <article
      className={`${styles.card} ${
        recommendation.hardConstraintRejected ? styles.cardRejected : ""
      }`}
    >
      <button
        type="button"
        className={styles.summary}
        aria-expanded={expanded}
        aria-controls={panelId}
        onClick={() => setExpanded((open) => !open)}
      >
        <span className={styles.rank} aria-label={orderingAria}>
          <span className={styles.rankOrder}>Parcel {displayOrder}</span>
          {isTied ? (
            <span className={styles.rankTie}>Tied</span>
          ) : null}
        </span>

        <div className={styles.mainMeta}>
          <div className={styles.titleRow}>
            <h3 className={styles.title}>
              {displayOrUnknown(recommendation.cadastralNumber)}
            </h3>
            <StatusBadge tone={statusTone(status)}>{badge}</StatusBadge>
          </div>
          <p className={styles.metaLine}>
            {searchDistrict
              ? `Search district: ${searchDistrict}`
              : "District: all"}
            {" · "}
            Area: {formatParcelAreaHectares(recommendation.parcelAreaHectares)}
            {" · "}
            {orderingLabel}
          </p>
          {sourceLandUse ? (
            <p className={styles.metaLine}>
              Land use: {sourceLandUse}
            </p>
          ) : null}
          {researchCover ? (
            <p className={styles.metaLine}>
              Project land cover: {researchCover.supplementGroup}
            </p>
          ) : null}
        </div>

        <div
          className={styles.scoreBlock}
          aria-label={`Match across assessed search criteria ${formatRuleScore(recommendation.suitabilityScore)} out of 100. Not legal suitability or lease approval.`}
        >
          <span className={styles.scoreValue}>
            {formatRuleScore(recommendation.suitabilityScore)}
          </span>
          <span className={styles.scoreHint}>/ 100 match</span>
          <span className={styles.scoreCaption}>
            Assessed search criteria only
          </span>
          <div className={styles.scoreTrack} aria-hidden>
            <div
              className={`${styles.scoreFill} ${scoreFillClass(status)}`}
              style={{ width: `${scorePct}%` }}
            />
          </div>
        </div>

        <ChevronDown
          className={`${styles.chevron} ${expanded ? styles.chevronOpen : ""}`}
          size={18}
          aria-hidden
        />
      </button>

      <div className={styles.actionsRow}>
        <Button
          href={`/land-intelligence/parcels/${recommendation.parcelId}/map`}
          variant="secondary"
          className={styles.mapButton}
        >
          <MapPin size={16} aria-hidden />
          View on map
        </Button>
        <Button
          href={`/land-intelligence/parcels/${recommendation.parcelId}`}
          variant="primary"
        >
          Parcel intelligence
        </Button>
        <Button
          href={`/land-intelligence/parcels/${recommendation.parcelId}/lease-interest`}
          variant="secondary"
        >
          Express preliminary interest
        </Button>
      </div>

      {expanded ? (
        <div id={panelId} className={styles.details}>
          {recommendation.hardConstraintRejected ? (
            <p className={styles.hardBanner} role="status">
              Hard constraint rejected:{" "}
              {displayOrUnknown(recommendation.hardConstraintReason)}
            </p>
          ) : null}

          <div className={styles.tabs} role="tablist" aria-label="Result details">
            {(
              [
                ["why", "Why recommended"],
                ["criteria", "Criteria"],
                ["restrictions", `Restrictions${hasRestrictions ? ` (${recommendation.restrictions.length})` : ""}`],
                ["evidence", "Evidence"],
              ] as const
            ).map(([id, label]) => (
              <button
                key={id}
                type="button"
                role="tab"
                aria-selected={tab === id}
                className={`${styles.tab} ${tab === id ? styles.tabActive : ""}`}
                onClick={() => setTab(id)}
              >
                {label}
              </button>
            ))}
          </div>

          <div className={styles.panel} role="tabpanel">
            {tab === "why" ? (
              <section aria-labelledby={`${panelId}-why-recommended`}>
                <h4 id={`${panelId}-why-recommended`} className={styles.whyHeading}>
                  Why this parcel was recommended
                </h4>
                <p className={styles.lead}>{recommendationIntro}</p>
                {matchReasons.length > 0 ? (
                  <ul className={styles.factorList}>
                    {matchReasons.map((item) => (
                      <li key={item}>{item}</li>
                    ))}
                  </ul>
                ) : (
                  <p className={styles.empty}>
                    No short reasons were available from the criteria assessed in
                    this search.
                  </p>
                )}

              </section>
            ) : null}

            {tab === "criteria" ? (
              visibleCriteria.length === 0 ? (
                <p className={styles.empty}>No criteria returned by the API.</p>
              ) : (
                <div style={{ overflowX: "auto" }}>
                  <table className={styles.criteriaTable}>
                    <thead>
                      <tr>
                        <th scope="col">Criterion</th>
                        <th scope="col">Status</th>
                        <th scope="col">Score</th>
                      </tr>
                    </thead>
                    <tbody>
                      {visibleCriteria.map((criterion) => {
                        const statusInfo = criterionStatusLabel(criterion);
                        return (
                          <tr key={`${criterion.key}-${criterion.name}`}>
                            <td>
                              <span className={styles.criteriaName}>
                                {criterion.name}
                              </span>
                              <span className={styles.criteriaSummary}>
                                {customerCriterionSummaryLines(criterion).map(
                                  (line) => (
                                    <span
                                      key={line}
                                      className={styles.criteriaSummaryLine}
                                    >
                                      {line}
                                    </span>
                                  ),
                                )}
                              </span>
                            </td>
                            <td>
                              <StatusBadge tone={statusInfo.tone}>
                                {statusInfo.label}
                              </StatusBadge>
                            </td>
                            <td>
                              {criterion.wasAssessed === false
                                ? "—"
                                : formatCriterionScore(criterion.score)}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )
            ) : null}

            {tab === "restrictions" ? (
              !hasRestrictions ? (
                <p className={styles.empty}>
                  No restrictions reported for this parcel in the API response.
                </p>
              ) : (
                <ul className={styles.restrictionList}>
                  {recommendation.restrictions.map((item, index) => (
                    <li
                      key={`${item.restrictionType}-${index}`}
                      className={`${styles.restrictionItem} ${restrictionSeverityClass(item.severity)}`}
                    >
                      <div className={styles.restrictionHeader}>
                        <strong>
                          {displayOrUnknown(item.restrictionType)}
                        </strong>
                        <StatusBadge
                          tone={
                            item.severity >= 4
                              ? "danger"
                              : item.severity >= 2
                                ? "warning"
                                : "neutral"
                          }
                        >
                          {restrictionSeverityLabels[item.severity] ??
                            "Unknown"}
                        </StatusBadge>
                      </div>
                      <p className={styles.restrictionDesc}>
                        {displayOrUnknown(item.description)}
                      </p>
                    </li>
                  ))}
                </ul>
              )
            ) : null}

            {tab === "evidence" ? (
              <EvidenceList
                evidence={recommendation.evidence}
                recommendation={recommendation}
                variant="customer"
              />
            ) : null}
          </div>
        </div>
      ) : null}
    </article>
  );
}
