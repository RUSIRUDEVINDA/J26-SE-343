"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { EmptyState } from "@/shared/components/EmptyState";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import { PageHeading } from "@/shared/components/PageLayout";
import {
  findParcelsByCadastralNumber,
  getParcelById,
  getParcelRelationships,
} from "@/modules/component-01-land-intelligence/services/landIntelligenceApi";
import type {
  LandParcelResponse,
  LandRelationshipResponse,
  LandSearchResultResponse,
} from "@/modules/component-01-land-intelligence/types/landIntelligence";
import { sourceReportedLandUseDisplay } from "@/modules/component-01-land-intelligence/utils/sourceReportedCadastre";
import styles from "./KnowledgeGraph.module.css";

type GraphNode = {
  id: string;
  label: string;
  sublabel: string;
  kind: "parcel" | "other";
  x: number;
  y: number;
};

type GraphEdge = {
  key: string;
  from: string;
  to: string;
};

function shortId(value: string): string {
  if (value.length <= 12) {
    return value;
  }
  return `${value.slice(0, 8)}…`;
}

function formatNodeType(type: string): string {
  const words = type
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/_/g, " ")
    .trim();
  return words.charAt(0).toUpperCase() + words.slice(1).toLowerCase();
}

function nodeKey(type: string, id: string): string {
  return `${type}:${id}`;
}

function neighborLabelFromRelationship(
  rel: LandRelationshipResponse,
  parcelId: string,
  nodeType: string,
  nodeId: string,
): string | null {
  const parcelKey = nodeKey("LandParcel", parcelId);
  const key = nodeKey(nodeType, nodeId);
  if (key === parcelKey) {
    return null;
  }
  const parcelIsSource =
    rel.sourceNodeType === "LandParcel" && rel.sourceNodeId === parcelId;
  const parcelIsTarget =
    rel.targetNodeType === "LandParcel" && rel.targetNodeId === parcelId;
  if (!parcelIsSource && !parcelIsTarget) {
    return null;
  }
  const matchesNeighbor =
    (parcelIsSource &&
      rel.targetNodeType === nodeType &&
      rel.targetNodeId === nodeId) ||
    (parcelIsTarget &&
      rel.sourceNodeType === nodeType &&
      rel.sourceNodeId === nodeId);
  if (!matchesNeighbor) {
    return null;
  }
  const desc = rel.description?.trim();
  return desc || null;
}

function buildGraphLayout(
  parcelId: string,
  parcelLabel: string,
  relationships: LandRelationshipResponse[],
): { nodes: GraphNode[]; edges: GraphEdge[] } {
  const nodes = new Map<string, GraphNode>();
  const centerKey = nodeKey("LandParcel", parcelId);
  nodes.set(centerKey, {
    id: centerKey,
    label: parcelLabel,
    sublabel: "Land parcel",
    kind: "parcel",
    x: 320,
    y: 200,
  });

  const edges: GraphEdge[] = [];
  const neighborKeys: string[] = [];

  relationships.forEach((rel, index) => {
    const sourceKey = nodeKey(rel.sourceNodeType, rel.sourceNodeId);
    const targetKey = nodeKey(rel.targetNodeType, rel.targetNodeId);

    if (!nodes.has(sourceKey)) {
      nodes.set(sourceKey, {
        id: sourceKey,
        label: shortId(rel.sourceNodeId),
        sublabel: rel.sourceNodeType,
        kind: sourceKey === centerKey ? "parcel" : "other",
        x: 0,
        y: 0,
      });
      if (sourceKey !== centerKey) {
        neighborKeys.push(sourceKey);
      }
    }
    if (!nodes.has(targetKey)) {
      nodes.set(targetKey, {
        id: targetKey,
        label: shortId(rel.targetNodeId),
        sublabel: rel.targetNodeType,
        kind: targetKey === centerKey ? "parcel" : "other",
        x: 0,
        y: 0,
      });
      if (targetKey !== centerKey) {
        neighborKeys.push(targetKey);
      }
    }

    for (const { type, id } of [
      { type: rel.sourceNodeType, id: rel.sourceNodeId },
      { type: rel.targetNodeType, id: rel.targetNodeId },
    ]) {
      const key = nodeKey(type, id);
      const node = nodes.get(key);
      const humanLabel = neighborLabelFromRelationship(rel, parcelId, type, id);
      if (node && humanLabel) {
        node.label = humanLabel;
      }
    }

    edges.push({
      key: `${rel.relationshipType}-${sourceKey}-${targetKey}-${index}`,
      from: sourceKey,
      to: targetKey,
    });
  });

  const uniqueNeighbors = [...new Set(neighborKeys)];
  const center = nodes.get(centerKey);
  if (center) {
    center.x = 320;
    center.y = uniqueNeighbors.length <= 2 ? 108 : 210;
  }

  if (uniqueNeighbors.length === 1) {
    const node = nodes.get(uniqueNeighbors[0]);
    if (node) {
      node.x = 520;
      node.y = 108;
    }
  } else if (uniqueNeighbors.length === 2) {
    uniqueNeighbors.forEach((key, index) => {
      const node = nodes.get(key);
      if (!node) {
        return;
      }
      node.x = index === 0 ? 120 : 520;
      node.y = 108;
    });
  } else {
    const radius = uniqueNeighbors.length <= 4 ? 140 : 170;
    uniqueNeighbors.forEach((key, index) => {
      const angle =
        (Math.PI * 2 * index) / Math.max(uniqueNeighbors.length, 1) -
        Math.PI / 2;
      const node = nodes.get(key);
      if (!node) {
        return;
      }
      node.x = 320 + radius * Math.cos(angle);
      node.y = 210 + radius * Math.sin(angle);
    });
  }

  return { nodes: [...nodes.values()], edges };
}

