"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import dynamic from "next/dynamic";
import Link from "next/link";
import { MapPin } from "lucide-react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import { PageHeading } from "@/shared/components/PageLayout";
import { StatusBadge } from "@/shared/components/StatusBadge";
import { getParcelById } from "../../services/landIntelligenceApi";
import type { LandParcelResponse } from "../../types/landIntelligence";
import {
  displayOrUnavailable,
  formatArea,
} from "../../utils/formatters";
import {
  formatCoordinatePair,
  resolveParcelMapGeometry,
} from "../../utils/parcelGeometry";
import styles from "./ParcelMapView.module.css";

const ParcelMap = dynamic(
  () => import("./ParcelMap").then((module) => module.ParcelMap),
  {
    ssr: false,
    loading: () => <LoadingState message="Loading interactive map…" />,
  },
);

function describeProvenance(parcel: LandParcelResponse): string[] {
  const labels: string[] = [];
  const cadastral = parcel.identifier.cadastralNumber ?? "";
  const category = parcel.category.description ?? "";
  const use = parcel.currentUse?.description ?? "";

  if (/DEMO/i.test(cadastral) || /DEMO SYNTHETIC/i.test(category)) {
    labels.push("Demo / synthetic parcel fixture");
  }
  if (/SIMULATED/i.test(category) || /research/i.test(category)) {
    labels.push("Research or simulated attributes present");
  }
  if (/SYNTHETIC/i.test(use) || /DEMO SYNTHETIC/i.test(use)) {
    labels.push("Synthetic land-use label");
  }
  if (parcel.spatial.boundaryPolygon == null) {
    labels.push("Boundary not stored — centroid may be shown");
  }
  if (labels.length === 0) {
    labels.push("Source labels follow API parcel attributes");
  }
  return labels;
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

  return (
    <>
      <PageHeading
        title="Parcel map"
        subtitle="Interactive location view for a single land parcel. Map display does not imply approval, ownership verification, or suitability."
        action={
          <div className={styles.headingActions}>
            <Button href={recommendationsHref} variant="secondary">
              Back to recommendations
            </Button>
            <Button href={intelligenceHref} variant="primary">
              View Full Parcel Intelligence
            </Button>
          </div>
        }
      />

      <p className={styles.fallbackNote}>
        If you opened this page directly and have no saved search,{" "}
        <Link href="/land-intelligence/find-suitable-land">
          Find Suitable Land
        </Link>{" "}
        or the{" "}
        <Link href="/land-intelligence/parcels">parcel catalog</Link> are
        available.
      </p>

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
        <div className={styles.layout}>
          <aside className={styles.facts} aria-label="Parcel map facts">
            <h2 className={styles.factsTitle}>
              <MapPin size={18} aria-hidden />
              Parcel facts
            </h2>

            <dl className={styles.factList}>
              <div>
                <dt>Cadastral / source id</dt>
                <dd>
                  {displayOrUnavailable(parcel.identifier.cadastralNumber)}
                </dd>
              </div>
              <div>
                <dt>Parcel UUID</dt>
                <dd className={styles.mono}>{parcel.id}</dd>
              </div>
              <div>
                <dt>District</dt>
                <dd>{displayOrUnavailable(parcel.location.district)}</dd>
              </div>
              <div>
                <dt>Province</dt>
                <dd>{displayOrUnavailable(parcel.location.province)}</dd>
              </div>
              <div>
                <dt>Available area</dt>
                <dd>{formatArea(parcel.area.value, parcel.area.unit)}</dd>
              </div>
              <div>
                <dt>Recorded coordinates</dt>
                <dd>
                  {formatCoordinatePair(
                    parcel.spatial.centroidLatitude,
                    parcel.spatial.centroidLongitude,
                  )}
                </dd>
              </div>
              <div>
                <dt>Coordinate system</dt>
                <dd>
                  {displayOrUnavailable(parcel.spatial.coordinateSystem)}
                </dd>
              </div>
            </dl>

            <div className={styles.badgeRow}>
              {parcel.environmentalRestrictionCount > 0 ? (
                <StatusBadge tone="danger">
                  Environmental restrictions:{" "}
                  {parcel.environmentalRestrictionCount}
                </StatusBadge>
              ) : null}
              {parcel.spatialConstraintCount > 0 ? (
                <StatusBadge tone="danger">
                  Spatial constraints: {parcel.spatialConstraintCount}
                </StatusBadge>
              ) : null}
            </div>

            <div className={styles.provenance}>
              <h3 className={styles.provenanceTitle}>Provenance labels</h3>
              <ul>
                {describeProvenance(parcel).map((label) => (
                  <li key={label}>{label}</li>
                ))}
              </ul>
            </div>

            <p className={styles.disclaimer} role="note">
              This map is for spatial orientation only. It does not confirm
              legal status, ownership, or recommendation outcomes.
            </p>
          </aside>

          <section className={styles.mapColumn}>
            {geometry.kind === "unavailable" ? (
              <div className={styles.unavailable} role="status">
                <p className={styles.unavailableTitle}>Location unavailable</p>
                <p>{geometry.reason}</p>
                <p className={styles.unavailableHint}>
                  Recorded coordinates:{" "}
                  {formatCoordinatePair(
                    parcel.spatial.centroidLatitude,
                    parcel.spatial.centroidLongitude,
                  )}
                </p>
              </div>
            ) : (
              <ParcelMap parcel={parcel} geometry={geometry} />
            )}
          </section>
        </div>
      ) : null}
    </>
  );
}
