import styles from "./StatePanels.module.css";

export function LoadingState({ message = "Loading…" }: { message?: string }) {
  return (
    <div className={styles.panel} role="status" aria-live="polite">
      <p className={styles.message}>{message}</p>
    </div>
  );
}
