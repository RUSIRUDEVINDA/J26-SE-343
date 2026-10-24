"use client";

import { useEffect, useMemo, useState } from "react";
import {
  GeoJSON,
  MapContainer,
  Marker,
  Popup,
  TileLayer,
  useMap,
} from "react-leaflet";
import L from "leaflet";
import type { LandParcelResponse } from "../../types/landIntelligence";
import {
  getBasemapConfig,
  type LatLngTuple,
  type ParcelMapGeometry,
} from "../../utils/parcelGeometry";
import { displayOrUnavailable } from "../../utils/formatters";
import styles from "./ParcelMap.module.css";
import "leaflet/dist/leaflet.css";

const MAROON = "#850906";
const MAX_FIT_ZOOM = 16;
const FIT_PADDING: [number, number] = [36, 36];

function createMaroonPinIcon(): L.DivIcon {
  const html = `
    <svg width="28" height="40" viewBox="0 0 28 40" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
      <path fill="${MAROON}" stroke="#ffffff" stroke-width="1.5"
        d="M14 1C7.4 1 2 6.4 2 13c0 8.4 10.2 20.4 11.2 21.5a1.1 1.1 0 0 0 1.6 0C15.8 33.4 26 21.4 26 13 26 6.4 20.6 1 14 1z"/>
      <circle cx="14" cy="13" r="4.5" fill="#ffffff"/>
    </svg>`;
  return L.divIcon({
    className: styles.markerRoot,
    html,
    iconSize: [28, 40],
    iconAnchor: [14, 40],
    popupAnchor: [0, -34],
  });
}

function escapeHtml(value: string): string {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function buildPopupHtml(parcel: LandParcelResponse): string {
  const id = escapeHtml(parcel.identifier.cadastralNumber || "Unavailable");
  const district = escapeHtml(parcel.location.district || "Unavailable");
  return `
    <div style="min-width:12rem;max-width:16rem;">
      <p style="margin:0 0 0.35rem;font-weight:700;color:${MAROON};">${id}</p>
      <p style="margin:0.2rem 0;font-size:0.8rem;">District: ${district}</p>
      <a href="/land-intelligence/parcels/${encodeURIComponent(parcel.id)}" style="display:inline-block;margin-top:0.45rem;color:${MAROON};font-weight:600;font-size:0.85rem;">View parcel details</a>
    </div>`;
}

function fitMapToGeometry(
  map: L.Map,
  geometry: Extract<ParcelMapGeometry, { kind: "boundary" | "centroid" }>,
) {
  if (geometry.kind === "boundary") {
    const bounds = L.latLngBounds(geometry.fitPositions);
    if (bounds.isValid()) {
      map.fitBounds(bounds, { padding: FIT_PADDING, maxZoom: MAX_FIT_ZOOM });
    }
    return;
  }
  map.setView(geometry.position, 14, { animate: false });
}

function FitToGeometry({
  geometry,
  requestKey,
}: {
  geometry: Extract<ParcelMapGeometry, { kind: "boundary" | "centroid" }>;
  requestKey: number;
}) {
  const map = useMap();

  useEffect(() => {
    fitMapToGeometry(map, geometry);
  }, [geometry, map, requestKey]);

  return null;
}

function MapResizeSync({
  geometry,
}: {
  geometry: Extract<ParcelMapGeometry, { kind: "boundary" | "centroid" }>;
}) {
  const map = useMap();

  useEffect(() => {
    const container = map.getContainer();
    const observer = new ResizeObserver(() => {
      map.invalidateSize({ animate: false });
      fitMapToGeometry(map, geometry);
    });
    observer.observe(container);
    return () => observer.disconnect();
  }, [geometry, map]);

  return null;
}

function RecenterControl({
  geometry,
}: {
  geometry: Extract<ParcelMapGeometry, { kind: "boundary" | "centroid" }>;
}) {
  const map = useMap();

  useEffect(() => {
    const control = new L.Control({ position: "topleft" });
    control.onAdd = () => {
      const container = L.DomUtil.create(
        "div",
        `leaflet-bar ${styles.recenterControl}`,
      );
      const button = L.DomUtil.create("a", "", container) as HTMLAnchorElement;
      button.href = "#";
      button.role = "button";
      button.title = "Recenter on parcel";
      button.setAttribute("aria-label", "Recenter on parcel");
      button.textContent = "⌖";
      L.DomEvent.disableClickPropagation(button);
      L.DomEvent.on(button, "click", (event) => {
        L.DomEvent.preventDefault(event);
        fitMapToGeometry(map, geometry);
      });
      return container;
    };
    control.addTo(map);
    return () => {
      control.remove();
    };
  }, [geometry, map]);

  return null;
}

export function ParcelMap({
  parcel,
  geometry,
}: {
  parcel: LandParcelResponse;
  geometry: Extract<ParcelMapGeometry, { kind: "boundary" | "centroid" }>;
}) {
  const basemap = useMemo(() => getBasemapConfig(), []);
  const pinIcon = useMemo(() => createMaroonPinIcon(), []);
  const [tileError, setTileError] = useState(false);

  const initialCenter: LatLngTuple =
    geometry.kind === "centroid"
      ? geometry.position
      : geometry.fitPositions[0];

  const boundaryData =
    geometry.kind === "boundary"
      ? {
          type: "Feature" as const,
          properties: {},
          geometry: {
            type: "Polygon" as const,
            coordinates: geometry.geoJson.coordinates,
          },
        }
      : null;

  return (
    <div className={styles.mapShell}>
      {tileError ? (
        <p className={styles.tileWarning} role="status">
          Basemap tiles failed to load. You can still use the parcel summary and
          navigation links.
        </p>
      ) : null}
      <div className={styles.mapFrame} role="region" aria-label="Parcel map">
        <MapContainer
          center={initialCenter}
          zoom={13}
          className={styles.map}
          scrollWheelZoom
          attributionControl
        >
          <TileLayer
            url={basemap.url}
            attribution={basemap.attribution}
            maxZoom={basemap.maxZoom}
            eventHandlers={{
              tileerror: () => setTileError(true),
            }}
          />
          <FitToGeometry geometry={geometry} requestKey={0} />
          <MapResizeSync geometry={geometry} />
          <RecenterControl geometry={geometry} />

          {boundaryData ? (
            <GeoJSON
              key={`boundary-${parcel.id}`}
              data={boundaryData}
              style={{
                color: MAROON,
                weight: 3,
                fillColor: MAROON,
                fillOpacity: 0.22,
              }}
              onEachFeature={(_feature, layer) => {
                layer.bindPopup(buildPopupHtml(parcel));
              }}
            />
          ) : null}

          {geometry.kind === "centroid" ? (
            <Marker
              position={geometry.position}
              icon={pinIcon}
              title={displayOrUnavailable(parcel.identifier.cadastralNumber)}
            >
              <Popup>
                <div className={styles.popup}>
                  <p className={styles.popupTitle}>
                    {displayOrUnavailable(parcel.identifier.cadastralNumber)}
                  </p>
                  <p className={styles.popupMeta}>
                    District:{" "}
                    {displayOrUnavailable(parcel.location.district)}
                  </p>
                  <a
                    href={`/land-intelligence/parcels/${parcel.id}`}
                    className={styles.popupLink}
                  >
                    View parcel details
                  </a>
                </div>
              </Popup>
            </Marker>
          ) : null}
        </MapContainer>
      </div>
      {geometry.kind === "centroid" ? (
        <p className={styles.centroidNote}>{geometry.label}</p>
      ) : null}
    </div>
  );
}
