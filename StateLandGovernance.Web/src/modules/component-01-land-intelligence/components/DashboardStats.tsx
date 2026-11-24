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

function formatCount(value: number | null, failed: boolean): string {
  if (failed) {
    return "Unavailable";
  }
  if (value === null) {
    return "…";
  }
  return value.toLocaleString("en-LK");
}

function MetricHint({
  hintId,
  label,
  description,
}: {
  hintId: string;
  label: string;
  description: string;
}) {
  return (
    <span className={styles.metricLabelWrap}>
      <span>{label}</span>
      <button
        type="button"
        className={styles.metricHintButton}
        aria-describedby={hintId}
        aria-label={`About ${label}`}
        title={description}
      >
        <span aria-hidden="true">i</span>
      </button>
      <span id={hintId} className={styles.metricHintText} role="tooltip">
        {description}
      </span>
    </span>
  );
}

const GET_STARTED = [
  {
    href: "/land-intelligence/find-suitable-land",
    title: "Find suitable land",
    description:
      "Set purpose, area, and location preferences to see ranked parcel suggestions.",
    variant: "primary" as const,
    cta: "Start search",
    icon: "01",
  },
  {
    href: "/land-intelligence/parcels",
    title: "Parcel catalog",
    description:
      "Browse indexed parcels, cadastral references, and source-reported attributes.",
    variant: "secondary" as const,
    cta: "Open catalog",
    icon: "02",
  },
];

