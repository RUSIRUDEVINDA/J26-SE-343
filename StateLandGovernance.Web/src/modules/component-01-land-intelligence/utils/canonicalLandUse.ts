import type {
  CanonicalLandUseOptionResponse,
  ResearchSupplementParcelResponse,
  SourceReportedLandUseOptionResponse,
} from "../types/landIntelligence";

/**
 * Canonical land-use filter list for Find Suitable Land (mirrors CanonicalLandUseCatalog.cs).
 * Original cadastral labels and project GIS classification are stored separately; this list is
 * only the single user-facing filter. A canonical option may match the original label, a
 * qualifying GIS classification, or both - counts come from the API as distinct parcel ids.
 */
export const CANONICAL_LAND_USE_LABELS = [
  "Coconut",
  "Wetland",
  "Scrub / Low Jungle",
  "Paddy Field",
  "Homegarden",
  "Garden",
  "Open Waste Land",
  "Barren Land",
  "Rubber",
  "Other",
] as const;

export type CanonicalLandUseLabel = (typeof CANONICAL_LAND_USE_LABELS)[number];

/** Form values for canonical options reuse the existing `cover:<group>` -> landCover URL semantics. */
export const CANONICAL_FORM_VALUE_PREFIX = "cover:";

export type CanonicalLandUseChoice = {
  value: string;
  label: string;
  count: number;
};

const SOURCE_CODE_TO_CANONICAL: Record<string, CanonicalLandUseLabel> = {
  "75": "Coconut",
  "8": "Wetland",
  "43": "Scrub / Low Jungle",
  "73": "Paddy Field",
  "1": "Homegarden",
  "2": "Garden",
  "6": "Open Waste Land",
  "9": "Barren Land",
  "76": "Rubber",
  "13": "Other",
};

const LEGACY_GROUP_TO_CANONICAL: Record<string, CanonicalLandUseLabel> = {
  "scrub / low jungle": "Scrub / Low Jungle",
  "other state parcel": "Other",
};

export function canonicalFormValue(label: string): string {
  return `${CANONICAL_FORM_VALUE_PREFIX}${label}`;
}

/**
 * Maps older stored/URL filter values (raw source code like "75", legacy GIS group names) to the
 * canonical `cover:<label>` form value. Unknown values are returned unchanged.
 */
export function normalizeLandUseFilterValue(value: string): string {
  const trimmed = value.trim();
  if (!trimmed) {
    return "";
  }
  if (trimmed.startsWith(CANONICAL_FORM_VALUE_PREFIX)) {
    const group = trimmed.slice(CANONICAL_FORM_VALUE_PREFIX.length).trim();
    const canonical =
      CANONICAL_LAND_USE_LABELS.find(
        (label) => label.toLowerCase() === group.toLowerCase(),
      ) ?? LEGACY_GROUP_TO_CANONICAL[group.toLowerCase()];
    return canonical ? canonicalFormValue(canonical) : trimmed;
  }
  const fromCode = SOURCE_CODE_TO_CANONICAL[trimmed];
  return fromCode ? canonicalFormValue(fromCode) : trimmed;
}

/**
 * Builds the single dropdown list from API counts: canonical order, no technical grouping headings,
 * no duplicate overlapping categories, and options with no matching parcels are omitted.
 */
export function buildCanonicalLandUseChoices(
  options: readonly CanonicalLandUseOptionResponse[],
): CanonicalLandUseChoice[] {
  const byLabel = new Map(options.map((option) => [option.label, option]));
  return CANONICAL_LAND_USE_LABELS.flatMap((label) => {
    const count = byLabel.get(label)?.parcelCount ?? 0;
    return count > 0 ? [{ value: canonicalFormValue(label), label, count }] : [];
  });
}

/**
 * Compatibility fallback used when an older running API has not yet exposed the canonical
 * endpoint. Source counts are already grouped by code; supplement rows retain parcel identity,
 * allowing source/GIS overlap within the supplement to be removed from the union.
 */
export function buildCanonicalLandUseFallbackOptions(
  sourceOptions: readonly SourceReportedLandUseOptionResponse[],
  supplement: readonly ResearchSupplementParcelResponse[],
): CanonicalLandUseOptionResponse[] {
  return CANONICAL_LAND_USE_LABELS.map((label) => {
    const sourceMatchedParcelCount = sourceOptions
      .filter((option) =>
        SOURCE_CODE_TO_CANONICAL[option.code] === label
        || option.englishLabel.trim().toLowerCase() === label.toLowerCase()
        || (label === "Wetland" && option.englishLabel.trim().toLowerCase() === "marshy land")
        || (label === "Scrub / Low Jungle" && option.englishLabel.trim().toLowerCase() === "low jungle"),
      )
      .reduce((sum, option) => sum + option.parcelCount, 0);

    const gisRows = supplement.filter((row) => {
      const mapped = LEGACY_GROUP_TO_CANONICAL[row.supplementGroup.trim().toLowerCase()]
        ?? CANONICAL_LAND_USE_LABELS.find(
          (candidate) => candidate.toLowerCase() === row.supplementGroup.trim().toLowerCase(),
        );
      return mapped === label;
    });
    const gisMatchedParcelCount = new Set(gisRows.map((row) => row.parcelId)).size;
    const bothSourcesParcelCount = new Set(
      gisRows
        .filter((row) => {
          const source = CANONICAL_LAND_USE_LABELS.find(
            (candidate) => candidate.toLowerCase() === row.originalLandUseLabel.trim().toLowerCase(),
          )
            ?? (row.originalLandUseLabel.trim().toLowerCase() === "marshy land" ? "Wetland" : undefined)
            ?? (row.originalLandUseLabel.trim().toLowerCase() === "low jungle" ? "Scrub / Low Jungle" : undefined);
          return source === label;
        })
        .map((row) => row.parcelId),
    ).size;

    return {
      key: label.toLowerCase().replaceAll(" / ", "-").replaceAll(" ", "-"),
      label,
      parcelCount: sourceMatchedParcelCount + gisMatchedParcelCount - bothSourcesParcelCount,
      sourceMatchedParcelCount,
      gisMatchedParcelCount,
      bothSourcesParcelCount,
    };
  });
}
