namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public sealed class GisReferenceDataImportResult
{
    public required IReadOnlyDictionary<string, int> TableCounts { get; init; }

    public required int FeaturesImported { get; init; }

    public required int FeaturesUpdated { get; init; }

    public required int FeaturesSkipped { get; init; }

    public required IReadOnlyList<string> SkipLog { get; init; }

    public required string HambantotaDistrictName { get; init; }

    public required string ProvinceName { get; init; }
}
