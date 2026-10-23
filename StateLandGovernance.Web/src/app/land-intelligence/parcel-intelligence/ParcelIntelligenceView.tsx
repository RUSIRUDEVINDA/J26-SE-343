"use client";

import { useRouter } from "next/navigation";
import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type FormEvent,
} from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { EmptyState } from "@/shared/components/EmptyState";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import { PageHeading } from "@/shared/components/PageLayout";
import {
  ParcelCatalog,
  type ParcelCatalogPageSize,
} from "@/modules/component-01-land-intelligence/components/ParcelCatalog";
import { ParcelDetails } from "@/modules/component-01-land-intelligence/components/ParcelDetails";
import { SaveParcelButton } from "@/modules/component-01-land-intelligence/components/SaveParcelButton";
import {
  findParcelsByCadastralNumber,
  getLandIntelligenceGisCoverage,
  getParcelById,
  getParcelRelationships,
  listAllParcels,
  listCanonicalLandUseOptions,
  listResearchSupplement,
  listSourceReportedLandUseOptions,
} from "@/modules/component-01-land-intelligence/services/landIntelligenceApi";
import type {
  CanonicalLandUseOptionResponse,
  IndexedDistrictSummaryResponse,
  LandParcelResponse,
  LandRelationshipResponse,
  LandSearchResultResponse,
  ResearchSupplementParcelResponse,
} from "@/modules/component-01-land-intelligence/types/landIntelligence";
import {
  buildCanonicalLandUseChoices,
  buildCanonicalLandUseFallbackOptions,
  CANONICAL_FORM_VALUE_PREFIX,
  normalizeLandUseFilterValue,
} from "@/modules/component-01-land-intelligence/utils/canonicalLandUse";
import { sourceReportedLandUseDisplay } from "@/modules/component-01-land-intelligence/utils/sourceReportedCadastre";
import filterStyles from "./ParcelIntelligenceFilters.module.css";

type AppliedFilters = {
  district: string;
  landUseValue: string;
  landUseLabel: string;
};

function landUseLabelFromValue(value: string): string {
  const normalized = normalizeLandUseFilterValue(value);
  if (!normalized.startsWith(CANONICAL_FORM_VALUE_PREFIX)) {
    return "";
  }
  return normalized.slice(CANONICAL_FORM_VALUE_PREFIX.length).trim();
}

