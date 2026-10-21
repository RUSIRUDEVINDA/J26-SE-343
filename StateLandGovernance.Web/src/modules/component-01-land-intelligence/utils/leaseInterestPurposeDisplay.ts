import { LandUseType } from "../types/landIntelligence";
import type { LeaseInterestRequestStatus } from "../types/landIntelligence";
import { landUseTypeLabels } from "./enumMappings";

/**
 * Purpose label for list/status UI. Draft defaults use LandUseType.Other as a
 * placeholder — never present that as if the applicant selected "Other".
 */
export function leaseInterestPurposeDisplay(
  requestedPurpose: LandUseType,
  status: LeaseInterestRequestStatus,
): string | null {
  if (status === 1 && requestedPurpose === LandUseType.Other) {
    return null;
  }

  return landUseTypeLabels[requestedPurpose] ?? null;
}
