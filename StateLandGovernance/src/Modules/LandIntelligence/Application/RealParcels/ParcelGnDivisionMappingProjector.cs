using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.RealParcels;

public static class ParcelGnDivisionMappingProjector
{
    public const string AgreedProvenanceLabel =
        "Matched against DCS code and NSDI map marker.";

    public const string ReviewProvenanceLabel =
        "DCS administrative code and NSDI map-marker evidence disagree or are incomplete; requires administrative review.";

    public const string NeedsAdministrativeReviewDisplay = "Needs administrative review";

    public static ParcelGnDivisionMappingDto ToPublicDto(
        Colombo400GnDivisionMappingRow row,
        DateTimeOffset importedAtUtc) =>
        ToPublicDto(ToRecord(row, importedAtUtc));

    public static ParcelGnDivisionMappingDto ToPublicDto(ParcelGnDivisionMappingRecord record)
    {
        var sourceFromCode = BuildAssignment(
            record.SourceCodeGnName,
            record.SourceCodeGnNumber,
            record.SourceCodeDs);
        var gnAtMarker = BuildAssignment(
            record.MarkerGnName,
            record.MarkerGnNumber,
            record.MarkerDs);

        var agreed = record.Status == ParcelGnDivisionMappingStatus.SourceCodeAndMapMarkerAgree;
        return new ParcelGnDivisionMappingDto(
            record.Status,
            record.WorkbookStatusLabel,
            agreed ? "Matched" : NeedsAdministrativeReviewDisplay,
            agreed ? AgreedProvenanceLabel : ReviewProvenanceLabel,
            record.SourceGnIndex,
            agreed ? sourceFromCode : null,
            agreed ? null : sourceFromCode,
            agreed ? null : gnAtMarker,
            record.CodeDistrict,
            record.MarkerLatitude,
            record.MarkerLongitude,
            record.DatasetKey,
            record.CadastralNumber,
            record.ImportedAtUtc);
    }

    public static ParcelGnDivisionMappingRecord ToRecord(
        Colombo400GnDivisionMappingRow row,
        DateTimeOffset importedAtUtc) =>
        new(
            row.CadastralNumber,
            row.DatasetKey,
            row.SourceGnIndex,
            row.SourceCodeGnName,
            row.SourceCodeGnNumber,
            row.SourceCodeDs,
            row.CodeDistrict,
            row.MarkerGnName,
            row.MarkerGnNumber,
            row.MarkerDs,
            row.Status,
            row.WorkbookStatusLabel,
            row.MarkerLatitude,
            row.MarkerLongitude,
            importedAtUtc);

    public static string FormatPrimaryDisplay(ParcelGnDivisionMappingDto mapping)
    {
        if (mapping.PrimaryAssignment is null)
        {
            return string.Empty;
        }

        return FormatAssignmentLine(mapping.PrimaryAssignment);
    }

    public static string FormatAssignmentLine(GnDivisionAdministrativeAssignmentDto assignment)
    {
        var parts = new List<string> { assignment.GnName };
        if (!string.IsNullOrWhiteSpace(assignment.DivisionalSecretariat))
        {
            parts.Add($"{assignment.DivisionalSecretariat} DS");
        }

        return string.Join(", ", parts);
    }

    private static GnDivisionAdministrativeAssignmentDto? BuildAssignment(
        string? gnName,
        string? gnNumber,
        string? ds)
    {
        if (string.IsNullOrWhiteSpace(gnName)
            && string.IsNullOrWhiteSpace(gnNumber)
            && string.IsNullOrWhiteSpace(ds))
        {
            return null;
        }

        return new GnDivisionAdministrativeAssignmentDto(
            gnName ?? "Unavailable",
            string.IsNullOrWhiteSpace(gnNumber) ? null : gnNumber,
            string.IsNullOrWhiteSpace(ds) ? null : ds);
    }
}

public sealed record ParcelGnDivisionMappingRecord(
    string CadastralNumber,
    string DatasetKey,
    string SourceGnIndex,
    string? SourceCodeGnName,
    string? SourceCodeGnNumber,
    string? SourceCodeDs,
    string? CodeDistrict,
    string? MarkerGnName,
    string? MarkerGnNumber,
    string? MarkerDs,
    ParcelGnDivisionMappingStatus Status,
    string WorkbookStatusLabel,
    double? MarkerLatitude,
    double? MarkerLongitude,
    DateTimeOffset ImportedAtUtc);