export function ParcelIntelligenceView({
  initialParcelId = "",
  showCatalog = true,
}: {
  initialParcelId?: string;
  showCatalog?: boolean;
}) {
  const router = useRouter();
  const [detailLoading, setDetailLoading] = useState(
    Boolean(initialParcelId.trim()),
  );
  const [relationshipsLoading, setRelationshipsLoading] = useState(false);
  const [parcels, setParcels] = useState<LandSearchResultResponse[]>([]);
  const [parcel, setParcel] = useState<LandParcelResponse | null>(null);
  const [relationships, setRelationships] = useState<
    LandRelationshipResponse[] | null
  >(null);
  const [listLoading, setListLoading] = useState(showCatalog);
  const [listError, setListError] = useState<string | null>(null);
  const [filtersApplied, setFiltersApplied] = useState(false);
  const [districtDraft, setDistrictDraft] = useState("");
  const [landUseDraft, setLandUseDraft] = useState("");
  const [applied, setApplied] = useState<AppliedFilters | null>(null);
  const [indexedDistricts, setIndexedDistricts] = useState<
    IndexedDistrictSummaryResponse[]
  >([]);
  const [districtsLoading, setDistrictsLoading] = useState(showCatalog);
  const [districtsError, setDistrictsError] = useState<string | null>(null);
  const [canonicalOptions, setCanonicalOptions] = useState<
    CanonicalLandUseOptionResponse[]
  >([]);
  const [landUseOptionsLoading, setLandUseOptionsLoading] = useState(false);
  const [landUseOptionsError, setLandUseOptionsError] = useState<string | null>(
    null,
  );
  const [supplement, setSupplement] = useState<
    ResearchSupplementParcelResponse[]
  >([]);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [relationshipsError, setRelationshipsError] = useState<string | null>(
    null,
  );
  const [selectedId, setSelectedId] = useState<string | null>(
    initialParcelId.trim() || null,
  );
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<ParcelCatalogPageSize>(20);
  const [cadastralQuery, setCadastralQuery] = useState("");
  const [cadastralLoading, setCadastralLoading] = useState(false);
  const [cadastralError, setCadastralError] = useState<string | null>(null);
  const [cadastralEmpty, setCadastralEmpty] = useState(false);
  const [cadastralMatches, setCadastralMatches] = useState<
    LandSearchResultResponse[]
  >([]);
  const loadedRouteId = useRef<string | null>(null);
  const initialCatalogLoadedRef = useRef(false);

  const loadRelationships = useCallback(async (id: string) => {
    setRelationshipsLoading(true);
    setRelationshipsError(null);
    setRelationships(null);
    try {
      const result = await getParcelRelationships(id);
      setRelationships(result.relationships);
    } catch (err) {
      if (err instanceof ApiClientError) {
        setRelationshipsError(err.detail || err.title);
      } else if (err instanceof Error) {
        setRelationshipsError(err.message);
      } else {
        setRelationshipsError("Unable to load knowledge graph relationships.");
      }
      setRelationships([]);
    } finally {
      setRelationshipsLoading(false);
    }
  }, []);

  const loadParcelDetails = useCallback(
    async (id: string, navigate = true) => {
      const trimmed = id.trim();
      if (!trimmed) {
        setDetailError("Enter a cadastral number to find a parcel.");
        return;
      }
      setDetailLoading(true);
      setDetailError(null);
      setParcel(null);
      setRelationships(null);
      setRelationshipsError(null);
      setSelectedId(trimmed);
      try {
        const parcelResult = await getParcelById(trimmed);
        setParcel(parcelResult);
        if (navigate) {
          router.push(`/land-intelligence/parcels/${trimmed}`);
        }
        void loadRelationships(trimmed);
      } catch (err) {
        if (err instanceof ApiClientError) {
          setDetailError(err.detail || err.title);
        } else if (err instanceof Error) {
          setDetailError(err.message);
        } else {
          setDetailError("Unable to load parcel intelligence.");
        }
      } finally {
        setDetailLoading(false);
      }
    },
    [loadRelationships, router],
  );

  const loadCatalog = useCallback(
    async (filters: AppliedFilters) => {
      if (!showCatalog) {
        return;
      }
      setListLoading(true);
      setListError(null);
      try {
        const landUseLabel = landUseLabelFromValue(filters.landUseValue);
        const response = await listAllParcels({
          district: filters.district,
          researchLandCoverGroup: landUseLabel || undefined,
        });
        setParcels(response.results);
        setPage(1);
        setFiltersApplied(true);
        setApplied(filters);
      } catch (err) {
        if (err instanceof ApiClientError) {
          setListError(err.detail || err.title);
        } else if (err instanceof Error) {
          setListError(err.message);
        } else {
          setListError("Unable to load the parcel catalog.");
        }
        setParcels([]);
        setFiltersApplied(false);
      } finally {
        setListLoading(false);
      }
    },
    [showCatalog],
  );

  useEffect(() => {
    if (!showCatalog || initialCatalogLoadedRef.current) {
      return;
    }
    initialCatalogLoadedRef.current = true;
    void loadCatalog({
      district: "",
      landUseValue: "",
      landUseLabel: "",
    });
  }, [showCatalog, loadCatalog]);

  useEffect(() => {
    if (!showCatalog) {
      return;
    }
    let cancelled = false;
    setDistrictsLoading(true);
    setDistrictsError(null);
    getLandIntelligenceGisCoverage()
      .then((coverage) => {
        if (cancelled) {
          return;
        }
        setIndexedDistricts(coverage.indexedDistricts ?? []);
      })
      .catch((err) => {
        if (cancelled) {
          return;
        }
        setIndexedDistricts([]);
        setDistrictsError(
          err instanceof ApiClientError
            ? err.detail || err.title
            : "Unable to load indexed districts. Try refreshing the page.",
        );
      })
      .finally(() => {
        if (!cancelled) {
          setDistrictsLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [showCatalog]);

  useEffect(() => {
    if (!showCatalog) {
      return;
    }
    let cancelled = false;
    listResearchSupplement()
      .then((rows) => {
        if (!cancelled) {
          setSupplement(rows);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setSupplement([]);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [showCatalog]);

  useEffect(() => {
    if (!showCatalog) {
      return;
    }

    let cancelled = false;
    const district = districtDraft.trim();
    setLandUseOptionsLoading(true);
    setLandUseOptionsError(null);

    // Prefer the canonical endpoint (district-scoped when set). If the running API does not
    // expose it yet, fall back the same way Find Suitable Land does.
    void (async () => {
      try {
        let options: CanonicalLandUseOptionResponse[] | null = null;
        try {
          options = district
            ? await listCanonicalLandUseOptions(district)
            : await listCanonicalLandUseOptions();
        } catch {
          try {
            options = await listCanonicalLandUseOptions();
          } catch {
            options = null;
          }
        }

        if (options == null) {
          const parcelListParams = district ? { district } : {};
          const [sourceResult, parcelsResult] = await Promise.allSettled([
            listSourceReportedLandUseOptions(),
            listAllParcels(parcelListParams),
          ]);
          const sourceOptions =
            sourceResult.status === "fulfilled" ? sourceResult.value : [];
          const districtParcelIds =
            parcelsResult.status === "fulfilled"
              ? new Set(parcelsResult.value.results.map((row) => row.id))
              : null;
          const scopedSupplement =
            districtParcelIds == null
              ? supplement
              : supplement.filter((row) => districtParcelIds.has(row.parcelId));
          options = buildCanonicalLandUseFallbackOptions(
            sourceOptions,
            scopedSupplement,
          );
        }

        if (!cancelled) {
          setCanonicalOptions(options);
          setLandUseOptionsError(null);
        }
      } catch (err) {
        if (!cancelled) {
          setCanonicalOptions([]);
          setLandUseOptionsError(
            err instanceof ApiClientError
              ? err.detail || err.title
              : "Unable to load land-use options for this district.",
          );
        }
      } finally {
        if (!cancelled) {
          setLandUseOptionsLoading(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [districtDraft, showCatalog, supplement]);

  useEffect(() => {
    const trimmed = initialParcelId.trim();
    if (!trimmed || loadedRouteId.current === trimmed) {
      return;
    }
    loadedRouteId.current = trimmed;
    void loadParcelDetails(trimmed, false);
  }, [initialParcelId, loadParcelDetails]);

  const landUseChoices = buildCanonicalLandUseChoices(canonicalOptions);
  const canApply = !listLoading;

  function handleDistrictChange(next: string) {
    setDistrictDraft(next);
    setLandUseDraft("");
    setFiltersApplied(false);
    setApplied(null);
    setParcels([]);
    setListError(null);
  }

  function handleClearFilters() {
    setDistrictDraft("");
    setLandUseDraft("");
    setListError(null);
    setPage(1);
    setCadastralQuery("");
    setCadastralError(null);
    setCadastralEmpty(false);
    setCadastralMatches([]);
    void loadCatalog({
      district: "",
      landUseValue: "",
      landUseLabel: "",
    });
  }

  function handleApplyFilters() {
    const landUseValue = normalizeLandUseFilterValue(landUseDraft);
    void loadCatalog({
      district: districtDraft.trim(),
      landUseValue,
      landUseLabel: landUseLabelFromValue(landUseValue),
    });
  }

  async function handleFindParcel(event?: FormEvent) {
    event?.preventDefault();
    const trimmed = cadastralQuery.trim();
    setCadastralError(null);
    setCadastralEmpty(false);
    setCadastralMatches([]);
    if (!trimmed) {
      setCadastralError("Enter a cadastral number.");
      return;
    }

    setCadastralLoading(true);
    try {
      const response = await findParcelsByCadastralNumber(trimmed);
      if (response.totalCount === 0 || response.results.length === 0) {
        setCadastralEmpty(true);
        return;
      }
      if (response.results.length === 1) {
        await loadParcelDetails(response.results[0].id);
        return;
      }
      setCadastralMatches(response.results);
    } catch (err) {
      if (err instanceof ApiClientError) {
        setCadastralError(err.detail || err.title);
      } else if (err instanceof Error) {
        setCadastralError(err.message);
      } else {
        setCadastralError("Unable to search by cadastral number.");
      }
    } finally {
      setCadastralLoading(false);
    }
  }

  const catalogTotal = parcels.length;
  const catalogTotalPages = Math.max(1, Math.ceil(catalogTotal / pageSize));
  const activePage = Math.min(page, catalogTotalPages);
  const displayedParcels = parcels.slice(
    (activePage - 1) * pageSize,
    activePage * pageSize,
  );
  const activeSummary =
    applied != null
      ? [
          applied.district.trim() ? applied.district : "All districts",
          applied.landUseLabel || null,
          `${catalogTotal} matching parcel${catalogTotal === 1 ? "" : "s"}`,
        ]
          .filter(Boolean)
          .join(" · ")
      : null;

  return (
    <>
      <PageHeading
        title="Parcel Intelligence"
        subtitle=""
      />

      {showCatalog ? (
        <form
          className={filterStyles.panel}
          aria-label="Parcel intelligence filters"
          onSubmit={(event) => void handleFindParcel(event)}
        >
          <div className={filterStyles.row}>
            <div className={filterStyles.field}>
              <label className={filterStyles.label} htmlFor="pi-district">
                District
              </label>
              <select
                id="pi-district"
                className={filterStyles.select}
                value={districtDraft}
                disabled={districtsLoading}
                onChange={(e) => handleDistrictChange(e.target.value)}
                aria-describedby="pi-district-hint"
              >
                <option value="">Select district</option>
                {indexedDistricts.map((row) => (
                  <option
                    key={`${row.province}:${row.district}`}
                    value={row.district}
                  >
                    {row.district}
                  </option>
                ))}
              </select>
              <p id="pi-district-hint" className={filterStyles.hint}>
                {districtsLoading
                  ? "Loading indexed districts…"
                  : districtsError
                    ? districtsError
                    : indexedDistricts.length === 0
                      ? "No indexed districts are available yet."
                      : ""}
              </p>
            </div>

            <div className={filterStyles.field}>
              <label className={filterStyles.label} htmlFor="pi-land-use">
                Land use
              </label>
              <select
                id="pi-land-use"
                className={filterStyles.select}
                value={normalizeLandUseFilterValue(landUseDraft)}
                disabled={landUseOptionsLoading}
                onChange={(e) => setLandUseDraft(e.target.value)}
                aria-describedby="pi-land-use-hint"
              >
                <option value="">All land uses</option>
                {landUseChoices.map((choice) => (
                  <option key={choice.value} value={choice.value}>
                    {choice.label} ({choice.count})
                  </option>
                ))}
              </select>
              <p id="pi-land-use-hint" className={filterStyles.hint}>
                {landUseOptionsLoading
                  ? "Loading land-use options…"
                  : landUseOptionsError
                    ? landUseOptionsError
                    : ""}
              </p>
            </div>

            <div className={filterStyles.field}>
              <label className={filterStyles.label} htmlFor="pi-cadastral">
                Cadastral number
              </label>
              <input
                id="pi-cadastral"
                className={filterStyles.input}
                value={cadastralQuery}
                onChange={(e) => {
                  setCadastralQuery(e.target.value);
                  setCadastralEmpty(false);
                  setCadastralError(null);
                  setCadastralMatches([]);
                }}
                placeholder="Enter cadastral number"
                autoComplete="off"
                disabled={cadastralLoading}
                aria-describedby="pi-cadastral-hint"
              />
              <p id="pi-cadastral-hint" className={filterStyles.hint}>
                {cadastralLoading
                  ? "Searching…"
                  : ""}
              </p>
            </div>

            <div className={filterStyles.actions}>
              <Button
                type="button"
                variant="primary"
                disabled={!canApply}
                onClick={handleApplyFilters}
              >
                Apply filters
              </Button>
              <Button
                type="button"
                variant="secondary"
                onClick={handleClearFilters}
              >
                Clear filters
              </Button>
            </div>
          </div>
        </form>
      ) : null}

      {showCatalog && cadastralError ? (
        <ErrorMessage message={cadastralError} />
      ) : null}
      {showCatalog && cadastralEmpty ? (
        <EmptyState
          title="No parcel was found with this cadastral number."
          description="Check the number and try again. Do not use a parcel UUID."
        />
      ) : null}
      {showCatalog && cadastralMatches.length > 1 ? (
        <div className={filterStyles.matchesList} role="region" aria-label="Matching parcels">
          <p className={filterStyles.matchesTitle}>
            {cadastralMatches.length} parcels share this cadastral number. Select
            one:
          </p>
          <ul className={filterStyles.matchesItems}>
            {cadastralMatches.map((match) => (
              <li key={match.id}>
                <button
                  type="button"
                  className={filterStyles.matchButton}
                  onClick={() => void loadParcelDetails(match.id)}
                >
                  <span>{match.cadastralNumber}</span>
                  <span className={filterStyles.matchMeta}>
                    {match.district}
                  </span>
                  <span className={filterStyles.matchMeta}>
                    {sourceReportedLandUseDisplay(match.cadastralSource) ||
                      "Source land use not recorded"}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      {showCatalog && activeSummary ? (
        <p className={filterStyles.summary} role="status">
          {activeSummary}
        </p>
      ) : null}

      {showCatalog && listLoading ? (
        <LoadingState message="Loading available parcels…" />
      ) : null}
      {showCatalog && listError ? <ErrorMessage message={listError} /> : null}

      {showCatalog &&
      !listLoading &&
      !listError &&
      filtersApplied &&
      catalogTotal === 0 ? (
        <EmptyState
          title="No parcels match these filters"
          description="Try a different land use, or clear filters and start again."
        />
      ) : null}

      {showCatalog &&
      !listLoading &&
      !listError &&
      filtersApplied &&
      catalogTotal > 0 ? (
        <div className="stack" style={{ marginBottom: "1.25rem" }}>
          <ParcelCatalog
            parcels={displayedParcels}
            totalCount={catalogTotal}
            selectedId={selectedId}
            onSelect={(id) => void loadParcelDetails(id)}
            currentPage={activePage}
            pageSize={pageSize}
            onPageChange={setPage}
            onPageSizeChange={(size) => {
              setPageSize(size);
              setPage(1);
            }}
          />
        </div>
      ) : null}

      {detailLoading ? (
        <LoadingState message="Loading parcel intelligence…" />
      ) : null}
      {detailError ? <ErrorMessage message={detailError} /> : null}
      {parcel ? (
        <>
          <div
            className="parcelFormRow"
            style={{ marginBottom: "1rem", flexWrap: "wrap", gap: "0.5rem" }}
          >
            <SaveParcelButton parcelId={parcel.id} variant="secondary" />
            <Button
              href={`/land-intelligence/parcels/${parcel.id}/lease-interest`}
              variant="primary"
            >
              Express preliminary interest
            </Button>
          </div>
          <ParcelDetails
            parcel={parcel}
            relationships={relationships}
            relationshipsError={relationshipsError}
            relationshipsLoading={relationshipsLoading}
          />
        </>
      ) : null}
    </>
  );
}
