"use client";

import { useEffect, useMemo, useState } from "react";
import { Button } from "@/shared/components/Button";
import { StatusBadge } from "@/shared/components/StatusBadge";
import {
  getLandIntelligenceGisCoverage,
  getLandParcelMetrics,
  listParcelsSummary,
} from "../services/landIntelligenceApi";
import type { LandIntelligenceGisCoverageResponse } from "../types/landIntelligence";
import {
  gisCoverageBadgeTone,
  gisCoverageStatusLabel,
} from "../utils/gisCoverageStatus";
import styles from "./DashboardStats.module.css";

export function DashboardStats() {
  const [parcelTotal, setParcelTotal] = useState<string | null>(null);
  const [leaseReadyEffective, setLeaseReadyEffective] = useState<string | null>(
    null,
  );
  const [gisCoverage, setGisCoverage] =
    useState<LandIntelligenceGisCoverageResponse | null>(null);
  const [loadFailed, setLoadFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    Promise.all([
      listParcelsSummary(),
      getLandParcelMetrics(),
      getLandIntelligenceGisCoverage(),
    ])
      .then(([summary, metrics, coverage]) => {
        if (!cancelled) {
          setParcelTotal(String(summary.totalCount));
          setLeaseReadyEffective(
            String(metrics.projectLeaseReadyEffectiveCount),
          );
          setGisCoverage(coverage);
          setLoadFailed(false);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setParcelTotal(null);
          setLeaseReadyEffective(null);
          setGisCoverage(null);
          setLoadFailed(true);
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const parcelDisplay = loadFailed ? "Unavailable" : (parcelTotal ?? "…");
  const leaseReadyDisplay = loadFailed
    ? "Unavailable"
    : (leaseReadyEffective ?? "…");

  const primaryDistrict = useMemo(() => {
    if (!gisCoverage?.indexedDistricts.length) {
      return null;
    }
    const top = gisCoverage.indexedDistricts[0];
    return {
      province: top.province,
      district: top.district,
      count: top.parcelCount,
    };
  }, [gisCoverage]);

  const enrichmentStatus = gisCoverage?.overallEnrichmentStatus ?? null;
  const enrichmentLabel = enrichmentStatus
    ? gisCoverageStatusLabel(enrichmentStatus)
    : loadFailed
      ? "Unavailable"
      : "…";

  const configuredDistrictsText =
    gisCoverage && gisCoverage.configuredEnrichmentDistricts.length > 0
      ? gisCoverage.configuredEnrichmentDistricts.join(", ")
      : "None configured";

  return (
    <div className={styles.dashboard}>
      <section className={styles.metrics} aria-label="Key status">
        <article className={`${styles.card} ${styles.metricCard}`}>
          <p className={styles.label}>Indexed districts (live)</p>
          {primaryDistrict ? (
            <>
              <p className={styles.value}>{primaryDistrict.district}</p>
              <span className={styles.chip}>{primaryDistrict.province}</span>
              <p className={styles.meta}>
                {primaryDistrict.count} parcel
                {primaryDistrict.count === 1 ? "" : "s"} in this district
                {gisCoverage && gisCoverage.indexedDistricts.length > 1
                  ? ` · ${gisCoverage.indexedDistricts.length} district(s) total`
                  : ""}
              </p>
            </>
          ) : (
            <>
              <p className={styles.value}>Unavailable</p>
              <p className={styles.meta}>
                District counts are derived from indexed parcel location fields.
              </p>
            </>
          )}
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
          <p className={styles.label}>Lease-ready (project dataset)</p>
          <p
            className={`${styles.value} ${styles.valueAccent}`}
            aria-live="polite"
          >
            {leaseReadyDisplay}
          </p>
          <p className={styles.meta}>
            Parcels marked &quot;Lease-ready — project dataset assumption&quot;
            after excluding source-reported Private. Not official government
            verification.
          </p>
        </article>

        <article className={`${styles.card} ${styles.metricCard}`}>
          <p className={styles.label}>GIS enrichment (database)</p>
          <div className={styles.enrichmentRow}>
            {enrichmentStatus ? (
              <StatusBadge tone={gisCoverageBadgeTone(enrichmentStatus)}>
                {enrichmentLabel}
              </StatusBadge>
            ) : (
              <StatusBadge tone="neutral">{enrichmentLabel}</StatusBadge>
            )}
          </div>
          <p className={styles.enrichmentExplain}>
            {gisCoverage?.overallEnrichmentNotice ??
              "Measured from enrichment snapshots in the connected database. Missing layers are never treated as a positive match."}
          </p>
          {gisCoverage ? (
            <p className={styles.meta}>
              Configured enrichment districts: {configuredDistrictsText}. Snapshots:{" "}
              {gisCoverage.enrichmentSnapshotCount} of{" "}
              {gisCoverage.totalParcelCount}.
            </p>
          ) : null}
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
                GIS-derived evidence where it exists in the database.
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
          <h2 className={styles.sectionTitle}>GIS intelligence coverage</h2>
          <p className={`${styles.meta} ${styles.coverageIntro}`}>
            Per-layer counts are measured from reference tables and parcel-level
            GIS evidence in the connected database. &quot;Unavailable&quot; means
            the layer was not assessed for indexed parcels—not that the parcel
            is clear.
          </p>
          {loadFailed ? (
            <p className={styles.emptyText} role="alert">
              Coverage could not be loaded from the API.
            </p>
          ) : null}
          {!loadFailed && !gisCoverage ? (
            <p className={styles.emptyText}>Loading coverage…</p>
          ) : null}
          {gisCoverage ? (
            <ul className={styles.intelList}>
              {gisCoverage.intelligenceLayers.map((layer) => (
                <li key={layer.layerKey} className={styles.intelItem}>
                  <div className={styles.intelHeader}>
                    <p className={styles.intelName}>{layer.displayName}</p>
                    <StatusBadge tone={gisCoverageBadgeTone(layer.coverageStatus)}>
                      {gisCoverageStatusLabel(layer.coverageStatus)}
                    </StatusBadge>
                  </div>
                  <p className={styles.intelNote}>
                    Reference features: {layer.referenceFeatureCount} · Parcels
                    with evidence: {layer.parcelsWithEvidenceCount} /{" "}
                    {gisCoverage.totalParcelCount}
                  </p>
                  <p className={styles.intelNote}>{layer.limitationSummary}</p>
                </li>
              ))}
            </ul>
          ) : null}
        </article>
      </section>
    </div>
  );
}
