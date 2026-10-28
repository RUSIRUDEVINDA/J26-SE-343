"use client";

import { useEffect, useState, useSyncExternalStore } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Button } from "@/shared/components/Button";
import { EmptyState } from "@/shared/components/EmptyState";
import { PageHeading } from "@/shared/components/PageLayout";
import { RecommendationAssessmentPanel } from "@/modules/component-01-land-intelligence/components/RecommendationAssessmentPanel";
import { RecommendationCard } from "@/modules/component-01-land-intelligence/components/RecommendationCard";
import { listResearchSupplement, searchRecommendations } from "@/modules/component-01-land-intelligence/services/landIntelligenceApi";
import {
  FIND_SUITABLE_LAND_FORM_SESSION_KEY,
  RECOMMENDATIONS_SESSION_KEY,
  type FindSuitableLandFormState,
  type LandRecommendationSearchRequest,
  type LandRecommendationSearchResultsResponse,
  type RecommendationsSessionPayload,
  type ResearchSupplementParcelResponse,
} from "@/modules/component-01-land-intelligence/types/landIntelligence";
import { landUseTypeLabels } from "@/modules/component-01-land-intelligence/utils/enumMappings";
import {
  targetAreaUnitShortLabel,
} from "@/modules/component-01-land-intelligence/utils/targetAreaUnits";
import styles from "./RecommendationsView.module.css";

const REC_STORE_EVENT = "component01.recommendations";
const FORM_STORE_EVENT = "component01.findSuitableLandForm";

type PayloadSnapshot = {
  raw: string | null;
  value: RecommendationsSessionPayload | null;
};

let payloadSnapshot: PayloadSnapshot | null = null;

function parsePayload(raw: string | null): RecommendationsSessionPayload | null {
  if (!raw) {
    return null;
  }
  try {
    return JSON.parse(raw) as RecommendationsSessionPayload;
  } catch {
    return null;
  }
}

function getPayloadSnapshot(): RecommendationsSessionPayload | null {
  const raw = sessionStorage.getItem(RECOMMENDATIONS_SESSION_KEY);
  if (payloadSnapshot && payloadSnapshot.raw === raw) {
    return payloadSnapshot.value;
  }
  const value = parsePayload(raw);
  payloadSnapshot = { raw, value };
  return value;
}

function getServerPayloadSnapshot(): RecommendationsSessionPayload | null {
  return null;
}

function subscribePayloadStore(onStoreChange: () => void) {
  const handler = () => onStoreChange();
  window.addEventListener("storage", handler);
  window.addEventListener(REC_STORE_EVENT, handler);
  return () => {
    window.removeEventListener("storage", handler);
    window.removeEventListener(REC_STORE_EVENT, handler);
  };
}

function subscribeNoop() {
  return () => {};
}

function getClientHydrated() {
  return true;
}

function getServerHydrated() {
  return false;
}

function buildSummaryChips(
  request: LandRecommendationSearchRequest,
  formState?: FindSuitableLandFormState,
) {
  const chips: Array<{ label: string; muted?: boolean }> = [
    {
      label: `Purpose: ${landUseTypeLabels[request.requiredPurpose] ?? "Unknown"}`,
    },
    {
      label: request.preferredLocation?.district
        ? `District: ${request.preferredLocation.district}`
        : "District: all",
      muted: !request.preferredLocation?.district,
    },
  ];

  if (request.preferredLocation?.province) {
    chips.push({ label: `Province: ${request.preferredLocation.province}` });
  }
  const targetArea =
    request.targetAreaHectares ??
    request.preferredAreaHectares ??
    request.requiredAreaHectares;
  if (targetArea != null) {
    const entered = formState?.targetAreaValue?.trim();
    const unit = formState?.targetAreaUnit;
    if (entered && unit) {
      chips.push({
        label: `Target area: ${entered} ${targetAreaUnitShortLabel(unit)} (${targetArea} ha scored)`,
      });
    } else {
      chips.push({ label: `Target area: ${targetArea} ha` });
    }
  }
  if (request.sourceReportedLandUse) {
    chips.push({
      label: `Source land use: ${request.sourceReportedLandUse}`,
    });
  }
  if (request.researchLandCoverGroup) {
    chips.push({
      label: `Project land cover: ${request.researchLandCoverGroup}${request.researchLandCoverGroup === "Coconut" ? " (original or GIS)" : ""}`,
    });
  }

  return chips;
}

