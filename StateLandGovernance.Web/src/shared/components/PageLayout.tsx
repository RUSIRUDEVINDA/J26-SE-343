import type { ReactNode } from "react";
import { AppHeader } from "./AppHeader";
import { Sidebar } from "./Sidebar";
import styles from "./PageLayout.module.css";

export function PageLayout({
  children,
  showSidebar = true,
}: {
  children: ReactNode;
  showSidebar?: boolean;
}) {
  return (
    <div className={styles.shell}>
      <AppHeader />
      <div className={showSidebar ? styles.bodyWithSidebar : styles.body}>
        {showSidebar ? <Sidebar /> : null}
        <main className={styles.main}>{children}</main>
      </div>
    </div>
  );
}

export function PageHeading({
  title,
  subtitle,
  action,
}: {
  title: string;
  subtitle?: string;
  action?: ReactNode;
}) {
  return (
    <div className={styles.headingRow}>
      <div>
        <h1 className={styles.pageTitle}>{title}</h1>
        {subtitle ? <p className={styles.pageSubtitle}>{subtitle}</p> : null}
      </div>
      {action ? <div className={styles.headingAction}>{action}</div> : null}
    </div>
  );
}
