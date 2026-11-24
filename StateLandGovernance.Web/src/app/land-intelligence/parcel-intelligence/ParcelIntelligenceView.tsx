"use client";

import { useRouter, useSearchParams } from "next/navigation";
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
import { BackToSuitableLandResultsLink } from "@/modules/component-01-land-intelligence/components/BackToSuitableLandResultsLink";
import { buildPathPreservingResultsReturn } from "@/modules/component-01-land-intelligence/utils/suitableLandResultsReturn";
import {
  ApprovedParcelsCatalog,
  type ApprovedParcelsPageSize,
} from "@/modules/component-01-land-intelligence/components/ApprovedParcelsCatalog";
import {
  ParcelCatalog,
  type ParcelCatalogPageSize,
} from "@/modules/component-01-land-intelligence/components/ParcelCatalog";
import { ParcelDetails } from "@/modules/component-01-land-intelligence/components/ParcelDetails";
import {
  listApprovedPreliminaryParcels,
  getLandIntelligenceGisCoverage,
  getParcelById,
  getParcelRelationships,
  listAllParcels,
  listCanonicalLandUseOptions,
  listResearchSupplement,
  listSourceReportedLandUseOptions,
} from "@/modules/component-01-land-intelligence/services/landIntelligenceApi";
import type {
  ApprovedPreliminaryParcelSummaryResponse,
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
import filterStyles from "./ParcelIntelligenceFilters.module.css";

type AppliedFilters = {
  district: string;
  landUseValue: string;
  landUseLabel: string;
  cadastralNumber: string;
};

type ParcelIntelligenceTab = "indexed" | "approved";

const PARCEL_INTELLIGENCE_TABS: {
  value: ParcelIntelligenceTab;
  label: string;
}[] = [
  { value: "indexed", label: "Indexed parcels" },
  { value: "approved", label: "Approved parcels" },
];

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
  const searchParams = useSearchParams();
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
  const loadedRouteId = useRef<string | null>(null);
  const initialCatalogLoadedRef = useRef(false);
  const [workspaceTab, setWorkspaceTab] =
    useState<ParcelIntelligenceTab>("indexed");
  const [approvedParcels, setApprovedParcels] = useState<
    ApprovedPreliminaryParcelSummaryResponse[]
  >([]);
  const [approvedTotal, setApprovedTotal] = useState(0);
  const [approvedPage, setApprovedPage] = useState(1);
  const [approvedPageSize, setApprovedPageSize] =
    useState<ApprovedParcelsPageSize>(20);
  const [approvedLoading, setApprovedLoading] = useState(false);
  const [approvedError, setApprovedError] = useState<string | null>(null);
  const [approvedLoaded, setApprovedLoaded] = useState(false);

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
        const cadastral = filters.cadastralNumber.trim();
        const response = await listAllParcels({
          district: filters.district,
          researchLandCoverGroup: landUseLabel || undefined,
          cadastralNumber: cadastral || undefined,
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

  const loadApprovedParcels = useCallback(
    async (page: number, size: ApprovedParcelsPageSize) => {
      if (!showCatalog) {
        return;
      }
      setApprovedLoading(true);
      setApprovedError(null);
      try {
        const response = await listApprovedPreliminaryParcels({
          page,
          pageSize: size,
        });
        setApprovedParcels(response.results);
        setApprovedTotal(response.totalCount);
        setApprovedLoaded(true);
      } catch (err) {
        if (err instanceof ApiClientError) {
          setApprovedError(err.detail || err.title);
        } else if (err instanceof Error) {
          setApprovedError(err.message);
        } else {
          setApprovedError("Unable to load approved parcels.");
        }
        setApprovedParcels([]);
        setApprovedTotal(0);
      } finally {
        setApprovedLoading(false);
      }
    },
    [showCatalog],
  );

  useEffect(() => {
    if (!showCatalog || workspaceTab !== "approved") {
      return;
    }
    void loadApprovedParcels(approvedPage, approvedPageSize);
  }, [
    showCatalog,
    workspaceTab,
    approvedPage,
    approvedPageSize,
    loadApprovedParcels,
  ]);

  useEffect(() => {
    if (!showCatalog || initialCatalogLoadedRef.current) {
      return;
    }
    initialCatalogLoadedRef.current = true;
    void loadCatalog({
      district: "",
      landUseValue: "",
      landUseLabel: "",
      cadastralNumber: "",
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
    setListError(null);
    void loadCatalog({
      district: next.trim(),
      landUseValue: "",
      landUseLabel: "",
      cadastralNumber: cadastralQuery.trim(),
    });
  }

  function handleClearFilters() {
    setDistrictDraft("");
    setLandUseDraft("");
    setListError(null);
    setPage(1);
    setCadastralQuery("");
    void loadCatalog({
      district: "",
      landUseValue: "",
      landUseLabel: "",
      cadastralNumber: "",
    });
  }

  function handleApplyFilters(event?: FormEvent) {
    event?.preventDefault();
    const landUseValue = normalizeLandUseFilterValue(landUseDraft);
    void loadCatalog({
      district: districtDraft.trim(),
      landUseValue,
      landUseLabel: landUseLabelFromValue(landUseValue),
      cadastralNumber: cadastralQuery.trim(),
    });
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
          applied.cadastralNumber.trim()
            ? `Cadastral ${applied.cadastralNumber.trim()}`
            : null,
          `${catalogTotal} matching parcel${catalogTotal === 1 ? "" : "s"}`,
        ]
          .filter(Boolean)
          .join(" · ")
      : null;

  const leaseInterestHref =
    parcel != null
      ? buildPathPreservingResultsReturn(
          `/land-intelligence/parcels/${parcel.id}/lease-interest`,
          searchParams,
        )
      : null;

  return (
    <>
      <PageHeading
        title="Parcel Intelligence"
        subtitle=""
        action={!showCatalog ? <BackToSuitableLandResultsLink /> : undefined}
      />

      {showCatalog ? (
        <div
          className={filterStyles.tabList}
          role="tablist"
          aria-label="Parcel intelligence views"
        >
          {PARCEL_INTELLIGENCE_TABS.map((tab) => {
            const selected = workspaceTab === tab.value;
            return (
              <button
                key={tab.value}
                type="button"
                role="tab"
                id={`pi-tab-${tab.value}`}
                aria-selected={selected}
                aria-controls={`pi-panel-${tab.value}`}
                className={
                  selected
                    ? `${filterStyles.tab} ${filterStyles.tabActive}`
                    : filterStyles.tab
                }
                onClick={() => setWorkspaceTab(tab.value)}
              >
                {tab.label}
              </button>
            );
          })}
        </div>
      ) : null}

      {showCatalog && workspaceTab === "approved" ? (
        <div
          id="pi-panel-approved"
          role="tabpanel"
          aria-labelledby="pi-tab-approved"
        >

          {approvedLoading ? (
            <LoadingState message="Loading approved parcels…" />
          ) : null}
          {approvedError ? <ErrorMessage message={approvedError} /> : null}
          {!approvedLoading &&
          !approvedError &&
          approvedLoaded &&
          approvedTotal === 0 ? (
            <EmptyState
              title="No approved preliminary requests yet"
              description="When an authority approves a preliminary lease interest request, the parcel will appear here. Approval does not mean a lease has been executed or land reserved."
            />
          ) : null}
          {!approvedLoading &&
          !approvedError &&
          approvedTotal > 0 ? (
            <ApprovedParcelsCatalog
              parcels={approvedParcels}
              totalCount={approvedTotal}
              currentPage={approvedPage}
              pageSize={approvedPageSize}
              onPageChange={setApprovedPage}
              onPageSizeChange={(size) => {
                setApprovedPageSize(size);
                setApprovedPage(1);
              }}
            />
          ) : null}
        </div>
      ) : null}

      {showCatalog && workspaceTab === "indexed" ? (
        <form
          className={filterStyles.panel}
          aria-label="Parcel intelligence filters"
          onSubmit={handleApplyFilters}
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
                onChange={(e) => setCadastralQuery(e.target.value)}
                placeholder="Enter cadastral number"
                autoComplete="off"
                aria-describedby="pi-cadastral-hint"
              />
              <p id="pi-cadastral-hint" className={filterStyles.hint}>
               .
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

      {showCatalog && workspaceTab === "indexed" && activeSummary ? (
        <p className={filterStyles.summary} role="status">
          {activeSummary}
        </p>
      ) : null}

      {showCatalog && workspaceTab === "indexed" && listLoading ? (
        <LoadingState message="Loading available parcels…" />
      ) : null}
      {showCatalog && workspaceTab === "indexed" && listError ? (
        <ErrorMessage message={listError} />
      ) : null}

      {showCatalog &&
      workspaceTab === "indexed" &&
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
      workspaceTab === "indexed" &&
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
            <Button
              href={leaseInterestHref ?? `/land-intelligence/parcels/${parcel.id}/lease-interest`}
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
