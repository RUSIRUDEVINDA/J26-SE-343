import type { CadastralSourceMetadataResponse } from "../types/landIntelligence";

/** Raw display value from cadastre import (no label prefix). */
export function sourceReportedLandUseDisplay(
  source?: CadastralSourceMetadataResponse | null,
): string {
  return source?.sourceReportedLandUseDisplay?.trim() ?? "";
}

/** English land-use label from cadastre codes (not raw Sinhala source text). */
export function sourceReportedLandUseEnglishDisplay(
  source?: CadastralSourceMetadataResponse | null,
): string {
  const englishLabel = source?.sourceReportedLandUseEnglishLabel?.trim();
  if (englishLabel) {
    return englishLabel;
  }
  const display = sourceReportedLandUseDisplay(source);
  if (display) {
    return display;
  }
  return source?.sourceReportedLandUseText?.trim() ?? "";
}

/** Raw display value from cadastre import (no label prefix). */
export function sourceReportedClaimantCategoryDisplay(
  source?: CadastralSourceMetadataResponse | null,
): string {
  return source?.sourceReportedClaimantCategoryDisplay?.trim() ?? "";
}

/** Full sentence for tooltips or standalone lines without a separate label. */
export function formatSourceReportedLandUseLine(
  source?: CadastralSourceMetadataResponse | null,
): string {
  const display = sourceReportedLandUseDisplay(source);
  return display ? `Source-reported land use: ${display}` : "";
}

export function formatSourceReportedClaimantCategoryLine(
  source?: CadastralSourceMetadataResponse | null,
): string {
  const display = sourceReportedClaimantCategoryDisplay(source);
  return display ? `Source-reported claimant category: ${display}` : "";
}
