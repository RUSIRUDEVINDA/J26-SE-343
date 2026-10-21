import styles from "./StatePanels.module.css";

export function ErrorMessage({
  title = "Something went wrong",
  message,
  details,
  id,
}: {
  title?: string;
  message: string;
  details?: string[];
  /** Optional id so form controls can reference this message via aria-describedby. */
  id?: string;
}) {
  return (
    <div id={id} className={`${styles.panel} ${styles.error}`} role="alert">
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
