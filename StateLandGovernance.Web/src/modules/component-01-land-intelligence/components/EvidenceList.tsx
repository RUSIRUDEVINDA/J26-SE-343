import type { RecommendationEvidenceResponse } from "../types/landIntelligence";
import { displayOrUnknown } from "../utils/formatters";
import styles from "./Lists.module.css";

const ML_CRITERION = "MlSuitabilityPrediction";

export function EvidenceList({
  evidence,
}: {
  evidence: RecommendationEvidenceResponse[];
}) {
  const standard = evidence.filter(
    (item) => item.relatedCriterionName !== ML_CRITERION,
  );
  const mlEvidence = evidence.filter(
    (item) => item.relatedCriterionName === ML_CRITERION,
  );

  return (
    <div className={styles.evidenceWrap}>
      <section className={styles.section}>
        <h4 className={styles.sectionTitle}>Evidence</h4>
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
        <section className={styles.mlSection}>
          <h4 className={styles.sectionTitle}>Supporting ML assessment</h4>
          <p className={styles.mlDisclaimer}>
            Machine learning output supports exploratory ranking only. It is not
            a legal determination, approval, or final suitability decision.
          </p>
          <ul className={styles.list}>
            {mlEvidence.map((item, index) => (
              <li key={`ml-${index}`} className={styles.item}>
                <p className={styles.meta}>
                  <strong>{displayOrUnknown(item.source)}</strong>
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
