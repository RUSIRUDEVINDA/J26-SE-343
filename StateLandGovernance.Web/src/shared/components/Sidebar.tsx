"use client";

import { useEffect, useState } from "react";
import { readDevelopmentSession, type DevelopmentUser } from "@/lib/developmentSession";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  component01NavItems,
  LAND_INTELLIGENCE_BASE,
  type NavItem,
} from "@/shared/constants/navigation";
import styles from "./Sidebar.module.css";

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
}: {
  items?: NavItem[];
  label?: string;
  basePath?: string;
  compactMobile?: boolean;
}) {
  const pathname = usePathname();
  const [user,setUser]=useState<DevelopmentUser|null>(null);
  useEffect(()=>{const update=()=>setUser(readDevelopmentSession()?.user||null);update();window.addEventListener("development-session-changed",update);return()=>window.removeEventListener("development-session-changed",update);},[]);
  const visibleItems=items.filter(item=>{
    if(!user||!item.href.startsWith("/workflow-governance"))return true;
    if(!user.institution)return item.href.includes("/applicant/");
    if(user.institution==="DS"||user.institution==="DEVELOPMENT_RESEARCH")return item.href.endsWith("/cases")||item.href.endsWith("/ocr-demo")||(user.institution==="DS"&&item.href.endsWith("/assignments"));
    return item.href.endsWith("/commissioner")&&user.institution==="COMMISSIONER";
  });

  return (
    <aside
      className={`${styles.sidebar} ${compactMobile ? styles.compactMobile : ""}`}
      aria-label={label}
    >
      <nav className={styles.nav}>
        {visibleItems.map((item) => {
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
