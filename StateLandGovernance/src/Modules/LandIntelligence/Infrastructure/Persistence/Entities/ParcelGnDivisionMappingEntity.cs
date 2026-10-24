using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

public sealed class ParcelGnDivisionMappingEntity
{
    public string CadastralNumber { get; set; } = string.Empty;

    public Guid? LandParcelId { get; set; }

    public string DatasetKey { get; set; } = string.Empty;

    public string SourceGnIndex { get; set; } = string.Empty;

    public ParcelGnDivisionMappingStatus Status { get; set; }

    public string WorkbookStatusLabel { get; set; } = string.Empty;

    public string? SourceCodeGnName { get; set; }

    public string? SourceCodeGnNumber { get; set; }

    public string? SourceCodeDs { get; set; }

    public string? CodeDistrict { get; set; }

    public string? MarkerGnName { get; set; }

    public string? MarkerGnNumber { get; set; }

    public string? MarkerDs { get; set; }

    public double? MarkerLatitude { get; set; }

    public double? MarkerLongitude { get; set; }

    public string WorkbookSha256 { get; set; } = string.Empty;

    public DateTimeOffset ImportedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