function RelationshipGraph({
  parcelId,
  parcelLabel,
  relationships,
}: {
  parcelId: string;
  parcelLabel: string;
  relationships: LandRelationshipResponse[];
}) {
  const { nodes, edges } = useMemo(
    () => buildGraphLayout(parcelId, parcelLabel, relationships),
    [parcelId, parcelLabel, relationships],
  );
  const centerKey = nodeKey("LandParcel", parcelId);

  return (
    <div className={styles.relationshipCanvas}>
      <div className={styles.parcelHub}>
        <span className={styles.hubEyebrow}>LAND PARCEL</span>
        <strong>{parcelLabel}</strong>
        <span>Starting record</span>
      </div>
      <div className={styles.branchStem} aria-hidden="true" />
      <ul className={styles.relationshipBranches} aria-label={`Connections for parcel ${parcelLabel}`}>
        {edges.map((edge, index) => {
          const relatedKey = edge.from === centerKey ? edge.to : edge.from;
          const relatedNode = nodes.find((node) => node.id === relatedKey);
          const relationship = relationships[index];
          return (
            <li key={edge.key} className={styles.branchCard}>
              <span className={styles.branchType}>
                {formatNodeType(relationship.relationshipType)}
              </span>
              <strong>{relatedNode?.label ?? shortId(relatedKey)}</strong>
              <span className={styles.branchKind}>
                {relatedNode ? formatNodeType(relatedNode.sublabel) : "Connected record"}
              </span>
              <details className={styles.branchTechnical}>
                <summary>Record identifiers</summary>
                <span>From: {formatNodeType(relationship.sourceNodeType)} {relationship.sourceNodeId}</span>
                <span>To: {formatNodeType(relationship.targetNodeType)} {relationship.targetNodeId}</span>
              </details>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

function isServiceUnavailable(err: unknown): boolean {
  return err instanceof ApiClientError && err.status === 503;
}

export function KnowledgeGraphView({
  initialParcelId = "",
  initialCadastralNumber = "",
}: {
  initialParcelId?: string;
  initialCadastralNumber?: string;
}) {
  const [cadastralQuery, setCadastralQuery] = useState(
    initialCadastralNumber.trim(),
  );
  const [cadastralMatches, setCadastralMatches] = useState<
    LandSearchResultResponse[]
  >([]);
  const [loading, setLoading] = useState(false);
  const [relationships, setRelationships] = useState<LandRelationshipResponse[]>(
    [],
  );
  const [parcel, setParcel] = useState<LandParcelResponse | null>(null);
  const [loadedParcelId, setLoadedParcelId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [serviceUnavailable, setServiceUnavailable] = useState(false);

  const parcelLabel = useMemo(() => {
    if (parcel?.identifier.cadastralNumber?.trim()) {
      return parcel.identifier.cadastralNumber.trim();
    }
    if (loadedParcelId) {
      return shortId(loadedParcelId);
    }
    return "Parcel";
  }, [parcel, loadedParcelId]);

  const loadRelationshipsForParcel = useCallback(async (parcelRecordId: string) => {
    const trimmed = parcelRecordId.trim();
    if (!trimmed) {
      return;
    }

    setLoading(true);
    setError(null);
    setCadastralMatches([]);
    setRelationships([]);
    setLoadedParcelId(null);
    setParcel(null);
    setServiceUnavailable(false);

    try {
      const [relResult, parcelResult] = await Promise.all([
        getParcelRelationships(trimmed),
        getParcelById(trimmed).catch(() => null),
      ]);
      setLoadedParcelId(relResult.landParcelId);
      setRelationships(relResult.relationships);
      setParcel(parcelResult);
      const cadastral = parcelResult?.identifier.cadastralNumber?.trim();
      if (cadastral) {
        setCadastralQuery(cadastral);
      }
      if (typeof window !== "undefined") {
        const url = new URL(window.location.href);
        url.searchParams.delete("parcelId");
        if (cadastral) {
          url.searchParams.set("cadastralNumber", cadastral);
        } else {
          url.searchParams.delete("cadastralNumber");
          url.searchParams.set("parcelId", relResult.landParcelId);
        }
        window.history.replaceState({}, "", url.toString());
      }
    } catch (err) {
      if (isServiceUnavailable(err)) {
        setServiceUnavailable(true);
        setError(
          err instanceof ApiClientError
            ? err.detail || err.title
            : "Knowledge graph service is not configured.",
        );
      } else if (err instanceof ApiClientError) {
        setError(err.detail || err.title);
      } else if (err instanceof Error) {
        setError(err.message);
      } else {
        setError("Unable to load knowledge graph relationships.");
      }
    } finally {
      setLoading(false);
    }
  }, []);

  const searchByCadastral = useCallback(
    async (cadastralNumber: string) => {
      const trimmed = cadastralNumber.trim();
      if (!trimmed) {
        setError("Enter a cadastral number.");
        return;
      }

      setLoading(true);
      setError(null);
      setCadastralMatches([]);
      setServiceUnavailable(false);

      try {
        const response = await findParcelsByCadastralNumber(trimmed);
        if (response.totalCount === 0 || response.results.length === 0) {
          setRelationships([]);
          setLoadedParcelId(null);
          setParcel(null);
          setError("No parcel was found with this cadastral number.");
          return;
        }
        if (response.results.length === 1) {
          await loadRelationshipsForParcel(response.results[0].id);
          return;
        }
        setCadastralMatches(response.results);
      } catch (err) {
        if (err instanceof ApiClientError) {
          setError(err.detail || err.title);
        } else if (err instanceof Error) {
          setError(err.message);
        } else {
          setError("Unable to search by cadastral number.");
        }
      } finally {
        setLoading(false);
      }
    },
    [loadRelationshipsForParcel],
  );

  useEffect(() => {
    const cadastral = initialCadastralNumber.trim();
    const parcelId = initialParcelId.trim();
    if (cadastral) {
      void searchByCadastral(cadastral);
      return;
    }
    if (parcelId) {
      void loadRelationshipsForParcel(parcelId);
    }
  }, [
    initialCadastralNumber,
    initialParcelId,
    loadRelationshipsForParcel,
    searchByCadastral,
  ]);

  return (
    <div className={styles.layout}>
      <PageHeading
        title="Land Knowledge Graph"
        subtitle="Explore the recorded connections for a cadastral parcel."
      />

      <form
        className={styles.toolbar}
        onSubmit={(event) => {
          event.preventDefault();
          void searchByCadastral(cadastralQuery);
        }}
      >
        <label className={styles.field}>
          <span className={styles.fieldLabel}>Cadastral number</span>
          <input
            className={styles.input}
            value={cadastralQuery}
            onChange={(e) => {
              setCadastralQuery(e.target.value);
              setCadastralMatches([]);
            }}
            placeholder="Enter cadastral number"
            autoComplete="off"
            aria-label="Cadastral number"
          />
        </label>
        <div className={styles.actions}>
          <Button type="submit" variant="primary" disabled={loading}>
            Load relationships
          </Button>
          <Button href="/land-intelligence/parcels" variant="ghost">
            Browse parcels
          </Button>
        </div>
      </form>

      {cadastralMatches.length > 1 ? (
        <div className={styles.matchesList} role="region" aria-label="Matching parcels">
          <p className={styles.matchesTitle}>
            {cadastralMatches.length} parcels share this cadastral number. Select
            one:
          </p>
          <ul className={styles.matchesItems}>
            {cadastralMatches.map((match) => (
              <li key={match.id}>
                <button
                  type="button"
                  className={styles.matchButton}
                  onClick={() => void loadRelationshipsForParcel(match.id)}
                >
                  <span>{match.cadastralNumber}</span>
                  <span className={styles.matchMeta}>{match.district}</span>
                  <span className={styles.matchMeta}>
                    {sourceReportedLandUseDisplay(match.cadastralSource) ||
                      "Source land use not recorded"}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      {loading ? <LoadingState message="Loading relationships…" /> : null}
      {error && !serviceUnavailable ? <ErrorMessage message={error} /> : null}

      {serviceUnavailable ? (
        <EmptyState
          title="Knowledge graph unavailable"
          description="Neo4j is not configured or not reachable, and no baseline relationships could be returned."
          action={
            <div>
              <ol className={styles.setupList}>
                <li>
                  Set <code>NEO4J_CONNECTION</code>, <code>NEO4J_USERNAME</code>,
                  and <code>NEO4J_PASSWORD</code> for the API process.
                </li>
                <li>Restart the Land Intelligence API.</li>
                <li>
                  Sync parcel graphs (create/update parcels) and run GIS
                  enrichment sync so edges reflect persisted snapshots.
                </li>
                <li>
                  Without enrichment snapshots, baseline admin/category edges may
                  still appear; road/water/soil edges stay empty.
                </li>
              </ol>
              <p className={styles.setupList} style={{ paddingLeft: 0 }}>
                See{" "}
                <code>
                  StateLandGovernance/docs/knowledge-graph/README.md
                </code>
                .
              </p>
            </div>
          }
        />
      ) : null}

      {loadedParcelId && relationships.length === 0 && !loading && !error ? (
        <EmptyState
          title="No relationships returned"
          description={`Parcel ${parcelLabel} exists, but the API returned an empty relationship list. This is accurate when Neo4j has no synced edges and the PostGIS baseline has nothing to project.`}
          action={
            <div>
              <ol className={styles.setupList}>
                <li>Confirm the parcel appears in the catalog.</li>
                <li>
                  Ensure Neo4j sync ran after parcel import (or rely on PostGIS
                  baseline for admin/category links).
                </li>
                <li>
                  GIS-derived edges (near road/water, soil, conservation) require
                  enrichment snapshots — the research 300-parcel import has none
                  by default.
                </li>
              </ol>
              <div className={styles.actions} style={{ marginTop: "0.75rem" }}>
                <Button
                  href={`/land-intelligence/parcels/${loadedParcelId}`}
                  variant="secondary"
                >
                  Open parcel details
                </Button>
              </div>
            </div>
          }
        />
      ) : null}

      {relationships.length > 0 && loadedParcelId ? (
        <div className={styles.explorer}>
          <section className={styles.panel} aria-labelledby="kg-visual-title">
            <div className={styles.panelHeader}>
              <div>
                <p className={styles.panelEyebrow}>PARCEL NETWORK</p>
                <h2 id="kg-visual-title" className={styles.panelTitle}>
                  Parcel connections
                </h2>
              </div>
              <span className={styles.connectionCount}>
                {relationships.length} connection{relationships.length === 1 ? "" : "s"}
              </span>
            </div>
            <p className={styles.parcelSummary}>
              Parcel <strong>{parcelLabel}</strong>
              {parcel?.location.district
                ? ` · ${parcel.location.district}`
                : null}
            </p>
            <RelationshipGraph
              parcelId={loadedParcelId}
              parcelLabel={parcelLabel}
              relationships={relationships}
            />
            <p className={styles.graphNote}>
              These connections describe available records. They do not confirm
              ownership or lease approval.
            </p>
            <div className={styles.actions} style={{ marginTop: "0.85rem" }}>
              <Button
                href={`/land-intelligence/parcels/${loadedParcelId}`}
                variant="secondary"
              >
                Open parcel details
              </Button>
            </div>
          </section>
        </div>
      ) : null}
    </div>
  );
}
