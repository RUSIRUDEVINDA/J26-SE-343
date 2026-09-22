"use client";

import { useState } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { EmptyState } from "@/shared/components/EmptyState";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import { PageHeading } from "@/shared/components/PageLayout";
import { getParcelRelationships } from "@/modules/component-01-land-intelligence/services/landIntelligenceApi";
import type { LandRelationshipResponse } from "@/modules/component-01-land-intelligence/types/landIntelligence";
import { displayOrUnknown } from "@/modules/component-01-land-intelligence/utils/formatters";

export function KnowledgeGraphView() {
  const [parcelId, setParcelId] = useState("");
  const [loading, setLoading] = useState(false);
  const [relationships, setRelationships] = useState<LandRelationshipResponse[]>(
    [],
  );
  const [loadedParcelId, setLoadedParcelId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleLoad() {
    const trimmed = parcelId.trim();
    if (!trimmed) {
      setError("Enter a parcel UUID.");
      return;
    }
    setLoading(true);
    setError(null);
    setRelationships([]);
    setLoadedParcelId(null);
    try {
      const result = await getParcelRelationships(trimmed);
      setLoadedParcelId(result.landParcelId);
      setRelationships(result.relationships);
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.detail || err.title);
      } else if (err instanceof Error) {
        setError(err.message);
      } else {
        setError("Unable to load knowledge graph relationships.");
      }
    } finally {
      setLoading(false);
    }
  }

  const relationshipTypes = [
    ...new Set(relationships.map((item) => item.relationshipType)),
  ];

  return (
    <>
      <PageHeading
        title="Land Knowledge Graph"
        subtitle="Neo4j view of persisted GIS intelligence (list view)."
      />

      <div className="parcelFormRow">
        <input
          className="parcelInput"
          value={parcelId}
          onChange={(e) => setParcelId(e.target.value)}
          placeholder="Parcel UUID"
          aria-label="Parcel UUID"
        />
        <Button type="button" variant="primary" onClick={handleLoad}>
          Load Relationships
        </Button>
      </div>

      {loading ? <LoadingState message="Loading relationships…" /> : null}
      {error ? <ErrorMessage message={error} /> : null}

      {loadedParcelId && relationships.length === 0 && !loading && !error ? (
        <EmptyState
          title="No relationships returned"
          description="The parcel exists but no graph relationships were returned."
        />
      ) : null}

      {relationships.length > 0 ? (
        <div className="searchSummary">
          <p style={{ margin: 0 }}>
            Parcel <strong>{loadedParcelId}</strong> · {relationships.length}{" "}
            relationship(s)
          </p>
          {relationshipTypes.length > 0 ? (
            <div className="chipRow">
              {relationshipTypes.map((type) => (
                <span key={type} className="chip">
                  {type}
                </span>
              ))}
            </div>
          ) : null}
          <ul className="relationshipList" style={{ marginTop: "1rem" }}>
            {relationships.map((item, index) => (
              <li
                key={`${item.relationshipType}-${item.sourceNodeId}-${index}`}
                className="relationshipItem"
              >
                <p style={{ margin: "0 0 0.35rem" }}>
                  <strong>{item.relationshipType}</strong>
                </p>
                <p style={{ margin: 0, fontSize: "0.875rem" }}>
                  {item.sourceNodeType} ({item.sourceNodeId}) →{" "}
                  {item.targetNodeType} ({item.targetNodeId})
                </p>
                {item.description ? (
                  <p
                    style={{
                      margin: "0.35rem 0 0",
                      color: "var(--color-muted)",
                      fontSize: "0.85rem",
                    }}
                  >
                    {displayOrUnknown(item.description)}
                  </p>
                ) : null}
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </>
  );
}
