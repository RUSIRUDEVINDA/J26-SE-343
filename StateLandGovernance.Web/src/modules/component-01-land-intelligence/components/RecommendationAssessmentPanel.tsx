import { Button } from "@/shared/components/Button";
import styles from "./RecommendationAssessmentPanel.module.css";

export function RecommendationAssessmentPanel() {
  return (
    <aside className={styles.panel} aria-labelledby="assessment-panel-title">
      <h2 id="assessment-panel-title" className={styles.title}>
        Scoring &amp; status
      </h2>

      <section className={styles.section}>
        <h3 className={styles.sectionTitle}>Match score</h3>
        <p className={styles.copy}>
          Weighted 0–100 summary of criteria that were actually assessed for
          each parcel. Unassessed items do not contribute weight and cannot
          inflate the score.
        </p>
      </section>

      <section className={styles.section}>
        <h3 className={styles.sectionTitle}>Status legend</h3>
        <ul className={styles.legend}>
          <li>
            <span className={`${styles.swatch} ${styles.recommended}`} aria-hidden />
            <div>
              <strong>Recommended</strong>
              <p>Higher match across assessed criteria; review evidence before any decision.</p>
            </div>
          </li>
          <li>
            <span className={`${styles.swatch} ${styles.needsReview}`} aria-hidden />
            <div>
              <strong>Needs review</strong>
              <p>Mid/lower match or unmet criteria; inspect failed and not-assessed items.</p>
            </div>
          </li>
          <li>
            <span className={`${styles.swatch} ${styles.hardRejected}`} aria-hidden />
            <div>
              <strong>Hard constraint rejected</strong>
              <p>Blocked by a configured hard restriction; score set to zero.</p>
            </div>
          </li>
        </ul>
      </section>

      <div className={styles.actions}>
        <Button href="/land-intelligence/find-suitable-land" variant="secondary">
          Edit search
        </Button>
      </div>
    </aside>
  );
}
