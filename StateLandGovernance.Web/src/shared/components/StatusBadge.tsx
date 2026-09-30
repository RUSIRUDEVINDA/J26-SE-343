import type { ReactNode } from "react";
import styles from "./StatusBadge.module.css";

type Tone = "neutral" | "success" | "warning" | "danger" | "primary";

export function StatusBadge({
  children,
  tone = "neutral",
}: {
  children: ReactNode;
  tone?: Tone;
}) {
  return (
    <span className={`${styles.badge} ${styles[tone]}`}>{children}</span>
  );
}