export function DashboardStats() {
  const [parcelTotal, setParcelTotal] = useState<number | null>(null);
  const [projectDesignatedEffective, setProjectDesignatedEffective] =
    useState<number | null>(null);
  const [gisCoverage, setGisCoverage] =
    useState<LandIntelligenceGisCoverageResponse | null>(null);
  const [landUseOptions, setLandUseOptions] = useState<
    SourceReportedLandUseOptionResponse[] | null
  >(null);
  const [failed, setFailed] = useState({
    parcels: false,
    designated: false,
    location: false,
    landUse: false,
  });

  useEffect(() => {
    let cancelled = false;
    void listParcelsSummary()
      .then((result) => {
        if (!cancelled) {
          setParcelTotal(result.totalCount);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setFailed((current) => ({ ...current, parcels: true }));
        }
      });
    void getLandParcelMetrics()
      .then((result) => {
        if (!cancelled) {
          setProjectDesignatedEffective(
            result.projectDesignatedParcelsEffectiveCount,
          );
        }
      })
      .catch(() => {
        if (!cancelled) {
          setFailed((current) => ({ ...current, designated: true }));
        }
      });
    void getLandIntelligenceGisCoverage()
      .then((result) => {
        if (!cancelled) {
          setGisCoverage(result);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setFailed((current) => ({ ...current, location: true }));
        }
      });
    void listSourceReportedLandUseOptions()
      .then((result) => {
        if (!cancelled) {
          setLandUseOptions(result);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setFailed((current) => ({ ...current, landUse: true }));
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const pilotSummary = useMemo(() => {
    const districts = gisCoverage?.indexedDistricts ?? [];
    if (districts.length === 0) {
      return null;
    }
    const primary = districts[0];
    const districtLabel =
      districts.length > 1
        ? `${primary.district} (+${districts.length - 1} indexed district${
            districts.length - 1 === 1 ? "" : "s"
          })`
        : primary.district;
    return {
      districtLabel,
      province: primary.province,
      districtCount: districts.length,
    };
  }, [gisCoverage]);

  const leadingUses = useMemo(
    () =>
      [...(landUseOptions ?? [])]
        .filter((item) => item.parcelCount > 0)
        .sort((a, b) => b.parcelCount - a.parcelCount)
        .slice(0, 6),
    [landUseOptions],
  );
  const highestUseCount = leadingUses[0]?.parcelCount ?? 0;
  const landUseChartSummary =
    leadingUses.length > 0
      ? leadingUses
          .map((item) => `${item.englishLabel} ${item.parcelCount}`)
          .join(", ")
      : "";

  return (
    <div className={styles.dashboard}>
      <section className={styles.metrics} aria-label="Dataset summary">
        <article
          className={`${styles.metricCard} ${styles.metricCardLocation}`}
        >
          <p className={styles.metricLabel}>
            <MetricHint
              hintId="metric-hint-pilot"
              label="Pilot location"
              description="Geographic area where parcel boundaries and GIS enrichment are indexed for this research pilot. Shown from indexed district coverage, not a national roll-out."
            />
          </p>
          <p
            className={`${styles.metricValue} ${
              !failed.location && !pilotSummary ? styles.metricValueLoading : ""
            }`}
          >
            {failed.location
              ? "Unavailable"
              : pilotSummary?.districtLabel ?? "…"}
          </p>
          <p className={styles.metricNote}>
            {pilotSummary?.province
              ? `${pilotSummary.province} Province`
              : "Loaded from indexed district coverage"}
          </p>
        </article>
        <article
          className={`${styles.metricCard} ${styles.metricCardIndexed}`}
        >
          <p className={styles.metricLabel}>
            <MetricHint
              hintId="metric-hint-indexed"
              label="Indexed parcels"
              description="All parcels currently searchable in the Land Intelligence catalog, including boundaries and source-reported attributes. This is the full indexed count."
            />
          </p>
          <p
            className={`${styles.metricValue} ${
              parcelTotal === null && !failed.parcels
                ? styles.metricValueLoading
                : ""
            }`}
            aria-live="polite"
          >
            {formatCount(parcelTotal, failed.parcels)}
          </p>
          <p className={styles.metricNote}>Searchable in the parcel catalog</p>
        </article>
        <article
          className={`${styles.metricCard} ${styles.metricCardDesignated}`}
        >
          <p className={styles.metricLabel}>
            <MetricHint
              hintId="metric-hint-designated"
              label="Project-designated parcels"
              description="Parcels flagged for the Component 1 project lease-ready designation. This is a separate subset from the full indexed catalog and is not expected to match the indexed total."
            />
          </p>
          <p
            className={`${styles.metricValue} ${
              projectDesignatedEffective === null && !failed.designated
                ? styles.metricValueLoading
                : ""
            }`}
            aria-live="polite"
          >
            {formatCount(projectDesignatedEffective, failed.designated)}
          </p>
          <p className={styles.metricNote}>
            Lease-ready designation (subset of indexed data)
          </p>
        </article>
      </section>

      <div className={styles.lowerGrid}>
        <section
          className={`${styles.panel} ${styles.panelChart}`}
          aria-labelledby="land-use-heading"
        >
          <div className={styles.panelHeading}>
            <div>
              <p className={styles.sectionKicker}>Explore the dataset</p>
              <h2 id="land-use-heading">Land use at a glance</h2>
            </div>
            <Button href="/land-intelligence/parcels" variant="secondary">
              Parcel catalog
            </Button>
          </div>
          <p className={styles.panelIntro}>
            Source-reported land-use categories for indexed parcels. Open the
            catalog to filter or inspect individual records.
          </p>

          {leadingUses.length > 0 ? (
            <div
              className={styles.chart}
              role="img"
              aria-label={`Land use distribution: ${landUseChartSummary}`}
            >
              <ul className={styles.chartList}>
                {leadingUses.map((item) => {
                  const widthPercent =
                    highestUseCount > 0
                      ? (item.parcelCount / highestUseCount) * 100
                      : 0;
                  const barId = `land-use-bar-${item.code}`;
                  return (
                    <li className={styles.chartRow} key={item.code}>
                      <div className={styles.chartRowHeader}>
                        <span className={styles.chartLabel} id={barId}>
                          {item.englishLabel}
                        </span>
                        <span
                          className={styles.chartCount}
                          aria-labelledby={barId}
                        >
                          {item.parcelCount.toLocaleString("en-LK")}
                        </span>
                      </div>
                      <div
                        className={styles.barTrack}
                        role="progressbar"
                        aria-valuemin={0}
                        aria-valuemax={highestUseCount}
                        aria-valuenow={item.parcelCount}
                        aria-labelledby={barId}
                      >
                        <span
                          className={styles.barFill}
                          style={{ width: `${widthPercent}%` }}
                        />
                      </div>
                    </li>
                  );
                })}
              </ul>
            </div>
          ) : (
            <p className={styles.emptyState}>
              {failed.landUse
                ? "Land-use breakdown is unavailable right now."
                : landUseOptions
                  ? "No land-use categories are recorded yet."
                  : "Loading land-use breakdown…"}
            </p>
          )}
        </section>

        <section
          className={`${styles.panel} ${styles.panelTasks}`}
          aria-labelledby="next-steps-heading"
        >
          <div className={styles.panelHeading}>
            <div>
              <p className={styles.sectionKicker}>Get started</p>
              <h2 id="next-steps-heading">Common tasks</h2>
            </div>
          </div>
          <p className={styles.panelIntro}>
            Choose a task below to search for suitable land or browse the parcel
            catalog.
          </p>
          <ul className={styles.actionList}>
            {GET_STARTED.map((action) => (
              <li key={action.href}>
                <article
                  className={`${styles.actionCard} ${
                    action.variant === "primary"
                      ? styles.actionCardPrimary
                      : styles.actionCardSecondary
                  }`}
                >
                  <span
                    className={`${styles.actionIcon} ${
                      action.variant === "primary"
                        ? styles.actionIconPrimary
                        : styles.actionIconSecondary
                    }`}
                    aria-hidden="true"
                  >
                    {action.icon}
                  </span>
                  <div className={styles.actionContent}>
                    <div className={styles.actionBody}>
                      <h3 className={styles.actionTitle}>
                        <Link
                          href={action.href}
                          className={styles.actionTitleLink}
                        >
                          {action.title}
                        </Link>
                      </h3>
                      <p className={styles.actionDescription}>
                        {action.description}
                      </p>
                    </div>
                    <Button
                      href={action.href}
                      variant={action.variant}
                      className={styles.actionButton}
                    >
                      {action.cta}
                    </Button>
                  </div>
                </article>
              </li>
            ))}
          </ul>
        </section>
      </div>
    </div>
  );
}
