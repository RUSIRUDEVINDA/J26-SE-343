import type {
  LandParcelRecommendationResponse,
  RecommendationEvidenceResponse,
} from "../types/landIntelligence";
import { displayOrUnknown } from "../utils/formatters";
import {
  customerEvidenceDescriptionLines,
  customerEvidenceTitle,
  customerVisibleEvidence,
} from "../utils/recommendationCardCustomer";
import styles from "./Lists.module.css";

const ML_CRITERION_NAMES = new Set([
  "MlSuitabilityPrediction",
  "ExperimentalColomboMlPrediction",
  "ExperimentalColomboMlAbstention",
  "ExperimentalColomboMlUnavailable",
]);

const ML_SOURCES = new Set([
  "RandomForestSuitabilityModel",
  "ExperimentalColomboOsmRf",
]);

function isMlEvidence(item: RecommendationEvidenceResponse): boolean {
  if (item.relatedCriterionName && ML_CRITERION_NAMES.has(item.relatedCriterionName)) {
    return true;
  }
  return ML_SOURCES.has(item.source);
}

export function EvidenceList({
  evidence,
  recommendation,
  variant = "technical",
}: {
  evidence: RecommendationEvidenceResponse[];
  recommendation?: LandParcelRecommendationResponse;
  variant?: "technical" | "customer";
}) {
  if (variant === "customer" && recommendation) {
    const items = customerVisibleEvidence(recommendation);
    return (
      <div className={styles.evidenceWrap}>
        {items.length === 0 ? (
          <p className={styles.empty}>
            No additional supporting records beyond the criteria and restrictions
            shown for this parcel.
          </p>
        ) : (
          <ul className={styles.list} aria-label="Supporting evidence">
            {items.map((item, index) => (
              <li
                key={`${item.source}-${item.relatedCriterionName ?? ""}-${index}`}
                className={styles.item}
              >
                <p className={styles.meta}>
                  <strong>{customerEvidenceTitle(item)}</strong>
                </p>
                {customerEvidenceDescriptionLines(item).map((line) => (
                  <p key={line}>{line}</p>
                ))}
              </li>
            ))}
          </ul>
        )}
      </div>
    );
  }

  const standard = evidence.filter((item) => !isMlEvidence(item));
  const mlEvidence = evidence.filter((item) => isMlEvidence(item));

  return (
    <div className={styles.evidenceWrap}>
      <section className={styles.section}>
        <h4 className={styles.sectionTitle}>Rule-based evidence</h4>
        {standard.length === 0 ? (
          <p className={styles.empty}>Unavailable</p>
        ) : (
          <ul className={styles.list}>
            {standard.map((item, index) => (
              <li key={`${item.source}-${index}`} className={styles.item}>
                <p className={styles.meta}>
                  <strong>{displayOrUnknown(item.source)}</strong>
                  {item.relatedCriterionName
                    ? ` · ${item.relatedCriterionName}`
                    : null}
                </p>
                <p>{displayOrUnknown(item.description)}</p>
              </li>
            ))}
          </ul>
        )}
      </section>

      {mlEvidence.length > 0 ? (
        <section className={styles.mlSection} aria-label="Supplementary ML evidence">
          <h4 className={styles.sectionTitle}>Supplementary ML evidence</h4>
          <p className={styles.mlDisclaimer}>
            Machine learning output is supplementary only. It is not a legal
            determination, approval, or measure of real-world accuracy. Model
            confidence values (when present) are not probabilities of legal
            suitability. Absence of ML evidence is shown only when the API
            returns it; this UI does not invent reasons for missing ML output.
          </p>
          <ul className={styles.list}>
            {mlEvidence.map((item, index) => (
              <li key={`ml-${index}`} className={styles.item}>
                <p className={styles.meta}>
                  <strong>{displayOrUnknown(item.source)}</strong>
                  {item.relatedCriterionName
                    ? ` · ${item.relatedCriterionName}`
                    : null}
                </p>
                <p>{displayOrUnknown(item.description)}</p>
              </li>
            ))}
          </ul>
        </section>
      ) : null}
    </div>
  );
}
