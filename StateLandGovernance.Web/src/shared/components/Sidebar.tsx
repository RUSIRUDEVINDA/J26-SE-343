"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { getResearchDemoConfig } from "@/modules/component-01-land-intelligence/services/researchDemoApi";
import {
  authorityLeaseInterestQueueNavItem,
  component01NavItems,
  LAND_INTELLIGENCE_BASE,
  officerLeaseInterestLookupNavItem,
  officerLeaseInterestQueueNavItem,
  officerLeaseInterestValuationQueueNavItem,
  type NavItem,
} from "@/shared/constants/navigation";
import styles from "./Sidebar.module.css";

/** Temporarily hidden from the sidebar; routes remain available by direct URL. */
const SHOW_OFFICER_VALUATION_QUEUE_IN_SIDEBAR = false;
const SHOW_AUTHORITY_REVIEW_QUEUE_IN_SIDEBAR = false;

function isActive(pathname: string, href: string, basePath: string): boolean {
  if (href === basePath) {
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

export function Sidebar({
  items = component01NavItems,
  label = "Land Intelligence navigation",
  basePath = LAND_INTELLIGENCE_BASE,
  compactMobile = false,
  appendResearchDemoNav = true,
}: {
  items?: NavItem[];
  label?: string;
  basePath?: string;
  compactMobile?: boolean;
  appendResearchDemoNav?: boolean;
}) {
  const pathname = usePathname();
  const [officerLookupEnabled, setOfficerLookupEnabled] = useState(false);
  const [authorityReviewEnabled, setAuthorityReviewEnabled] = useState(false);
  const isLandIntelligenceNav = basePath === LAND_INTELLIGENCE_BASE;

  useEffect(() => {
    if (!isLandIntelligenceNav || !appendResearchDemoNav) {
      return;
    }
    let cancelled = false;
    void getResearchDemoConfig()
      .then((config) => {
        if (!cancelled) {
          setOfficerLookupEnabled(config.officerLeaseInterestNicLookupEnabled);
          setAuthorityReviewEnabled(
            Boolean(config.authorityLeaseInterestReviewEnabled),
          );
        }
      })
      .catch(() => {
        if (!cancelled) {
          setOfficerLookupEnabled(false);
          setAuthorityReviewEnabled(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [isLandIntelligenceNav, appendResearchDemoNav]);

  const navItems = useMemo((): NavItem[] => {
    if (!isLandIntelligenceNav || !appendResearchDemoNav) {
      return items;
    }
    const extras: NavItem[] = [];
    if (officerLookupEnabled) {
      if (!items.some((item) => item.href === officerLeaseInterestQueueNavItem.href)) {
        extras.push(officerLeaseInterestQueueNavItem);
        if (SHOW_OFFICER_VALUATION_QUEUE_IN_SIDEBAR) {
          extras.push(officerLeaseInterestValuationQueueNavItem);
        }
        extras.push(officerLeaseInterestLookupNavItem);
      }
    }
    if (
      SHOW_AUTHORITY_REVIEW_QUEUE_IN_SIDEBAR &&
      authorityReviewEnabled &&
      !items.some((item) => item.href === authorityLeaseInterestQueueNavItem.href)
    ) {
      extras.push(authorityLeaseInterestQueueNavItem);
    }
    return extras.length ? [...items, ...extras] : items;
  }, [
    items,
    isLandIntelligenceNav,
    appendResearchDemoNav,
    officerLookupEnabled,
    authorityReviewEnabled,
  ]);

  return (
    <aside
      className={`${styles.sidebar} ${compactMobile ? styles.compactMobile : ""}`}
      aria-label={label}
    >
      <nav className={styles.nav}>
        {navItems.map((item) => {
          const active = isActive(pathname, item.href, basePath);
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
