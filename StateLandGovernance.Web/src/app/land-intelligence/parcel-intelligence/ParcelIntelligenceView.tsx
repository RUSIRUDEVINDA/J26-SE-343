"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useRef, useState } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { EmptyState } from "@/shared/components/EmptyState";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import { PageHeading } from "@/shared/components/PageLayout";
import { ParcelCatalog } from "@/modules/component-01-land-intelligence/components/ParcelCatalog";
import { ParcelDetails } from "@/modules/component-01-land-intelligence/components/ParcelDetails";
import { SaveParcelButton } from "@/modules/component-01-land-intelligence/components/SaveParcelButton";
import {
  getParcelById,
  getParcelConstraints,
  getParcelRelationships,
  listAllParcels,
  listResearchSupplement,
  listSourceReportedLandUseOptions,
} from "@/modules/component-01-land-intelligence/services/landIntelligenceApi";
import type {
  LandParcelResponse,
  LandRelationshipResponse,
  LandSearchResultResponse,
  ResearchSupplementParcelResponse,
  SourceReportedLandUseOptionResponse,
  SpatialConstraintResponse,
} from "@/modules/component-01-land-intelligence/types/landIntelligence";

export function ParcelIntelligenceView({
  initialParcelId = "",
  showCatalog = true,
}: {
  initialParcelId?: string;
  showCatalog?: boolean;
}) {
  const router = useRouter();
  const [parcelId, setParcelId] = useState(initialParcelId);
  const [listLoading, setListLoading] = useState(showCatalog);
  const [detailLoading, setDetailLoading] = useState(Boolean(initialParcelId.trim()));
  const [relationshipsLoading, setRelationshipsLoading] = useState(false);
  const [parcels, setParcels] = useState<LandSearchResultResponse[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [parcel, setParcel] = useState<LandParcelResponse | null>(null);
  const [constraints, setConstraints] = useState<SpatialConstraintResponse[]>(
    [],
  );
  const [relationships, setRelationships] = useState<
    LandRelationshipResponse[] | null
  >(null);
  const [listError, setListError] = useState<string | null>(null);
  const [districtFilter, setDistrictFilter] = useState("Colombo");
  const [sourceLandUseFilter, setSourceLandUseFilter] = useState("");
  const [sourceLandUseOptions, setSourceLandUseOptions] = useState<
    SourceReportedLandUseOptionResponse[]
  >([]);
  const [supplement, setSupplement] = useState<ResearchSupplementParcelResponse[]>([]);
  const [supplementGroupFilter, setSupplementGroupFilter] = useState("");
  const [supplementError, setSupplementError] = useState<string | null>(null);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [relationshipsError, setRelationshipsError] = useState<string | null>(
    null,
  );
  const [selectedId, setSelectedId] = useState<string | null>(
    initialParcelId.trim() || null,
  );
  const loadedRouteId = useRef<string | null>(null);

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
        setDetailError("Enter a parcel UUID.");
        return;
      }
      setDetailLoading(true);
      setDetailError(null);
      setParcel(null);
      setConstraints([]);
      setRelationships(null);
      setRelationshipsError(null);
      setSelectedId(trimmed);
      setParcelId(trimmed);
      try {
        const [parcelResult, constraintsResult] = await Promise.all([
          getParcelById(trimmed),
          getParcelConstraints(trimmed),
        ]);
        setParcel(parcelResult);
        setConstraints(constraintsResult);
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

  const loadCatalog = useCallback(async () => {
    if (!showCatalog) {
      return;
    }
    setListLoading(true);
    setListError(null);
    try {
      const response = await listAllParcels({
        district: districtFilter.trim() || undefined,
        sourceReportedLandUse: sourceLandUseFilter.trim() || undefined,
      });
      setParcels(response.results);
      setTotalCount(response.totalCount);
    } catch (err) {
      if (err instanceof ApiClientError) {
        setListError(err.detail || err.title);
      } else if (err instanceof Error) {
        setListError(err.message);
      } else {
        setListError("Unable to load the parcel catalog.");
      }
    } finally {
      setListLoading(false);
    }
  }, [districtFilter, showCatalog, sourceLandUseFilter]);

  useEffect(() => {
    if (!showCatalog) {
      return;
    }
    let cancelled = false;
    listSourceReportedLandUseOptions()
      .then((options) => {
        if (!cancelled) {
          setSourceLandUseOptions(options);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setSourceLandUseOptions([]);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [showCatalog]);

  useEffect(() => {
    if (!showCatalog) return;
    let cancelled = false;
    listResearchSupplement()
      .then((rows) => {
        if (!cancelled) {
          setSupplement(rows);
          setSupplementError(null);
        }
      })
      .catch(() => {
        if (!cancelled) setSupplementError("Project GIS categories could not be loaded. The original cadastral land-use filter remains available.");
      });
    return () => { cancelled = true; };
  }, [showCatalog]);

  useEffect(() => {
    if (!showCatalog) {
      return;
    }
    let cancelled = false;
    const run = async () => {
      try {
        const response = await listAllParcels({
          district: districtFilter.trim() || undefined,
          sourceReportedLandUse: sourceLandUseFilter.trim() || undefined,
        });
        if (cancelled) {
          return;
        }
        setParcels(response.results);
        setTotalCount(response.totalCount);
        setListError(null);
      } catch (err) {
        if (cancelled) {
          return;
        }
        if (err instanceof ApiClientError) {
          setListError(err.detail || err.title);
        } else if (err instanceof Error) {
          setListError(err.message);
        } else {
          setListError("Unable to load the parcel catalog.");
        }
      } finally {
        if (!cancelled) {
          setListLoading(false);
        }
      }
    };
    void run();
    return () => {
      cancelled = true;
    };
  }, [districtFilter, showCatalog, sourceLandUseFilter]);

  useEffect(() => {
    const trimmed = initialParcelId.trim();
    if (!trimmed || loadedRouteId.current === trimmed) {
      return;
    }
    loadedRouteId.current = trimmed;
    void loadParcelDetails(trimmed, false);
  }, [initialParcelId, loadParcelDetails]);

  const supplementByParcelId = new Map(supplement.map((row) => [row.parcelId, row]));
  const displayedParcels = supplementGroupFilter
    ? parcels.filter((item) => supplementByParcelId.get(item.id)?.supplementGroup === supplementGroupFilter)
    : parcels;
  const supplementGroups = ["Coconut", "Scrub / low jungle", "Wetland", "Other State parcel"];

  return (
    <>
      <PageHeading
        title="Parcel Intelligence"
        subtitle="Browse indexed parcels, then open a record for attributes, constraints, and optional graph relationships."
      />

      {showCatalog ? (
        <div className="parcelFormRow" style={{ marginBottom: "1rem" }}>
          <input
            className="parcelInput"
            value={districtFilter}
            onChange={(e) => setDistrictFilter(e.target.value)}
            placeholder="District (e.g. Colombo)"
            aria-label="District filter"
          />
          <select
            className="parcelInput"
            value={sourceLandUseFilter}
            onChange={(e) => setSourceLandUseFilter(e.target.value)}
            aria-label="Source-reported land use filter"
          >
            <option value="">All source-reported land uses</option>
            {sourceLandUseOptions.map((option) => (
              <option key={option.code} value={option.code}>
                {option.code} — {option.englishLabel} ({option.parcelCount})
              </option>
            ))}
          </select>
          <select
            className="parcelInput"
            value={supplementGroupFilter}
            onChange={(e) => setSupplementGroupFilter(e.target.value)}
            aria-label="Project research group filter"
            disabled={supplement.length === 0}
          >
            <option value="">All project research groups</option>
            {supplementGroups.map((group) => (
              <option key={group} value={group}>
                {group} ({supplement.filter((row) => row.supplementGroup === group).length})
              </option>
            ))}
          </select>
          <Button type="button" variant="secondary" onClick={() => void loadCatalog()}>
            Apply filters
          </Button>
        </div>
      ) : null}

      {showCatalog && supplementError ? <ErrorMessage message={supplementError} /> : null}
      {showCatalog && supplement.length > 0 ? (
        <p style={{ marginBottom: "1rem" }}>
        </p>
      ) : null}

      <div className="parcelFormRow">
        <input
          className="parcelInput"
          value={parcelId}
          onChange={(e) => setParcelId(e.target.value)}
          placeholder="Parcel UUID"
          aria-label="Parcel UUID"
        />
        <Button
          type="button"
          variant="primary"
          onClick={() => void loadParcelDetails(parcelId)}
        >
          Load Parcel
        </Button>
      </div>

      {showCatalog && listLoading ? (
        <LoadingState message="Loading available parcels…" />
      ) : null}
      {showCatalog && listError ? <ErrorMessage message={listError} /> : null}
      {showCatalog && !listLoading && !listError && displayedParcels.length === 0 ? (
        <EmptyState
          title={supplementGroupFilter ? "No parcels match these filters" : "No parcels indexed"}
          description={supplementGroupFilter
            ? "Try a different project research group or source land-use filter."
            : "The API returned no land parcels. Check that the Colombo research import has been applied."}
        />
      ) : null}
      {showCatalog && !listLoading && !listError && displayedParcels.length > 0 ? (
        <div className="stack" style={{ marginBottom: "1.25rem" }}>
          <ParcelCatalog
            parcels={displayedParcels}
            totalCount={supplementGroupFilter ? displayedParcels.length : totalCount}
            selectedId={selectedId}
            onSelect={(id) => void loadParcelDetails(id)}
            supplementByParcelId={supplementByParcelId}
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
            <Button href="/land-intelligence/shortlist" variant="secondary">
              Open shortlist
            </Button>
          </div>
          <ParcelDetails
            parcel={parcel}
            constraints={constraints}
            relationships={relationships}
            relationshipsError={relationshipsError}
            relationshipsLoading={relationshipsLoading}
          />
        </>
      ) : null}
    </>
  );
}
