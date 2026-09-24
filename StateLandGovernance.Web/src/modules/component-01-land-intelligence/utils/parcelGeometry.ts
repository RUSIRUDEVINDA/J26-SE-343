import type {
  GeoJsonPolygonResponse,
  LandParcelResponse,
  SpatialReferenceResponse,
} from "../types/landIntelligence";

export type LatLngTuple = [number, number];

export type ParcelMapGeometry =
  | {
      kind: "boundary";
      /** GeoJSON geometry object (Polygon) with [lng, lat] positions. */
      geoJson: GeoJsonPolygonResponse;
      fitPositions: LatLngTuple[];
    }
  | {
      kind: "centroid";
      position: LatLngTuple;
      label: "Recorded location — parcel boundary unavailable";
    }
  | {
      kind: "unavailable";
      reason: string;
    };

const WGS84_ALIASES = new Set([
  "EPSG:4326",
  "WGS84",
  "WGS 84",
  "CRS:84",
  "CRS84",
  "URN:OGC:DEF:CRS:OGC:1.3:CRS84",
]);

export function isWgs84CoordinateSystem(
  coordinateSystem: string | null | undefined,
): boolean {
  if (coordinateSystem == null || coordinateSystem.trim() === "") {
    return false;
  }
  const normalized = coordinateSystem.trim().toUpperCase().replace(/\s+/g, " ");
  return WGS84_ALIASES.has(normalized);
}

/** Validates finite WGS84 coordinates. Zero is a valid coordinate. */
export function isValidWgs84LatLng(latitude: number, longitude: number): boolean {
  if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) {
    return false;
  }
  if (latitude < -90 || latitude > 90) {
    return false;
  }
  if (longitude < -180 || longitude > 180) {
    return false;
  }
  return true;
}

function isValidLngLatPair(position: unknown): position is [number, number] {
  if (!Array.isArray(position) || position.length < 2) {
    return false;
  }
  const longitude = position[0];
  const latitude = position[1];
  if (typeof longitude !== "number" || typeof latitude !== "number") {
    return false;
  }
  return isValidWgs84LatLng(latitude, longitude);
}

function ringHasValidPositions(ring: unknown): ring is number[][] {
  return Array.isArray(ring) && ring.length >= 3;
}

/**
 * Parses an API GeoJSON Polygon. Coordinates use [longitude, latitude].
 * Additional rings after the exterior are treated as holes.
 */
export function parseBoundaryPolygon(
  polygon: GeoJsonPolygonResponse | null | undefined,
): { geoJson: GeoJsonPolygonResponse; fitPositions: LatLngTuple[] } | null {
  if (!polygon || typeof polygon !== "object") {
    return null;
  }

  const type = (polygon.type ?? "").trim();
  if (type !== "" && type.toLowerCase() !== "polygon") {
    // MultiPolygon would need four nesting levels; this API DTO ships Polygon rings.
    return null;
  }

  const rings = polygon.coordinates;
  if (!Array.isArray(rings) || rings.length === 0) {
    return null;
  }

  const sanitizedRings: number[][][] = [];
  const fitPositions: LatLngTuple[] = [];

  for (const ring of rings) {
    if (!ringHasValidPositions(ring)) {
      continue;
    }
    const validPositions: number[][] = [];
    for (const position of ring) {
      if (!isValidLngLatPair(position)) {
        continue;
      }
      validPositions.push([position[0], position[1]]);
      fitPositions.push([position[1], position[0]]);
    }
    if (validPositions.length >= 3) {
      sanitizedRings.push(validPositions);
    }
  }

  if (sanitizedRings.length === 0 || fitPositions.length < 3) {
    return null;
  }

  return {
    geoJson: {
      type: "Polygon",
      coordinates: sanitizedRings,
    },
    fitPositions,
  };
}

export function resolveParcelMapGeometry(
  parcel: LandParcelResponse,
): ParcelMapGeometry {
  const spatial: SpatialReferenceResponse = parcel.spatial;

  if (!isWgs84CoordinateSystem(spatial.coordinateSystem)) {
    return {
      kind: "unavailable",
      reason:
        "Location unavailable — coordinate system is not WGS84 (EPSG:4326). Map display will not relabel projected coordinates.",
    };
  }

  const boundary = parseBoundaryPolygon(spatial.boundaryPolygon ?? null);
  if (boundary) {
    return {
      kind: "boundary",
      geoJson: boundary.geoJson,
      fitPositions: boundary.fitPositions,
    };
  }

  if (
    isValidWgs84LatLng(spatial.centroidLatitude, spatial.centroidLongitude)
  ) {
    return {
      kind: "centroid",
      position: [spatial.centroidLatitude, spatial.centroidLongitude],
      label: "Recorded location — parcel boundary unavailable",
    };
  }

  return {
    kind: "unavailable",
    reason: "Location unavailable — no usable parcel geometry was returned.",
  };
}

export function formatCoordinatePair(
  latitude: number,
  longitude: number,
): string {
  if (!isValidWgs84LatLng(latitude, longitude)) {
    return "Unavailable";
  }
  return `${latitude.toFixed(6)}, ${longitude.toFixed(6)}`;
}

export type BasemapConfig = {
  url: string;
  attribution: string;
  maxZoom: number;
};

/** Configurable OSM-compatible basemap (no paid API key required for local demos). */
export function getBasemapConfig(): BasemapConfig {
  return {
    url:
      process.env.NEXT_PUBLIC_MAP_TILE_URL?.trim() ||
      "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png",
    attribution:
      process.env.NEXT_PUBLIC_MAP_ATTRIBUTION?.trim() ||
      '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
    maxZoom: 19,
  };
}
