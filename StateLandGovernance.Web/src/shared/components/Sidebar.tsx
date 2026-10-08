"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  component01NavItems,
  LAND_INTELLIGENCE_BASE,
} from "@/shared/constants/navigation";
import styles from "./Sidebar.module.css";

function isActive(pathname: string, href: string): boolean {
  if (href === LAND_INTELLIGENCE_BASE) {
    return pathname === href;
  }
  if (href.endsWith("/parcels")) {
    return (
      pathname === href ||
      pathname.startsWith(`${href}/`) ||
      pathname.startsWith(`${LAND_INTELLIGENCE_BASE}/parcel-intelligence`)
    );
  }
  return pathname === href || pathname.startsWith(`${href}/`);
}

export function Sidebar() {
  const pathname = usePathname();

  return (
    <aside className={styles.sidebar} aria-label="Land Intelligence navigation">
      <nav className={styles.nav}>
        {component01NavItems.map((item) => {
          const active = isActive(pathname, item.href);
          return (
            <Link
              key={item.label}
              href={item.href}
              className={`${styles.link} ${active ? styles.active : styles.inactive}`}
              aria-current={active ? "page" : undefined}
            >
              <span aria-hidden="true">{item.icon} </span>
              {item.label}
            </Link>
          );
        })}
      </nav>
    </aside>
  );
}
