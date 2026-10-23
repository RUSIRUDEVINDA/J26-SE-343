"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import dynamic from "next/dynamic";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import { PageHeading } from "@/shared/components/PageLayout";
import { getParcelById } from "../../services/landIntelligenceApi";
import type { LandParcelResponse } from "../../types/landIntelligence";
import { landUseTypeLabels } from "../../utils/enumMappings";
import { displayOrUnavailable, formatArea } from "../../utils/formatters";
import { resolveParcelMapGeometry } from "../../utils/parcelGeometry";
import { formatGnDivisionPrimaryDisplay } from "../../utils/gnDivisionMappingDisplay";
import {
  sourceReportedLandUseDisplay,
} from "../../utils/sourceReportedCadastre";
import styles from "./ParcelMapView.module.css";

const ParcelMap = dynamic(
  () => import("./ParcelMap").then((module) => module.ParcelMap),
  {
    ssr: false,
    loading: () => <LoadingState message="Loading interactive map…" />,
  },
);

function customerGnDivisionLabel(parcel: LandParcelResponse): string {
  const gnMapping = parcel.gnDivisionMapping;
  if (gnMapping?.reviewStatusDisplay === "Needs administrative review") {
    return "Needs administrative review";
  }
  if (gnMapping) {
    return formatGnDivisionPrimaryDisplay(gnMapping);
  }
  return displayOrUnavailable(parcel.location.gramaNiladhariDivision);
}

function customerLandUseLabel(parcel: LandParcelResponse): string {
  const fromCadastre = sourceReportedLandUseDisplay(parcel.cadastralSource);
  if (fromCadastre) {
    return fromCadastre;
  }
  if (parcel.currentUse?.type != null) {
    return (
      landUseTypeLabels[parcel.currentUse.type] ??
      displayOrUnavailable(parcel.currentUse.description)
    );
  }
  return "Unavailable";
}

function customerLocationUnavailableMessage(
  geometry: ReturnType<typeof resolveParcelMapGeometry>,
): string {
  if (geometry.kind === "unavailable") {
    if (geometry.reason.includes("WGS84")) {
      return "This parcel’s coordinates cannot be displayed on the web map.";
    }
    return "No map location is recorded for this parcel.";
  }
  return "Location unavailable.";
}

type LoadState =
  | { status: "loading" }
  | { status: "error"; message: string; notFound: boolean }
  | { status: "ready"; parcel: LandParcelResponse };

export function ParcelMapView({ parcelId }: { parcelId: string }) {
  const [loadState, setLoadState] = useState<LoadState>({ status: "loading" });
  const [retryToken, setRetryToken] = useState(0);

  useEffect(() => {
    const trimmed = parcelId.trim();
    let cancelled = false;

    if (!trimmed) {
      queueMicrotask(() => {
        if (!cancelled) {
          setLoadState({
            status: "error",
            message: "Parcel id is required.",
            notFound: true,
          });
        }
      });
      return () => {
        cancelled = true;
      };
    }

    queueMicrotask(() => {
      if (!cancelled) {
        setLoadState({ status: "loading" });
      }
    });

    void (async () => {
      try {
        const result = await getParcelById(trimmed);
        if (!cancelled) {
          setLoadState({ status: "ready", parcel: result });
        }
      } catch (err) {
        if (cancelled) {
          return;
        }
        if (err instanceof ApiClientError) {
          setLoadState({
            status: "error",
            message:
              err.status === 404
                ? "Parcel not found."
                : err.detail || err.title,
            notFound: err.status === 404,
          });
          return;
        }
        setLoadState({
          status: "error",
          message:
            err instanceof Error
              ? err.message
              : "Unable to load parcel for the map.",
          notFound: false,
        });
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [parcelId, retryToken]);

  const retry = useCallback(() => {
    setRetryToken((value) => value + 1);
  }, []);

  const parcel =
    loadState.status === "ready" ? loadState.parcel : null;
  const geometry = useMemo(
    () => (parcel ? resolveParcelMapGeometry(parcel) : null),
    [parcel],
  );

  const recommendationsHref = "/land-intelligence/recommendations";
  const intelligenceHref = parcel
    ? `/land-intelligence/parcels/${parcel.id}`
    : `/land-intelligence/parcels/${parcelId}`;

  const cadastral = parcel
    ? displayOrUnavailable(parcel.identifier.cadastralNumber)
    : "";

  return (
    <>
      <PageHeading
        title="Parcel map"
        subtitle="See where this parcel is recorded on the map."
        action={
          <div className={styles.headingActions}>
            <Button
              href={recommendationsHref}
              variant="secondary"
              className={styles.headingAction}
            >
              Back to recommendations
            </Button>
            {parcel ? (
              <Button
                href={intelligenceHref}
                variant="primary"
                className={styles.headingAction}
              >
                View parcel details
              </Button>
            ) : null}
          </div>
        }
      />

      {loadState.status === "loading" ? (
        <LoadingState message="Loading parcel location…" />
      ) : null}

      {loadState.status === "error" ? (
        <div className={styles.errorBlock}>
          <ErrorMessage message={loadState.message} />
          {!loadState.notFound ? (
            <Button type="button" variant="secondary" onClick={retry}>
              Retry
            </Button>
          ) : (
            <Button href="/land-intelligence/parcels" variant="secondary">
              Browse parcels
            </Button>
          )}
        </div>
      ) : null}

      {parcel && geometry ? (
        <div className={styles.workspace}>
          <div className={styles.mapLayout}>
            <section
              className={styles.mapPanel}
              aria-label={`Map for parcel ${cadastral}`}
            >
              {geometry.kind === "unavailable" ? (
                <div className={styles.unavailable} role="status">
                  <p className={styles.unavailableTitle}>Location unavailable</p>
                  <p>{customerLocationUnavailableMessage(geometry)}</p>
                </div>
              ) : (
                <ParcelMap parcel={parcel} geometry={geometry} />
              )}
            </section>

            <aside className={styles.summary} aria-label="Parcel summary">
              <h2 className={styles.summaryTitle}>Parcel summary</h2>
              <dl className={styles.summaryList}>
                <div>
                  <dt>Cadastral number</dt>
                  <dd>{cadastral}</dd>
                </div>
                <div>
                  <dt>Province</dt>
                  <dd>{displayOrUnavailable(parcel.location.province)}</dd>
                </div>
                <div>
                  <dt>District</dt>
                  <dd>{displayOrUnavailable(parcel.location.district)}</dd>
                </div>
                <div>
                  <dt>Grama Niladhari division</dt>
                  <dd>{customerGnDivisionLabel(parcel)}</dd>
                </div>
                <div>
                  <dt>Area</dt>
                  <dd>{formatArea(parcel.area.value, parcel.area.unit)}</dd>
                </div>
                <div>
                  <dt>Source-reported land use</dt>
                  <dd>{customerLandUseLabel(parcel)}</dd>
                </div>
              </dl>
              <div className={styles.summaryActions}>
                <Button
                  href={recommendationsHref}
                  variant="secondary"
                  className={styles.summaryAction}
                >
                  Back to recommendations
                </Button>
                <Button
                  href={intelligenceHref}
                  variant="primary"
                  className={styles.summaryAction}
                >
                  View parcel details
                </Button>
              </div>
            </aside>
          </div>
        </div>
      ) : null}
    </>
  );
}
