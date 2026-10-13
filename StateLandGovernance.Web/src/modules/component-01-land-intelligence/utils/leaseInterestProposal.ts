import type {
  LandParcelResponse,
  UpsertLeaseInterestDraft,
} from "../types/landIntelligence";
import { convertAreaToHectares } from "./formatters";

export function proposalWithParcelArea(
  proposal: UpsertLeaseInterestDraft["proposal"],
  parcel: LandParcelResponse,
): UpsertLeaseInterestDraft["proposal"] {
  const hectares = convertAreaToHectares(parcel.area.value, parcel.area.unit);
  if (hectares == null || hectares <= 0) {
    return proposal;
  }
  return { ...proposal, requestedAreaHectares: hectares };
}

export function parcelRecordedAreaHectares(
  parcel: LandParcelResponse | null,
): number | null {
  if (!parcel) {
    return null;
  }
  const hectares = convertAreaToHectares(parcel.area.value, parcel.area.unit);
  if (hectares == null || hectares <= 0) {
    return null;
  }
  return hectares;
}
