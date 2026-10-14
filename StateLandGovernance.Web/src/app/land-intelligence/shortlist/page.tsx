"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { EmptyState } from "@/shared/components/EmptyState";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import { LoadingState } from "@/shared/components/LoadingState";
import { PageHeading } from "@/shared/components/PageLayout";
import {
  listShortlist,
  removeParcelFromShortlist,
} from "@/modules/component-01-land-intelligence/services/citizenWorkspaceApi";
import type { ParcelShortlistItem } from "@/modules/component-01-land-intelligence/types/landIntelligence";

export default function ShortlistPage() {
  const [items, setItems] = useState<ParcelShortlistItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    listShortlist()
      .then((rows) => {
        if (!cancelled) {
          setItems(rows);
          setError(null);
        }
      })
      .catch((err) => {
        if (!cancelled) {
          if (err instanceof ApiClientError) {
            setError(err.detail || err.title);
          } else {
            setError("Unable to load shortlist.");
          }
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  async function handleRemove(id: string) {
    await removeParcelFromShortlist(id);
    setItems((prev) => prev.filter((item) => item.landParcelId !== id));
  }

  return (
    <>
      <PageHeading
        title="Saved parcels"
        subtitle="Your shortlist for comparison and lease interest requests."
      />
      {loading ? <LoadingState message="Loading shortlist…" /> : null}
      {error ? <ErrorMessage message={error} /> : null}
      {!loading && !error && items.length === 0 ? (
        <EmptyState
          title="No saved parcels"
          description="Use Save parcel on recommendation results or parcel intelligence."
          action={
            <Button href="/land-intelligence/find-suitable-land" variant="primary">
              Find suitable land
            </Button>
          }
        />
      ) : null}
      {!loading && !error && items.length > 0 ? (
        <ul className="stack">
          {items.map((item) => (
            <li
              key={item.landParcelId}
              className="card"
              style={{ padding: "1rem", listStyle: "none" }}
            >
              <p style={{ margin: 0, fontWeight: 700 }}>
                {item.cadastralNumber ?? item.landParcelId}
              </p>
              <p style={{ margin: "0.35rem 0", fontSize: "0.85rem" }}>
                {item.district ? `District: ${item.district}` : "District unknown"}
                {item.areaHectares != null
                  ? ` · ${item.areaHectares.toFixed(2)} ha`
                  : ""}
              </p>
              <div style={{ display: "flex", flexWrap: "wrap", gap: "0.5rem" }}>
                <Button
                  href={`/land-intelligence/parcels/${item.landParcelId}`}
                  variant="secondary"
                >
                  Parcel intelligence
                </Button>
                <Button
                  href={`/land-intelligence/parcels/${item.landParcelId}/lease-interest`}
                  variant="primary"
                >
                  Express preliminary interest
                </Button>
                <Button
                  type="button"
                  variant="secondary"
                  onClick={() => void handleRemove(item.landParcelId)}
                >
                  Remove
                </Button>
              </div>
            </li>
          ))}
        </ul>
      ) : null}
      <p style={{ marginTop: "1rem", fontSize: "0.8rem" }}>
        <Link href="/land-intelligence/my-requests">View my preliminary interest requests</Link>
      </p>
    </>
  );
}
