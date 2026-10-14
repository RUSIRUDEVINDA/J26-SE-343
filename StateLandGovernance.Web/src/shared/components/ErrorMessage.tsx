import styles from "./StatePanels.module.css";

export function ErrorMessage({
  title = "Something went wrong",
  message,
  details,
}: {
  title?: string;
  message: string;
  details?: string[];
}) {
  return (
    <div className={`${styles.panel} ${styles.error}`} role="alert">
      <h3 className={styles.title}>{title}</h3>
      <p className={styles.message}>{message}</p>
      {details && details.length > 0 ? (
        <ul className={styles.list}>
          {details.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
