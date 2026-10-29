import type { LeaseInterestValuation } from "../types/landIntelligence";
import { AuthorityValuationWorksheet } from "./AuthorityValuationWorksheet";
import styles from "./HistoricalValuationRecords.module.css";

type HistoricalValuationRecordsProps = {
  valuations: LeaseInterestValuation[];
  formatRecordedAt: (iso: string) => string;
};

/** Older officer valuations (not the current / latest worksheet). */
export function HistoricalValuationRecords({
  valuations,
  formatRecordedAt,
}: HistoricalValuationRecordsProps) {
  if (valuations.length === 0) {
    return null;
  }

  return (
    <details className={styles.historicalRecords}>
      <summary>Historical records ({valuations.length})</summary>
      <ol className={styles.historicalList}>
        {valuations.map((item) => (
          <li key={item.id} className={styles.historicalItem}>
            <p className={styles.historicalMeta}>
              Revision {item.revision} · recorded {formatRecordedAt(item.recordedAtUtc)}
              {" · "}
              LKR {item.assessedLandValueLkr.toLocaleString("en-LK")}
            </p>
            <AuthorityValuationWorksheet
              valuation={item}
              headerMode="historical"
            />
          </li>
        ))}
      </ol>
    </details>
  );
}
