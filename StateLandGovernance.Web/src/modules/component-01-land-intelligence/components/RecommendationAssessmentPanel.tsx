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
  return (
    <aside className={styles.panel} aria-labelledby="assessment-panel-title">
      <h2 id="assessment-panel-title" className={styles.title}>
        How results are assessed
      </h2>

      <section className={styles.section}>
        <h3 className={styles.sectionTitle}>How to read these results</h3>
        <ul className={styles.bullets}>
          <li>
            Rank and rule score come from configured criteria only — not legal
            approval or ownership.
          </li>
          <li>
            Expand a card for why it ranked, criteria, restrictions, and
            provenance.
          </li>
          <li>
            Missing GIS evidence stays Unavailable and is never treated as a
            positive match.
          </li>
        </ul>
      </section>

      <section className={styles.section}>
        <h3 className={styles.sectionTitle}>Rule score</h3>
        <p className={styles.copy}>
          The rule score is a weighted 0–100 summary of assessed criteria. It is
          not a probability of lease suitability or a legal determination.
        </p>
      </section>

      <section className={styles.section}>
        <h3 className={styles.sectionTitle}>Status legend</h3>
        <ul className={styles.legend}>
          <li>
            <span className={`${styles.swatch} ${styles.recommended}`} aria-hidden />
            <div>
              <strong>Recommended</strong>
              <p>Higher rule-based score; review evidence before any decision.</p>
            </div>
          </li>
          <li>
            <span className={`${styles.swatch} ${styles.needsReview}`} aria-hidden />
            <div>
              <strong>Needs review</strong>
              <p>Mid/lower score or unmet criteria; inspect failed items.</p>
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
          legal availability determination. Mapped road/water distances and
          conservation intersections apply only where coverage exists.
        </p>
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
