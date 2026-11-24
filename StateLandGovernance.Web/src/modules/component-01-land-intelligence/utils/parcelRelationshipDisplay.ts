import {
  LandCategoryType,
  type LandParcelResponse,
  type LandRelationshipResponse,
} from "../types/landIntelligence";
import { landCategoryTypeLabels, landUseTypeLabels } from "./enumMappings";
import {
  sourceReportedClaimantCategoryDisplay,
  sourceReportedLandUseDisplay,
  sourceReportedLandUseEnglishDisplay,
} from "./sourceReportedCadastre";

function readableType(value: string): string {
  const words = value
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/_/g, " ")
    .trim();
  return words.charAt(0).toUpperCase() + words.slice(1).toLowerCase();
}

export function landCategoryConnectionLabel(raw: string): string {
  const enumValue = LandCategoryType[raw as keyof typeof LandCategoryType];
  if (typeof enumValue === "number") {
    return landCategoryTypeLabels[enumValue as LandCategoryType] ?? raw;
  }
  return readableType(raw);
}

/** Land-use label aligned with parcel details (cadastre first, then indexed use). */
export function parcelLandUseDisplay(
  parcel: LandParcelResponse | null,
): string {
  if (!parcel) {
    return "";
  }
  if (parcel.cadastralSource) {
    const fromCadastre = sourceReportedLandUseDisplay(parcel.cadastralSource);
    if (fromCadastre) {
      return fromCadastre;
    }
  }
  if (parcel.currentUse) {
    const fromUse =
      landUseTypeLabels[parcel.currentUse.type] ??
      parcel.currentUse.description?.trim();
    if (fromUse) {
      return fromUse;
    }
  }
  if (parcel.cadastralSource) {
    return sourceReportedLandUseEnglishDisplay(parcel.cadastralSource);
  }
  return "";
}

/**
 * HAS_CATEGORY in the graph is shown as land use (not governance land category).
 */
export function parcelConnectionDescription(
  item: LandRelationshipResponse,
  parcel: LandParcelResponse | null,
): string {
  const raw = item.description?.trim() ?? "";
  if (item.relationshipType === "HAS_CATEGORY") {
    const landUse = parcelLandUseDisplay(parcel);
    if (landUse) {
      return landUse;
    }

    const categoryLabel = landCategoryConnectionLabel(raw);
    if (
      parcel?.cadastralSource &&
      (raw === "Other" || categoryLabel === "Other")
    ) {
      const claimant = sourceReportedClaimantCategoryDisplay(
        parcel.cadastralSource,
      );
      if (claimant) {
        return claimant;
      }
    }

    if (
      parcel &&
      parcel.category.type !== LandCategoryType.Other
    ) {
      const fromParcel =
        landCategoryTypeLabels[parcel.category.type] ??
        parcel.category.description?.trim();
      if (fromParcel) {
        return fromParcel;
      }
    }

    return categoryLabel;
  }

  return raw || "No descriptive label recorded";
}
