import type {
  CadastralSourceMetadataResponse,
  LandParcelAvailabilityResponse,
  LandParcelResponse,
  SpatialConstraintResponse,
} from "../types/landIntelligence";
import { RestrictionSeverity } from "../types/landIntelligence";
import { parcelRecordedAreaHectares } from "./leaseInterestProposal";

export type LeaseInterestEligibilityBlock = {
  code: "private-claimant" | "hard-restriction" | "missing-parcel-area";
  message: string;
};

export function isSourceReportedPrivateClaimant(
  cadastralSource?: CadastralSourceMetadataResponse | null,
): boolean {
  const code = cadastralSource?.sourceReportedClaimantCategoryCode?.trim();
  return code === "3";
}

export function leaseInterestEligibilityBlock(
  parcel: {
    cadastralSource?: CadastralSourceMetadataResponse | null;
    availability?: LandParcelAvailabilityResponse | null;
  } | null,
  spatialConstraints: SpatialConstraintResponse[],
): LeaseInterestEligibilityBlock | null {
  if (!parcel) {
    return null;
  }

  if (
    parcel.availability?.projectLeaseReady?.excludedBecauseSourceReportedPrivate ||
    isSourceReportedPrivateClaimant(parcel.cadastralSource)
  ) {
    return {
      code: "private-claimant",
      message:
        "Lease interest cannot be submitted: source-reported Private claimant category. This is preliminary screening only.",
    };
  }

  const prohibitive = spatialConstraints.some(
    (c) => c.severity === RestrictionSeverity.Prohibitive,
  );
  if (prohibitive) {
    return {
      code: "hard-restriction",
      message:
        "Lease interest cannot be submitted: this parcel has recorded prohibitive spatial constraints. Hard restrictions remain in effect.",
    };
  }

  return null;
}

export function leaseInterestMissingParcelAreaBlock(
  parcel: LandParcelResponse | null,
): LeaseInterestEligibilityBlock | null {
  if (!parcel) {
    return null;
  }
  if (parcelRecordedAreaHectares(parcel) != null) {
    return null;
  }
  return {
    code: "missing-parcel-area",
    message:
      "Lease interest cannot be submitted: this parcel has no recorded area in hectares. Officer verification requires a parcel size from the cadastre record.",
  };
}
