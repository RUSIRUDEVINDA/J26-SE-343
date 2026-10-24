"use client";

import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Button } from "@/shared/components/Button";
import { EmptyState } from "@/shared/components/EmptyState";
import { PageHeading } from "@/shared/components/PageLayout";
import { RecommendationAssessmentPanel } from "@/modules/component-01-land-intelligence/components/RecommendationAssessmentPanel";
import { RecommendationCard } from "@/modules/component-01-land-intelligence/components/RecommendationCard";
import {
  listResearchSupplement,
  searchRecommendations,
} from "@/modules/component-01-land-intelligence/services/landIntelligenceApi";
import {
  FIND_SUITABLE_LAND_FORM_SESSION_KEY,
  FIND_SUITABLE_LAND_RESTORE_ONCE_KEY,
  RECOMMENDATIONS_SESSION_KEY,
  type FindSuitableLandFormState,
  type LandRecommendationSearchRequest,
  type LandRecommendationSearchResultsResponse,
  type RecommendationsSessionPayload,
  type ResearchSupplementParcelResponse,
} from "@/modules/component-01-land-intelligence/types/landIntelligence";
import { landUseTypeLabels } from "@/modules/component-01-land-intelligence/utils/enumMappings";
import {
  parseRecommendationSearchParams,
  recommendationSearchFingerprint,
  sessionMatchesUrlFingerprint,
} from "@/modules/component-01-land-intelligence/utils/recommendationSearchState";
import { targetAreaUnitShortLabel } from "@/modules/component-01-land-intelligence/utils/targetAreaUnits";
import styles from "./RecommendationsView.module.css";

const REC_STORE_EVENT = "component01.recommendations";
const FORM_STORE_EVENT = "component01.findSuitableLandForm";

type PayloadSnapshot = {
  raw: string | null;
  value: RecommendationsSessionPayload | null;
};

