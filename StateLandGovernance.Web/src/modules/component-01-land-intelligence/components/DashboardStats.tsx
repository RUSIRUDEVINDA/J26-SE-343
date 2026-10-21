"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { Button } from "@/shared/components/Button";
import {
  getLandIntelligenceGisCoverage,
  getLandParcelMetrics,
  listParcelsSummary,
  listSourceReportedLandUseOptions,
} from "../services/landIntelligenceApi";
import type {
  LandIntelligenceGisCoverageResponse,
  SourceReportedLandUseOptionResponse,
} from "../types/landIntelligence";
import styles from "./DashboardStats.module.css";

export function DashboardStats() {
  const [parcelTotal, setParcelTotal] = useState<number | null>(null);
  const [projectDesignatedEffective, setProjectDesignatedEffective] = useState<number | null>(null);
  const [gisCoverage, setGisCoverage] = useState<LandIntelligenceGisCoverageResponse | null>(null);
  const [landUseOptions, setLandUseOptions] = useState<SourceReportedLandUseOptionResponse[] | null>(null);
  const [failed, setFailed] = useState({ parcels: false, designated: false, location: false, landUse: false });

  useEffect(() => {
    let cancelled = false;
    void listParcelsSummary()
      .then((result) => { if (!cancelled) setParcelTotal(result.totalCount); })
      .catch(() => { if (!cancelled) setFailed((current) => ({ ...current, parcels: true })); });
    void getLandParcelMetrics()
      .then((result) => { if (!cancelled) setProjectDesignatedEffective(result.projectDesignatedParcelsEffectiveCount); })
      .catch(() => { if (!cancelled) setFailed((current) => ({ ...current, designated: true })); });
    void getLandIntelligenceGisCoverage()
      .then((result) => { if (!cancelled) setGisCoverage(result); })
      .catch(() => { if (!cancelled) setFailed((current) => ({ ...current, location: true })); });
    void listSourceReportedLandUseOptions()
      .then((result) => { if (!cancelled) setLandUseOptions(result); })
      .catch(() => { if (!cancelled) setFailed((current) => ({ ...current, landUse: true })); });
    return () => { cancelled = true; };
  }, []);

  const primaryDistrict = gisCoverage?.indexedDistricts[0] ?? null;
  const leadingUses = useMemo(() =>
    [...(landUseOptions ?? [])]
      .filter((item) => item.parcelCount > 0)
      .sort((a, b) => b.parcelCount - a.parcelCount)
      .slice(0, 5),
    [landUseOptions],
  );
  const highestUseCount = leadingUses[0]?.parcelCount ?? 0;

  return (
    <div className={styles.dashboard}>

      <section className={styles.metrics} aria-label="Dataset at a glance">
        <article className={styles.metricCard}>
          <p className={styles.metricLabel}>Pilot location</p>
          <p className={styles.metricValue}>{primaryDistrict?.district ?? (failed.location ? "Unavailable" : "…")}</p>
          <p className={styles.metricNote}>{primaryDistrict?.province ? `${primaryDistrict.province} Province` : "Based on indexed parcel locations"}</p>
        </article>
        <article className={styles.metricCard}>
          <p className={styles.metricLabel}>Indexed parcels</p>
          <p className={styles.metricValue} aria-live="polite">{parcelTotal ?? (failed.parcels ? "Unavailable" : "…")}</p>
          <p className={styles.metricNote}>Records available to browse and compare</p>
        </article>
        <article className={styles.metricCard}>
          <p className={styles.metricLabel}>Project-designated parcels</p>
          <p className={styles.metricValue} aria-live="polite">{projectDesignatedEffective ?? (failed.designated ? "Unavailable" : "…")}</p>
          <p className={styles.metricNote}>Project dataset assumption; official lease availability is unverified</p>
        </article>
      </section>

      <div className={styles.lowerGrid}>
        <section className={styles.panel} aria-labelledby="land-use-heading">
          <div className={styles.panelHeading}>
            <div><p className={styles.sectionKicker}>Explore the dataset</p><h2 id="land-use-heading">Land use at a glance</h2></div>
            <Link href="/land-intelligence/parcels" className={styles.panelLink}>View parcels →</Link>
          </div>
          <p className={styles.panelIntro}>Top source-reported land-use categories in the indexed parcel data. These labels do not confirm lease availability.</p>
          {leadingUses.length > 0 ? (
            <div className={styles.bars}>
              {leadingUses.map((item) => (
                <div className={styles.barRow} key={item.code}>
                  <div className={styles.barHeading}><span>{item.englishLabel}</span><strong>{item.parcelCount}</strong></div>
                  <div className={styles.barTrack}><span style={{ width: `${(item.parcelCount / highestUseCount) * 100}%` }} /></div>
                </div>
              ))}
            </div>
          ) : (
            <p className={styles.emptyState}>{failed.landUse ? "Land-use breakdown is unavailable right now." : landUseOptions ? "No land-use categories are recorded yet." : "Loading land-use breakdown…"}</p>
          )}
        </section>

        <section className={styles.panel} aria-labelledby="next-steps-heading">
          <div className={styles.panelHeading}><div><p className={styles.sectionKicker}>Get started</p><h2 id="next-steps-heading">What would you like to do?</h2></div></div>
          <div className={styles.actionList}>
            <Link href="/land-intelligence/find-suitable-land" className={styles.actionItem}>
              <span className={styles.actionIcon}>01</span><span><strong>Find a suitable parcel</strong><small>Compare parcels against your purpose, target area, and location.</small></span><span className={styles.actionArrow}>→</span>
            </Link>
            <Link href="/land-intelligence/parcels" className={styles.actionItem}>
              <span className={styles.actionIcon}>02</span><span><strong>Explore parcel records</strong><small>Review boundaries, source details, and available evidence.</small></span><span className={styles.actionArrow}>→</span>
            </Link>
            <Link href="/land-intelligence/my-requests" className={styles.actionItem}>
              <span className={styles.actionIcon}>03</span><span><strong>Track your requests</strong><small>See draft and submitted lease-interest requests.</small></span><span className={styles.actionArrow}>→</span>
            </Link>
          </div>
        </section>
      </div>
      
    </div>
  );
}