function hasRestrictiveFilters(formState?: FindSuitableLandFormState | null): boolean {
  if (!formState) {
    return false;
  }
  return Boolean(
    formState.targetAreaValue.trim() ||
      formState.sourceReportedLandUse.trim(),
  );
}

function ResultsSkeleton() {
  return (
    <div className={styles.layout} aria-busy="true" aria-label="Loading results">
      <div className={styles.listColumn}>
        {[0, 1, 2].map((index) => (
          <div key={index} className={styles.skeletonCard}>
            <div className={`${styles.skeletonLine} ${styles.skeletonTitle}`} />
            <div className={`${styles.skeletonLine} ${styles.skeletonMeta}`} />
            <div className={`${styles.skeletonLine} ${styles.skeletonScore}`} />
          </div>
        ))}
      </div>
      <div className={styles.skeletonCard}>
        <div className={`${styles.skeletonLine} ${styles.skeletonTitle}`} />
        <div className={`${styles.skeletonLine} ${styles.skeletonMeta}`} />
        <div className={`${styles.skeletonLine} ${styles.skeletonMeta}`} />
      </div>
    </div>
  );
}

export function RecommendationsView() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [researchSupplement, setResearchSupplement] = useState<ResearchSupplementParcelResponse[]>([]);
  const [loadingFromUrl, setLoadingFromUrl] = useState(false);
  const [urlError, setUrlError] = useState<string | null>(null);
  const [urlResponse, setUrlResponse] = useState<LandRecommendationSearchResultsResponse | null>(null);

  useEffect(() => {
    let cancelled = false;
    listResearchSupplement()
      .then((rows) => { if (!cancelled) setResearchSupplement(rows); })
      .catch(() => { if (!cancelled) setResearchSupplement([]); });
    return () => { cancelled = true; };
  }, []);

  // Load from URL parameters if present
  useEffect(() => {
    const purpose = searchParams.get("purpose");
    if (!purpose) {
      return;
    }

    let cancelled = false;
    setLoadingFromUrl(true);
    setUrlError(null);

    const request: LandRecommendationSearchRequest = {
      requiredPurpose: Number(purpose) as any,
      targetAreaHectares: searchParams.get("area") ? Number(searchParams.get("area")) : undefined,
      preferredLocation: (searchParams.get("district") || searchParams.get("province")) ? {
        district: searchParams.get("district") || undefined,
        province: searchParams.get("province") || undefined,
        divisionalSecretariat: undefined,
      } : undefined,
      sourceReportedLandUse: searchParams.get("landUse") || undefined,
      researchLandCoverGroup: searchParams.get("landCover") || undefined,
      maxResults: searchParams.get("maxResults") ? Number(searchParams.get("maxResults")) : undefined,
    };

    searchRecommendations(request)
      .then((response) => {
        if (!cancelled) {
          setUrlResponse(response);
          setLoadingFromUrl(false);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setUrlError("Failed to load results from URL parameters.");
          setLoadingFromUrl(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [searchParams]);

  const hydrated = useSyncExternalStore(
    subscribeNoop,
    getClientHydrated,
    getServerHydrated,
  );
  const payload = useSyncExternalStore(
    subscribePayloadStore,
    getPayloadSnapshot,
    getServerPayloadSnapshot,
  );
  const supplementByParcelId = new Map(researchSupplement.map((row) => [row.parcelId, row]));

  if (!hydrated) {
    return (
      <>
        <PageHeading
          title="Suitable Land Results"
          subtitle="Loading saved recommendation results…"
        />
        <ResultsSkeleton />
      </>
    );
  }

  if (loadingFromUrl) {
    return (
      <>
        <PageHeading
          title="Suitable Land Results"
          subtitle="Loading results from URL parameters…"
        />
        <ResultsSkeleton />
      </>
    );
  }

  if (urlError) {
    return (
      <>
        <PageHeading
          title="Suitable Land Results"
          subtitle="Ranked parcels from the Component 1 recommendation engine."
        />
        <EmptyState
          title="Failed to load results"
          description={urlError}
          action={
            <Button
              href="/land-intelligence/find-suitable-land"
              variant="primary"
            >
              Find Suitable Land
            </Button>
          }
        />
      </>
    );
  }

  // Use URL response if available, otherwise fall back to sessionStorage
  const response = urlResponse || payload?.response;
  const request = urlResponse ? null : payload?.request;
  const formState = urlResponse ? null : payload?.formState;

  if (!response) {
    return (
      <>
        <PageHeading
          title="Suitable Land Results"
          subtitle="Ranked parcels from the Component 1 recommendation engine."
        />
        <EmptyState
          title="No recommendation results yet"
          description="Run a search from Find Suitable Land to load live API results."
          action={
            <Button
              href="/land-intelligence/find-suitable-land"
              variant="primary"
            >
              Find Suitable Land
            </Button>
          }
        />
      </>
    );
  }
  const transparency = response.transparency;
  const chips = request && formState ? buildSummaryChips(request, formState) : [];
  const canClearFilters = hasRestrictiveFilters(formState);
  const searchDistrict = request?.preferredLocation?.district?.trim() || null;

  function clearRestrictiveFilters() {
    if (!formState) {
      router.push("/land-intelligence/find-suitable-land");
      return;
    }
    const cleared: FindSuitableLandFormState = {
      ...formState,
      targetAreaValue: "",
      targetAreaUnit: "hectares",
      sourceReportedLandUse: "",
    };
    try {
      sessionStorage.setItem(
        FIND_SUITABLE_LAND_FORM_SESSION_KEY,
        JSON.stringify(cleared),
      );
      window.dispatchEvent(new Event(FORM_STORE_EVENT));
    } catch {
      // Session write can fail in private mode; still navigate to the form.
    }
    router.push("/land-intelligence/find-suitable-land");
  }

  return (
    <>
      <PageHeading
        title="Suitable Land Results"
        subtitle=""
        action={
          <Button
            href="/land-intelligence/find-suitable-land"
            variant="secondary"
          >
            Edit search
          </Button>
        }
      />

      <section className={styles.summaryBar} aria-label="Search summary">
        <div className={styles.summaryHeader}>
          <h2 className={styles.summaryTitle}>Search summary</h2>
          <span className={`${styles.chip} ${styles.chipMuted}`}>
            {response.recommendations.length} shown · {response.candidateCount}{" "}
            candidates after filters
          </span>
        </div>
        <div className={styles.chipRow}>
          {chips.length > 0 ? (
            chips.map((chip) => (
              <span
                key={chip.label}
                className={`${styles.chip} ${chip.muted ? styles.chipMuted : ""}`}
              >
                {chip.label}
              </span>
            ))
          ) : (
            <span className={`${styles.chip} ${styles.chipMuted}`}>
              Search parameters loaded from URL
            </span>
          )}
        </div>
        {transparency && transparency.filterStages.length > 0 ? (
          <p className={styles.pipeline}>
            {transparency.filterStages
              .map((stage) => `${stage.candidateCount} after ${stage.name}`)
              .join(" → ")}
          </p>
        ) : null}
      </section>

      {response.recommendations.length === 0 ? (
        <EmptyState
          title="No suitable parcels returned"
          description="No parcels matched the active filters. Adjust the search and try again. Missing GIS coverage is not treated as a positive match."
          action={
            <div>
              <ul className={styles.emptyFilters} aria-label="Active filters">
                {chips.map((chip) => (
                  <li key={chip.label} className={styles.chip}>
                    {chip.label}
                  </li>
                ))}
              </ul>
              <div className={styles.emptyActions}>
                <Button
                  href="/land-intelligence/find-suitable-land"
                  variant="primary"
                >
                  Edit search
                </Button>
                {canClearFilters ? (
                  <Button
                    type="button"
                    variant="secondary"
                    onClick={clearRestrictiveFilters}
                  >
                    Clear restrictive filters
                  </Button>
                ) : null}
              </div>
            </div>
          }
        />
      ) : (
        <div className={styles.layout}>
          <div className={styles.listColumn}>
            <h2 className={styles.listHeading}>
              Results ({response.recommendations.length} shown)
            </h2>
            {response.recommendations.map((item) => (
              <RecommendationCard
                key={item.parcelId}
                recommendation={item}
                searchDistrict={searchDistrict}
                researchCover={supplementByParcelId.get(item.parcelId)}
              />
            ))}
          </div>
          <RecommendationAssessmentPanel
            candidateCount={response.candidateCount}
            resultCount={response.recommendations.length}
            transparency={transparency}
          />
        </div>
      )}
    </>
  );
}
