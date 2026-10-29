import type {
  GnDivisionAdministrativeAssignmentResponse,
  ParcelGnDivisionMappingResponse,
} from "../types/landIntelligence";

export function formatGnDivisionAssignment(
  assignment: GnDivisionAdministrativeAssignmentResponse | null | undefined,
): string {
  if (!assignment) {
    return "Unavailable";
  }

  const parts = [assignment.gnName];
  if (assignment.divisionalSecretariat?.trim()) {
    parts.push(`${assignment.divisionalSecretariat.trim()} DS`);
  }
  return parts.join(", ");
}

export function formatGnDivisionPrimaryDisplay(
  mapping: ParcelGnDivisionMappingResponse,
): string {
  if (mapping.primaryAssignment) {
    return formatGnDivisionAssignment(mapping.primaryAssignment);
  }
  return "Unavailable";
}
