import {
  isSupportedLandUseType,
  type LandUseType,
  type LeaseInterestRequestStatus,
} from "../types/landIntelligence";
import { landUseTypeLabels } from "./enumMappings";

/**
 * Purpose label for list/status UI. Drafts may still carry an unset (0) or legacy
 * placeholder purpose — never present those as if the applicant selected them.
 */
export function leaseInterestPurposeDisplay(
  requestedPurpose: LandUseType,
  status: LeaseInterestRequestStatus,
): string | null {
  if (status === 1 && !isSupportedLandUseType(requestedPurpose)) {
    return null;
  }

  return landUseTypeLabels[requestedPurpose] ?? null;
}
