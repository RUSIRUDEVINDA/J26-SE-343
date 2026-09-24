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
import {
  getParcelById,
  getParcelConstraints,
  getParcelRelationships,
  listAllParcels,
} from "@/modules/component-01-land-intelligence/services/landIntelligenceApi";
import type {
  LandParcelResponse,
  LandRelationshipResponse,
  LandSearchResultResponse,
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

  useEffect(() => {
    if (!showCatalog) {
      return;
    }
    let cancelled = false;
    listAllParcels()
      .then((response) => {
        if (cancelled) {
          return;
        }
        setParcels(response.results);
        setTotalCount(response.totalCount);
        setListError(null);
      })
      .catch((err) => {
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
      })
      .finally(() => {
        if (!cancelled) {
          setListLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [showCatalog]);

  useEffect(() => {
    const trimmed = initialParcelId.trim();
    if (!trimmed || loadedRouteId.current === trimmed) {
      return;
    }
    loadedRouteId.current = trimmed;
    void loadParcelDetails(trimmed, false);
  }, [initialParcelId, loadParcelDetails]);

  return (
    <>
      <PageHeading
        title="Parcel Intelligence"
        subtitle="Browse indexed parcels, then open a record for attributes, constraints, and optional graph relationships."
      />

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
      {showCatalog && !listLoading && !listError && parcels.length === 0 ? (
        <EmptyState
          title="No parcels indexed"
          description="The API returned no land parcels. Ensure the backend is running and seed data is loaded."
        />
      ) : null}
      {showCatalog && !listLoading && !listError && parcels.length > 0 ? (
        <div className="stack" style={{ marginBottom: "1.25rem" }}>
          <ParcelCatalog
            parcels={parcels}
            totalCount={totalCount}
            selectedId={selectedId}
            onSelect={(id) => void loadParcelDetails(id)}
          />
        </div>
      ) : null}

      {detailLoading ? (
        <LoadingState message="Loading parcel intelligence…" />
      ) : null}
      {detailError ? <ErrorMessage message={detailError} /> : null}
      {parcel ? (
        <ParcelDetails
          parcel={parcel}
          constraints={constraints}
          relationships={relationships}
          relationshipsError={relationshipsError}
          relationshipsLoading={relationshipsLoading}
        />
      ) : null}
    </>
  );
}
