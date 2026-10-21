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
  explanationToBullets,
  extractSourceReportedLandUseFromCriteria,
  formatParcelAreaHectares,
  formatCriterionScore,
  formatRuleScore,
  recommendationResultStatus,
  suitabilityBadgeLabel,
} from "../utils/formatters";
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
  const bullets = explanationToBullets(recommendation.explanation);
  const allCriteria = [
    ...recommendation.matchingCriteria,
    ...recommendation.failedCriteria,
  ];
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
          <span className={styles.rankOrder}>Order {displayOrder}</span>
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
                ["why", isTied ? "Why this score" : "Why this result"],
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
              <>
                {recommendation.candidateStatusReason ? (
                  <div className={styles.factorGroup}>
                    <p className={styles.factorLabel}>Candidate screening</p>
                    <p className={styles.lead}>
                      {recommendation.candidateStatusLabel
                        ? `${recommendation.candidateStatusLabel}. `
                        : ""}
                      {recommendation.candidateStatusReason}
                    </p>
                    <ul className={styles.factorList}>
                      {recommendation.sourceProvenanceLabel ? (
                        <li>
                          Provenance: {recommendation.sourceProvenanceLabel}
                        </li>
                      ) : null}
                      {recommendation.sourceParcelStatusCode ? (
                        <li>
                          Source parcel status code (not lease availability):{" "}
                          {recommendation.sourceParcelStatusCode}
                        </li>
                      ) : (
                        <li>
                          Source parcel status: not provided — availability not
                          verified
                        </li>
                      )}
                      {recommendation.sourceImportedAtUtc ? (
                        <li>
                          Source imported:{" "}
                          {new Date(
                            recommendation.sourceImportedAtUtc,
                          ).toLocaleString()}
                        </li>
                      ) : null}
                    </ul>
                  </div>
                ) : null}
                {recommendation.actualLeaseAvailabilityLabel ? (
                  <div className={styles.factorGroup}>
                    <p className={styles.factorLabel}>
                      Official lease availability
                    </p>
                    <p className={styles.lead}>
                      {recommendation.actualLeaseAvailabilityLabel}
                    </p>
                    <p className={styles.sourceNote}>
                      Any project designation is a dataset assumption and does
                      not confirm official lease availability.
                    </p>
                  </div>
                ) : null}
                {recommendation.projectLeaseReadyLabel ? (
                  <div className={styles.factorGroup}>
                    <p className={styles.factorLabel}>Project designation</p>
                    <p className={styles.lead}>
                      {recommendation.projectLeaseReadyLabel}
                    </p>
                  </div>
                ) : null}
                {recommendation.sourceReportedClaimantCategoryDisplay ? (
                  <div className={styles.factorGroup}>
                    <p className={styles.factorLabel}>Source-reported claimant category</p>
                    <p className={styles.lead}>
                      {recommendation.sourceReportedClaimantCategoryDisplay}
                      {recommendation.sourceReportedClaimantCategoryCode
                        ? ` (code ${recommendation.sourceReportedClaimantCategoryCode})`
                        : ""}
                    </p>
                    <p className={styles.sourceNote}>
                      This is preliminary screening only — not a legal ownership determination.
                    </p>
                  </div>
                ) : null}
                <p className={styles.lead}>{bullets.lead}</p>
                {bullets.positives.length > 0 ? (
                  <div className={styles.factorGroup}>
                    <p className={styles.factorLabel}>Positive factors</p>
                    <ul className={styles.factorList}>
                      {bullets.positives.map((item) => (
                        <li key={item}>{item}</li>
                      ))}
                    </ul>
                  </div>
                ) : null}
                {bullets.concerns.length > 0 ? (
                  <div className={styles.factorGroup}>
                    <p className={styles.factorLabel}>Concerns / failed criteria</p>
                    <ul className={styles.factorList}>
                      {bullets.concerns.map((item) => (
                        <li key={item}>{item}</li>
                      ))}
                    </ul>
                  </div>
                ) : null}
                <p className={styles.sourceNote}>
                  Decision-support screening only. Source-reported claimant category and
                  land use do not prove ownership, lease availability, or legal
                  suitability. Official confirmation is required.
                </p>
              </>
            ) : null}

            {tab === "criteria" ? (
              allCriteria.length === 0 ? (
                <p className={styles.empty}>No criteria returned by the API.</p>
              ) : (
                <div style={{ overflowX: "auto" }}>
                  <table className={styles.criteriaTable}>
                    <thead>
                      <tr>
                        <th scope="col">Criterion</th>
                        <th scope="col">Status</th>
                        <th scope="col">Score</th>
                        <th scope="col">Weight</th>
                        <th scope="col">Source</th>
                      </tr>
                    </thead>
                    <tbody>
                      {allCriteria.map((criterion) => {
                        const statusInfo = criterionStatusLabel(criterion);
                        return (
                          <tr key={`${criterion.key}-${criterion.name}`}>
                            <td>
                              <span className={styles.criteriaName}>
                                {criterion.name}
                              </span>
                              <span className={styles.criteriaSummary}>
                                {displayOrUnknown(
                                  criterion.wasAssessed === false
                                    ? criterion.notAssessedReason ||
                                        criterion.summary
                                    : criterion.summary,
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
                            <td>
                              {criterion.wasAssessed === false
                                ? "—"
                                : criterion.weight}
                            </td>
                            <td>
                              {criterion.dataProvenance?.sourceName
                                ? displayOrUnknown(
                                    criterion.dataProvenance.sourceName,
                                  )
                                : "Rule-based criterion"}
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
                        <span className={styles.sourceNote}>
                          Source: {displayOrUnknown(item.source)}
                        </span>
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
              <EvidenceList evidence={recommendation.evidence} />
            ) : null}
          </div>
        </div>
      ) : null}
    </article>
  );
}
