"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { EmptyState } from "@/shared/components/EmptyState";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import { PageHeading } from "@/shared/components/PageLayout";
import {
  getParcelById,
  getParcelRelationships,
} from "@/modules/component-01-land-intelligence/services/landIntelligenceApi";
import type {
  LandParcelResponse,
  LandRelationshipResponse,
} from "@/modules/component-01-land-intelligence/types/landIntelligence";
import { displayOrUnknown } from "@/modules/component-01-land-intelligence/utils/formatters";
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
  type: string;
};

function shortId(value: string): string {
  if (value.length <= 12) {
    return value;
  }
  return `${value.slice(0, 8)}…`;
}

function nodeKey(type: string, id: string): string {
  return `${type}:${id}`;
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

    edges.push({
      key: `${rel.relationshipType}-${sourceKey}-${targetKey}-${index}`,
      from: sourceKey,
      to: targetKey,
      type: rel.relationshipType,
    });
  });

  const uniqueNeighbors = [...new Set(neighborKeys)];
  const radius = uniqueNeighbors.length <= 4 ? 140 : 170;
  uniqueNeighbors.forEach((key, index) => {
    const angle =
      (Math.PI * 2 * index) / Math.max(uniqueNeighbors.length, 1) - Math.PI / 2;
    const node = nodes.get(key);
    if (!node) {
      return;
    }
    node.x = 320 + radius * Math.cos(angle);
    node.y = 200 + radius * Math.sin(angle);
  });

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

  return (
    <svg
      className={styles.graphSvg}
      viewBox="0 0 640 400"
      role="img"
      aria-label={`Relationship graph for parcel ${parcelLabel}. ${relationships.length} relationship${relationships.length === 1 ? "" : "s"} shown.`}
    >
      <title>{`Knowledge graph for ${parcelLabel}`}</title>
      {edges.map((edge) => {
        const from = nodes.find((n) => n.id === edge.from);
        const to = nodes.find((n) => n.id === edge.to);
        if (!from || !to) {
          return null;
        }
        const mx = (from.x + to.x) / 2;
        const my = (from.y + to.y) / 2;
        return (
          <g key={edge.key}>
            <line
              className={styles.edge}
              x1={from.x}
              y1={from.y}
              x2={to.x}
              y2={to.y}
            />
            <text className={styles.edgeLabel} x={mx} y={my - 6} textAnchor="middle">
              {edge.type}
            </text>
          </g>
        );
      })}
      {nodes.map((node) => (
        <g key={node.id} transform={`translate(${node.x}, ${node.y})`}>
          <circle
            r={node.kind === "parcel" ? 38 : 30}
            className={
              node.kind === "parcel" ? styles.nodeParcel : styles.nodeOther
            }
          />
          <text className={styles.nodeText} textAnchor="middle" dy="-2">
            {node.label.length > 14 ? `${node.label.slice(0, 12)}…` : node.label}
          </text>
          <text className={styles.nodeSubtext} textAnchor="middle" dy="12">
            {node.sublabel}
          </text>
        </g>
      ))}
    </svg>
  );
}

function isServiceUnavailable(err: unknown): boolean {
  return err instanceof ApiClientError && err.status === 503;
}

export function KnowledgeGraphView({
  initialParcelId = "",
}: {
  initialParcelId?: string;
}) {
  const [parcelId, setParcelId] = useState(initialParcelId);
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

  const loadRelationships = useCallback(async (id: string) => {
    const trimmed = id.trim();
    if (!trimmed) {
      setError("Enter a parcel UUID or open a parcel from the catalog.");
      return;
    }

    setLoading(true);
    setError(null);
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
      setParcelId(relResult.landParcelId);
      if (typeof window !== "undefined") {
        const url = new URL(window.location.href);
        url.searchParams.set("parcelId", relResult.landParcelId);
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

  useEffect(() => {
    if (initialParcelId.trim()) {
      void loadRelationships(initialParcelId);
    }
  }, [initialParcelId, loadRelationships]);

  const relationshipTypes = [
    ...new Set(relationships.map((item) => item.relationshipType)),
  ];

  return (
    <div className={styles.layout}>
      <PageHeading
        title="Land Knowledge Graph"
        subtitle="Explore relationships returned by the API for a parcel. Edges are never invented — Neo4j when configured, otherwise the PostGIS baseline if available."
      />

      <div className={styles.toolbar}>
        <label className={styles.field}>
          <span className={styles.fieldLabel}>Parcel UUID</span>
          <input
            className={styles.input}
            value={parcelId}
            onChange={(e) => setParcelId(e.target.value)}
            placeholder="Paste UUID or open from parcel details"
            aria-label="Parcel UUID"
          />
        </label>
        <div className={styles.actions}>
          <Button
            type="button"
            variant="primary"
            onClick={() => void loadRelationships(parcelId)}
          >
            Load relationships
          </Button>
          <Button href="/land-intelligence/parcels" variant="ghost">
            Browse parcels
          </Button>
        </div>
      </div>

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
            <h2 id="kg-visual-title" className={styles.panelTitle}>
              Visual explorer
            </h2>
            <p className={styles.parcelSummary}>
              Parcel <strong>{parcelLabel}</strong>
              {parcel?.location.district
                ? ` · ${parcel.location.district}`
                : null}
              {" · "}
              {relationships.length} relationship
              {relationships.length === 1 ? "" : "s"} from API
            </p>
            {relationshipTypes.length > 0 ? (
              <div className={styles.chipRow} aria-label="Relationship types">
                {relationshipTypes.map((type) => (
                  <span key={type} className={styles.chip}>
                    {type}
                  </span>
                ))}
              </div>
            ) : null}
            <RelationshipGraph
              parcelId={loadedParcelId}
              parcelLabel={parcelLabel}
              relationships={relationships}
            />
          </section>

          <section className={styles.panel} aria-labelledby="kg-list-title">
            <h2 id="kg-list-title" className={styles.panelTitle}>
              Relationship list
            </h2>
            <p className={styles.srOnly}>
              Accessible text equivalent of the visual graph.
            </p>
            <ul className={styles.list}>
              {relationships.map((item, index) => {
                const sourceIsParcel =
                  item.sourceNodeType === "LandParcel" &&
                  item.sourceNodeId === loadedParcelId;
                const targetIsParcel =
                  item.targetNodeType === "LandParcel" &&
                  item.targetNodeId === loadedParcelId;
                const sourceLabel = sourceIsParcel
                  ? parcelLabel
                  : `${item.sourceNodeType} (${shortId(item.sourceNodeId)})`;
                const targetLabel = targetIsParcel
                  ? parcelLabel
                  : `${item.targetNodeType} (${shortId(item.targetNodeId)})`;

                return (
                  <li
                    key={`${item.relationshipType}-${item.sourceNodeId}-${item.targetNodeId}-${index}`}
                    className={styles.item}
                  >
                    <p className={styles.itemType}>{item.relationshipType}</p>
                    <p className={styles.itemPath}>
                      {sourceLabel} → {targetLabel}
                    </p>
                    {item.description ? (
                      <p className={styles.itemMeta}>
                        {displayOrUnknown(item.description)}
                      </p>
                    ) : null}
                  </li>
                );
              })}
            </ul>
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
