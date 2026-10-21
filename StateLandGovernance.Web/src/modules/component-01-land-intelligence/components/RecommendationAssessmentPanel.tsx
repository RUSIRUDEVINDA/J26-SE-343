import { Button } from "@/shared/components/Button";
import type { RecommendationSearchTransparencyResponse } from "../types/landIntelligence";
import styles from "./RecommendationAssessmentPanel.module.css";

export function RecommendationAssessmentPanel({
  candidateCount,
  resultCount,
  transparency,
}: {
  candidateCount: number;
  resultCount: number;
  transparency?: RecommendationSearchTransparencyResponse | null;
}) {
  const unavailable = transparency?.unavailableCriteria ?? [];

  return (
    <aside className={styles.panel} aria-labelledby="assessment-panel-title">
      <h2 id="assessment-panel-title" className={styles.title}>
        How results are assessed
      </h2>

      <section className={styles.section}>
        <h3 className={styles.sectionTitle}>How to read these results</h3>
        <ul className={styles.bullets}>
          <li>
            The total is a <strong>match across assessed search criteria</strong>{" "}
            — not legal suitability, ownership verification, or lease approval.
          </li>
          <li>
            A displayed <strong>100</strong> means every assessed criterion
            matched fully. Near area matches (for example 0.045 ha vs 0.05 ha
            target → area 90) keep the total below 100.
          </li>
          <li>
            Road, water, and soil preferences are not part of search or rule
            scoring.
          </li>
          <li>
            Missing environmental / GIS evidence is{" "}
            <strong>not assessed</strong> and is never treated as a positive
            match.
          </li>
        </ul>
      </section>

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

      <section className={styles.section}>
        <h3 className={styles.sectionTitle}>GIS data limitations</h3>
        <p className={styles.copy}>
          Source-reported land use is Survey Department/iLAS metadata — not a
          legal availability determination. Mapped conservation intersections
          apply only where enrichment coverage exists. Road/water/soil layers
          may appear on parcel details but do not score the ranking.
        </p>
        {unavailable.length > 0 ? (
          <ul className={styles.bullets} aria-label="Not assessed criteria">
            {unavailable.map((item) => (
              <li key={item.key}>
                <strong>{item.key}</strong>: {item.reason} (not assessed)
              </li>
            ))}
          </ul>
        ) : null}
      </section>

      <section className={styles.section}>
        <h3 className={styles.sectionTitle}>Candidate summary</h3>
        <p className={styles.copy}>
          Showing {resultCount} result{resultCount === 1 ? "" : "s"} from{" "}
          {candidateCount} candidate{candidateCount === 1 ? "" : "s"} after
          filters.
        </p>
        {transparency?.filterStages && transparency.filterStages.length > 0 ? (
          <ol className={styles.stages}>
            {transparency.filterStages.map((stage) => (
              <li key={`${stage.name}-${stage.candidateCount}`}>
                <span className={styles.stageCount}>{stage.candidateCount}</span>
                <span>{stage.name}</span>
              </li>
            ))}
          </ol>
        ) : null}
      </section>

      <div className={styles.actions}>
        <Button href="/land-intelligence/find-suitable-land" variant="secondary">
          Edit search
        </Button>
      </div>
    </aside>
  );
}
