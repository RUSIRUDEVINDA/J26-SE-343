"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { getResearchDemoConfig } from "@/modules/component-01-land-intelligence/services/researchDemoApi";
import {
  component01NavItems,
  LAND_INTELLIGENCE_BASE,
  officerLeaseInterestLookupNavItem,
  officerLeaseInterestQueueNavItem,
  type NavItem,
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
  const [officerLookupEnabled, setOfficerLookupEnabled] = useState(false);

  useEffect(() => {
    let cancelled = false;
    void getResearchDemoConfig()
      .then((config) => {
        if (!cancelled) {
          setOfficerLookupEnabled(config.officerLeaseInterestNicLookupEnabled);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setOfficerLookupEnabled(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const navItems = useMemo((): NavItem[] => {
    if (!officerLookupEnabled) {
      return component01NavItems;
    }
    return [
      ...component01NavItems,
      officerLeaseInterestQueueNavItem,
      officerLeaseInterestLookupNavItem,
    ];
  }, [officerLookupEnabled]);

  return (
    <aside className={styles.sidebar} aria-label="Land Intelligence navigation">
      <nav className={styles.nav}>
        {navItems.map((item) => {
          const active = isActive(pathname, item.href);
          const className = [
            styles.link,
            active ? styles.active : styles.inactive,
            item.primary && !active ? styles.primaryAction : "",
          ]
            .filter(Boolean)
            .join(" ");

          return (
            <Link
              key={item.label}
              href={item.href}
              className={className}
              aria-current={active ? "page" : undefined}
            >
              <span className={styles.icon} aria-hidden="true">
                {item.icon}
              </span>
              <span className={styles.label}>{item.label}</span>
            </Link>
          );
        })}
      </nav>
    </aside>
  );
}
