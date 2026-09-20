import Image from "next/image";
import { ChevronDown, User } from "lucide-react";
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
          alt="Sri Lanka emblem"
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
      <div className={styles.actions}>
        <button type="button" className={styles.language} aria-label="Language">
          <span>English</span>
          <ChevronDown size={14} strokeWidth={2.25} aria-hidden />
        </button>
        <button
          type="button"
          className={styles.profile}
          aria-label="Profile"
        >
          <User size={17} strokeWidth={2.25} aria-hidden />
        </button>
      </div>
    </header>
  );
}
