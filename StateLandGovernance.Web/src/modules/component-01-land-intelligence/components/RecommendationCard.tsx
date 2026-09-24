import { MapPin } from "lucide-react";
import { Button } from "@/shared/components/Button";
import { StatusBadge } from "@/shared/components/StatusBadge";
import type { LandParcelRecommendationResponse } from "../types/landIntelligence";
import { restrictionSeverityLabels } from "../utils/enumMappings";
import {
  displayOrUnknown,
  formatRuleScore,
  suitabilityBadgeLabel,
} from "../utils/formatters";
import { CriteriaList } from "./CriteriaList";
import { EvidenceList } from "./EvidenceList";
import styles from "./RecommendationCard.module.css";
import listStyles from "./Lists.module.css";

export function RecommendationCard({
  recommendation,
}: {
  recommendation: LandParcelRecommendationResponse;
}) {
  const badge = suitabilityBadgeLabel(
    recommendation.suitabilityScore,
    recommendation.hardConstraintRejected,
  );

  return (
    <article className={styles.card}>
      <div className={styles.header}>
        <div className={styles.rankBlock}>
          <span className={styles.rank}>#{recommendation.rank}</span>
          <div>
            <h3 className={styles.title}>
              {displayOrUnknown(recommendation.cadastralNumber)}
            </h3>
            <p className={styles.subtitle}>
              Parcel ID: {recommendation.parcelId}
            </p>
            <StatusBadge
              tone={recommendation.hardConstraintRejected ? "danger" : "primary"}
            >
              {badge}
            </StatusBadge>
          </div>
        </div>
        <div
          className={styles.score}
          aria-label="Rule-based suitability score out of 100"
        >
          <span className={styles.scoreValue}>
            {formatRuleScore(recommendation.suitabilityScore)}
          </span>
          <span className={styles.scoreHint}>/ 100 rule score</span>
        </div>
      </div>

      {recommendation.hardConstraintRejected ? (
        <StatusBadge tone="danger">
          Hard constraint rejected:{" "}
          {displayOrUnknown(recommendation.hardConstraintReason)}
        </StatusBadge>
      ) : null}

      <p className={styles.explanation}>
        {displayOrUnknown(recommendation.explanation)}
      </p>

      <CriteriaList
        title="Matching criteria"
        criteria={recommendation.matchingCriteria}
      />
      <CriteriaList
        title="Failed criteria"
        criteria={recommendation.failedCriteria}
      />

      <section className={listStyles.section}>
        <h4 className={listStyles.sectionTitle}>Restrictions</h4>
        {recommendation.restrictions.length === 0 ? (
          <p className={listStyles.empty}>None reported by the API</p>
        ) : (
          <ul className={listStyles.list}>
            {recommendation.restrictions.map((item, index) => (
              <li
                key={`${item.restrictionType}-${index}`}
                className={listStyles.item}
              >
                <p className={listStyles.meta}>
                  <strong>{displayOrUnknown(item.restrictionType)}</strong> ·{" "}
                  {restrictionSeverityLabels[item.severity] ?? "Unknown"} ·{" "}
                  {displayOrUnknown(item.source)}
                </p>
                <p>{displayOrUnknown(item.description)}</p>
              </li>
            ))}
          </ul>
        )}
      </section>

      <EvidenceList evidence={recommendation.evidence} />

      <div className={styles.footer}>
        <Button
          href={`/land-intelligence/parcels/${recommendation.parcelId}`}
          variant="primary"
        >
          View Full Parcel Intelligence
        </Button>
        <Button
          href={`/land-intelligence/parcels/${recommendation.parcelId}/map`}
          variant="secondary"
          className={styles.mapButton}
        >
          <MapPin size={16} aria-hidden />
          View on map
        </Button>
      </div>
    </article>
  );
}
