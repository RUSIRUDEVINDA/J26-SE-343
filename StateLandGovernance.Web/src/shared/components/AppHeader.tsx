import {DevelopmentSessionBar} from "./DevelopmentSessionBar";
import Image from "next/image";
import {SignInLink} from "./SignInLink";
import {
  SYSTEM_TITLE,
  SYSTEM_TITLE_SINHALA,
  SYSTEM_TITLE_TAMIL,
} from "@/shared/constants/navigation";
import styles from "./AppHeader.module.css";

export function AppHeader({unifiedAccount=false}:{unifiedAccount?:boolean}) {
  return (
    <header className={styles.header}>
      <div className={styles.brand}>
        <Image
          src="/images/sri-lanka-emblem.png"
          alt="Coat of arms of Sri Lanka"
          width={48}
          height={48}
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
      {unifiedAccount?<DevelopmentSessionBar compact/>:<SignInLink className={styles.signIn}/>}
    </header>
  );
}
