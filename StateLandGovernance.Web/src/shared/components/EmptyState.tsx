import type { ReactNode } from "react";
import styles from "./StatePanels.module.css";

export function EmptyState({
  title,
  description,
  action,
}: {
  title: string;
  description?: string;
  action?: ReactNode;
}) {
  return (
    <div className={styles.panel}>
      <h3 className={styles.title}>{title}</h3>
      {description ? <p className={styles.message}>{description}</p> : null}
      {action ? <div className={styles.action}>{action}</div> : null}
    </div>
  );
}
