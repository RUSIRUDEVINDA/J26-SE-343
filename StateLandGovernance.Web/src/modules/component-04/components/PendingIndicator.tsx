import type {ReactNode} from "react";
import styles from "./KiwulaAssessmentWorkspace.module.css";
export function PendingIndicator({children}:{children:ReactNode}) {
 return <p role="status" aria-live="polite" className={styles.pending}><span className={styles.spinner} aria-hidden="true"/>{children}</p>;
}
