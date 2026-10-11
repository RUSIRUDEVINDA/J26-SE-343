import type { CadastralSourceMetadataResponse } from "../types/landIntelligence";

export function formatSourceReportedLandUseLine(
  source?: CadastralSourceMetadataResponse | null,
): string {
  if (!source?.sourceReportedLandUseDisplay) {
    return "";
  }

  return `Source-reported land use: ${source.sourceReportedLandUseDisplay}`;
}

export function formatSourceReportedClaimantCategoryLine(
  source?: CadastralSourceMetadataResponse | null,
): string {
  if (!source?.sourceReportedClaimantCategoryDisplay) {
    return "";
  }

  return `Source-reported claimant category: ${source.sourceReportedClaimantCategoryDisplay}`;
}
