import { LandUseType } from "../types/landIntelligence";
import type { LeaseInterestValuationFormType } from "../types/landIntelligence";

/** Paper worksheet type implied by the applicant's requested land-use purpose. */
export function valuationFormTypeForRequestedPurpose(
  requestedPurpose: number,
): LeaseInterestValuationFormType {
  return requestedPurpose === LandUseType.Residential
    ? "Residential"
    : "AgriculturalCommercial";
}
