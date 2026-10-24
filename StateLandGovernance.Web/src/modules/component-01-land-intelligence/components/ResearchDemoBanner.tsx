"use client";

import styles from "./ResearchDemoBanner.module.css";

export function ResearchDemoBanner() {
  const enabled =
    process.env.NEXT_PUBLIC_LAND_INTELLIGENCE_RESEARCH_DEMO === "true";

  if (!enabled) {
    return null;
  }

}