type SearchContext = {
  request: LandRecommendationSearchRequest;
  formState: FindSuitableLandFormState;
  response: LandRecommendationSearchResultsResponse;
  fingerprint: string;
  source: "url" | "session";
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

export function buildSummaryChips(
  request: LandRecommendationSearchRequest,
  formState?: FindSuitableLandFormState | null,
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
  if (request.preferredLocation?.gramaNiladhariDivision) {
    chips.push({
      label: `Grama Niladhari division: ${request.preferredLocation.gramaNiladhariDivision}`,
    });
  }
  const targetArea =
    request.targetAreaHectares ??
    request.preferredAreaHectares ??
    request.requiredAreaHectares;
  if (targetArea != null) {
    const entered = formState?.targetAreaValue?.trim();
    const unit = formState?.targetAreaUnit;
    const scoredHa = Number(targetArea).toFixed(3);
    if (entered && unit) {
      chips.push({
        label: `Target area: ${entered} ${targetAreaUnitShortLabel(unit)} (${scoredHa} ha scored)`,
      });
    } else {
      chips.push({ label: `Target area: ${scoredHa} ha` });
    }
  }
  if (request.sourceReportedLandUse) {
    chips.push({
      label: `Source land use: ${request.sourceReportedLandUse}`,
    });
  }
  if (request.researchLandCoverGroup) {
    chips.push({
      label: `Land use: ${request.researchLandCoverGroup} (original label or GIS)`,
    });
  }
  if (request.maxResults != null) {
    chips.push({ label: `Max results: ${request.maxResults}` });
  }

  return chips;
}

function hasRestrictiveFilters(
  formState?: FindSuitableLandFormState | null,
): boolean {
  if (!formState) {
    return false;
  }
  return Boolean(
    formState.targetAreaValue.trim() || formState.sourceReportedLandUse.trim(),
  );
}

function writeFormForEdit(formState: FindSuitableLandFormState) {
  try {
    sessionStorage.setItem(
      FIND_SUITABLE_LAND_FORM_SESSION_KEY,
      JSON.stringify(formState),
    );
    sessionStorage.setItem(FIND_SUITABLE_LAND_RESTORE_ONCE_KEY, "1");
    window.dispatchEvent(new Event(FORM_STORE_EVENT));
  } catch {
    // Session write can fail in private mode.
  }
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
  const [researchSupplement, setResearchSupplement] = useState<
    ResearchSupplementParcelResponse[]
  >([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [searchContext, setSearchContext] = useState<SearchContext | null>(
    null,
  );
  const fetchedFingerprintRef = useRef<string | null>(null);

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

  useEffect(() => {
    let cancelled = false;
    listResearchSupplement()
      .then((rows) => {
        if (!cancelled) setResearchSupplement(rows);
      })
      .catch(() => {
        if (!cancelled) setResearchSupplement([]);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!hydrated) {
      return;
    }

    const parsed = parseRecommendationSearchParams(searchParams);
    if (!parsed) {
      if (payload?.request && payload.response) {
        const fingerprint = recommendationSearchFingerprint(
          payload.request,
          payload.formState,
        );
        setSearchContext({
          request: payload.request,
          formState: payload.formState ?? {
            requiredPurpose: payload.request.requiredPurpose,
            targetAreaValue: "",
            targetAreaUnit: "hectares",
            preferredProvince: "",
            preferredDistrict: "",
            preferredGramaNiladhariDivision: "",
            sourceReportedLandUse: "",
            maxResults: String(payload.request.maxResults ?? 10),
          },
          response: payload.response,
          fingerprint,
          source: "session",
        });
        setError(null);
        setLoading(false);
      }
      return;
    }

    const { request, formState, fingerprint } = parsed;

    // Reuse session results when they match URL criteria — avoids duplicate API call
    // after Find Suitable Land already searched and stored the payload.
    if (
      payload?.response &&
      sessionMatchesUrlFingerprint(
        recommendationSearchFingerprint(payload.request, payload.formState),
        fingerprint,
      )
    ) {
      fetchedFingerprintRef.current = fingerprint;
      setSearchContext({
        request: payload.request,
        formState: payload.formState ?? formState,
        response: payload.response,
        fingerprint,
        source: "session",
      });
      setError(null);
      setLoading(false);
      return;
    }

    if (fetchedFingerprintRef.current === fingerprint && searchContext) {
      return;
    }

    let cancelled = false;
    fetchedFingerprintRef.current = fingerprint;
    setLoading(true);
    setError(null);

    searchRecommendations(request)
      .then((response) => {
        if (cancelled) {
          return;
        }
        const next: SearchContext = {
          request,
          formState,
          response,
          fingerprint,
          source: "url",
        };
        setSearchContext(next);
        try {
          const sessionPayload: RecommendationsSessionPayload = {
            request,
            response,
            submittedAt: new Date().toISOString(),
            formState,
          };
          sessionStorage.setItem(
            RECOMMENDATIONS_SESSION_KEY,
            JSON.stringify(sessionPayload),
          );
          window.dispatchEvent(new Event(REC_STORE_EVENT));
        } catch {
          // ignore session write failures
        }
        setLoading(false);
      })
      .catch(() => {
        if (!cancelled) {
          setError("Failed to load results from URL parameters.");
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
    // searchContext intentionally omitted — fingerprint ref guards re-entry
    // eslint-disable-next-line react-hooks/exhaustive-deps -- URL + session driven
  }, [hydrated, searchParams, payload]);

  const supplementByParcelId = new Map(
    researchSupplement.map((row) => [row.parcelId, row]),
  );

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

  if (loading) {
    return (
      <>
        <PageHeading
          title="Suitable Land Results"
          subtitle="Loading recommendation results…"
        />
        <ResultsSkeleton />
      </>
    );
  }

  if (error) {
    return (
      <>
        <PageHeading
          title="Suitable Land Results"
          subtitle="Ranked parcels from the Component 1 recommendation engine."
        />
        <EmptyState
          title="Failed to load results"
          description={error}
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

  if (!searchContext) {
    return (
      <>
        <PageHeading
          title="Suitable Land Results"
          subtitle=""
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

  const { request, formState, response } = searchContext;
  const transparency = response.transparency;
  const chips = buildSummaryChips(request, formState);
  const canClearFilters = hasRestrictiveFilters(formState);
  const searchDistrict = request.preferredLocation?.district?.trim() || null;

  function goToEditSearch() {
    writeFormForEdit(formState);
    router.push("/land-intelligence/find-suitable-land");
  }

  function clearRestrictiveFilters() {
    const cleared: FindSuitableLandFormState = {
      ...formState,
      targetAreaValue: "",
      targetAreaUnit: "hectares",
      sourceReportedLandUse: "",
    };
    writeFormForEdit(cleared);
    router.push("/land-intelligence/find-suitable-land");
  }

  return (
    <>
      <PageHeading
        title="Suitable Land Results"
        subtitle=""
        action={
          <Button type="button" variant="secondary" onClick={goToEditSearch}>
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
          {chips.map((chip) => (
            <span
              key={chip.label}
              className={`${styles.chip} ${chip.muted ? styles.chipMuted : ""}`}
            >
              {chip.label}
            </span>
          ))}
        </div>
      </section>

      {response.recommendations.length === 0 ? (
        <EmptyState
          title="No matching parcels"
          description="No parcels in the current dataset matched your search criteria. Broaden the location, target area, or land-use filters and try again. Missing GIS coverage is not treated as a match."
          action={
            <div className={styles.emptyActions}>
              <Button
                type="button"
                variant="primary"
                onClick={goToEditSearch}
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
          }
        />
      ) : (
        <div className={styles.layout}>
          <div className={styles.listColumn}>
            <h2 className={styles.listHeading}>
              Results ({response.recommendations.length} shown · ranked by
              suitability score)
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
