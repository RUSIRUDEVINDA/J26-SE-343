"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  component01NavItems,
  LAND_INTELLIGENCE_BASE,
} from "@/shared/constants/navigation";
import styles from "./Sidebar.module.css";

function isActive(pathname: string, href?: string): boolean {
  if (!href) {
    return false;
  }
  if (href === LAND_INTELLIGENCE_BASE) {
    return pathname === href;
  }
  return pathname === href || pathname.startsWith(`${href}/`);
}

export function Sidebar() {
  const pathname = usePathname();

  return (
    <aside className={styles.sidebar} aria-label="Component 1 navigation">
      <nav className={styles.nav}>
        {component01NavItems.map((item) => {
          const active = isActive(pathname, item.href);
          if (item.disabled || !item.href) {
            return (
              <span
                key={item.label}
                className={`${styles.link} ${styles.disabled}`}
                aria-disabled="true"
                title={item.disabledReason}
              >
                {item.icon ? `${item.icon}  ` : null}
                {item.label}
              </span>
            );
          }
          return (
            <Link
              key={item.label}
              href={item.href}
              className={`${styles.link} ${active ? styles.active : styles.inactive}`}
              aria-current={active ? "page" : undefined}
            >
              {item.icon ? `${item.icon}  ` : null}
              {item.label}
            </Link>
          );
        })}
      </nav>
    </aside>
  );
}
