using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public sealed record GnDivisionAdministrativeAssignmentDto(
    string GnName,
    string? GnNumber,
    string? DivisionalSecretariat);

public sealed record ParcelGnDivisionMappingDto(
    ParcelGnDivisionMappingStatus Status,
    string WorkbookStatusLabel,
    string ReviewStatusDisplay,
    string ProvenanceLabel,
    string SourceGnIndex,
    GnDivisionAdministrativeAssignmentDto? PrimaryAssignment,
    GnDivisionAdministrativeAssignmentDto? SourceFromAdministrativeCode,
    GnDivisionAdministrativeAssignmentDto? GnAtMapMarker,
    string? CodeDistrict,
    double? MapMarkerLatitude,
    double? MapMarkerLongitude,
    string DatasetKey,
    string CadastralNumber,
    DateTimeOffset ImportedAtUtc);
