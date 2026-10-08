import { StatusBadge } from "@/shared/components/StatusBadge";
import type { CriterionEvaluationResponse } from "../types/landIntelligence";
import { criterionCategoryLabels } from "../utils/enumMappings";
import { displayOrUnknown } from "../utils/formatters";
import styles from "./Lists.module.css";

export function CriteriaList({
  title,
  criteria,
  emptyLabel = "None",
}: {
  title: string;
  criteria: CriterionEvaluationResponse[];
  emptyLabel?: string;
}) {
  if (criteria.length === 0) {
    return (
      <section className={styles.section}>
        <h4 className={styles.sectionTitle}>{title}</h4>
        <p className={styles.empty}>{emptyLabel}</p>
      </section>
    );
  }

  return (
    <section className={styles.section}>
      <h4 className={styles.sectionTitle}>{title}</h4>
      <ul className={styles.list}>
        {criteria.map((item) => (
          <li key={`${item.key}-${item.name}`} className={styles.item}>
            <div className={styles.itemHeader}>
              <strong>{item.name}</strong>
              <StatusBadge tone={item.isMet ? "success" : "danger"}>
                {item.isMet ? "Met" : "Not met"}
              </StatusBadge>
            </div>
            <p className={styles.meta}>
              {criterionCategoryLabels[item.category] ?? "Unknown category"} ·
              Score {item.score} · Weight {item.weight}
            </p>
            <p>{displayOrUnknown(item.summary)}</p>
          </li>
        ))}
      </ul>
    </section>
  );
}
