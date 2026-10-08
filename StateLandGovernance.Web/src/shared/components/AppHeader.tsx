import Image from "next/image";
import {
  SYSTEM_TITLE,
  SYSTEM_TITLE_SINHALA,
  SYSTEM_TITLE_TAMIL,
} from "@/shared/constants/navigation";
import styles from "./AppHeader.module.css";

export function AppHeader() {
  return (
    <header className={styles.header}>
      <div className={styles.brand}>
        <Image
          src="/images/sri-lanka-emblem.png"
          alt="Coat of arms of Sri Lanka"
          width={58}
          height={58}
          className={styles.emblem}
          priority
        />
        <div className={styles.titles}>
          <p className={styles.sinhala} lang="si">
            {SYSTEM_TITLE_SINHALA}
          </p>
          <p className={styles.tamil} lang="ta">
            {SYSTEM_TITLE_TAMIL}
          </p>
          <p className={styles.english} lang="en">
            {SYSTEM_TITLE}
          </p>
        </div>
      </div>
    </header>
  );
}
