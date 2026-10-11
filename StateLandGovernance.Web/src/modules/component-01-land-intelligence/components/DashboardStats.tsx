"use client";

import { useEffect, useState } from "react";
import { Button } from "@/shared/components/Button";
import { StatusBadge } from "@/shared/components/StatusBadge";
import { listParcelsSummary } from "../services/landIntelligenceApi";
import styles from "./DashboardStats.module.css";

const AVAILABLE_INTELLIGENCE: Array<{
  name: string;
  note?: string;
  warn?: boolean;
}> = [
  { name: "Administrative verification" },
  { name: "Road accessibility" },
  { name: "Water proximity", note: "Evidence" },
  { name: "GIS soil group", note: "Evidence" },
  { name: "Conservation intersections" },
  { name: "Erosion observations", note: "May be unavailable", warn: true },
];

export function DashboardStats() {
  const [parcelTotal, setParcelTotal] = useState<string | null>(null);
  const [loadFailed, setLoadFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    listParcelsSummary()
      .then((result) => {
        if (!cancelled) {
          setParcelTotal(String(result.totalCount));
          setLoadFailed(false);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setParcelTotal(null);
          setLoadFailed(true);
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const parcelDisplay = loadFailed
    ? "Unavailable"
    : (parcelTotal ?? "…");

  return (
    <div className={styles.dashboard}>
      <section className={styles.metrics} aria-label="Key status">
        <article className={`${styles.card} ${styles.metricCard}`}>
          <p className={styles.label}>Pilot district</p>
          <p className={styles.value}>Colombo</p>
          <span className={styles.chip}>Western Province</span>
        </article>

        <article className={`${styles.card} ${styles.metricCard}`}>
          <p className={styles.label}>Indexed parcels</p>
          <p
            className={`${styles.value} ${styles.valueAccent}`}
            aria-live="polite"
          >
            {parcelDisplay}
          </p>
          <p className={styles.meta}>
            Live count from the Land Intelligence parcel API.
          </p>
        </article>

        <article className={`${styles.card} ${styles.metricCard}`}>
          <p className={styles.label}>Enrichment</p>
          <div className={styles.enrichmentRow}>
            <StatusBadge tone="warning">Partial</StatusBadge>
          </div>
          <p className={styles.enrichmentExplain}>
            Some GIS evidence may be unavailable for individual parcels.
            Missing layers are never treated as a positive match.
          </p>
        </article>
      </section>

      <section className={styles.lower} aria-label="Overview and capabilities">
        <article className={styles.card}>
          <h2 className={styles.sectionTitle}>Spatial overview</h2>
          <div className={styles.overviewBody}>
            <div className={styles.emptyState} role="status">
              <p className={styles.emptyTitle}>
                No district overview map is available here
              </p>
              <p className={styles.emptyText}>
                This dashboard does not include a spatial chart or map layer.
                Browse parcel intelligence for individual records, or run Find
                Suitable Land to compare parcels using recorded attributes and
                GIS-derived evidence.
              </p>
            </div>
            <div className={styles.actions}>
              <Button
                href="/land-intelligence/find-suitable-land"
                variant="primary"
              >
                Find Suitable Land
              </Button>
              <Button href="/land-intelligence/parcels" variant="secondary">
                Browse parcels
              </Button>
            </div>
          </div>
        </article>

        <article className={styles.card}>
          <h2 className={styles.sectionTitle}>Available intelligence</h2>
          <ul className={styles.intelList}>
            {AVAILABLE_INTELLIGENCE.map((item) => (
              <li key={item.name} className={styles.intelItem}>
                <p className={styles.intelName}>{item.name}</p>
                {item.note ? (
                  <p
                    className={`${styles.intelNote} ${item.warn ? styles.intelNoteWarn : ""}`}
                  >
                    {item.note}
                  </p>
                ) : null}
              </li>
            ))}
          </ul>
        </article>
      </section>
    </div>
  );
}
