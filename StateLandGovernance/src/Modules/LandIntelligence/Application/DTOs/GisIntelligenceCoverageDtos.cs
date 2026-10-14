namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public enum GisIntelligenceCoverageStatus
{
    Unavailable = 1,
    Partial = 2,
    Available = 3
}

public sealed record IndexedDistrictSummaryDto(
    string Province,
    string District,
    int ParcelCount);

public sealed record GisIntelligenceLayerCoverageDto(
    string LayerKey,
    string DisplayName,
    GisIntelligenceCoverageStatus CoverageStatus,
    int ReferenceFeatureCount,
    int ParcelsWithEvidenceCount,
    string ProvenanceSummary,
    string LimitationSummary);

public sealed record LandIntelligenceGisCoverageDto(
    int TotalParcelCount,
    IReadOnlyList<IndexedDistrictSummaryDto> IndexedDistricts,
    IReadOnlyList<string> ConfiguredEnrichmentDistricts,
    int EnrichmentSnapshotCount,
    int EnrichmentCompleteCount,
    int EnrichmentPartialCount,
    int EnrichmentUnavailableCount,
    GisIntelligenceCoverageStatus OverallEnrichmentStatus,
    string OverallEnrichmentNotice,
    IReadOnlyList<GisIntelligenceLayerCoverageDto> IntelligenceLayers,
    DateTimeOffset MeasuredAtUtc);

public sealed record ParcelGisEnrichmentSummaryDto(
    GisIntelligenceCoverageStatus OverallStatus,
    string? DetectedProvince,
    string? DetectedDistrict,
    DateTimeOffset? EnrichedAtUtc,
    string AssessmentNotice);
