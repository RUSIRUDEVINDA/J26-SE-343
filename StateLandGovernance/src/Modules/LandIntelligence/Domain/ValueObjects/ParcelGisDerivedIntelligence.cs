using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Domain.ValueObjects;

public sealed record ParcelDerivedSoilGroupEvidence(
    string SoilGroupName,
    decimal? OverlapPercentage,
    AttributeProvenance? Provenance);

public sealed record ParcelGisDerivedIntelligence(
    GisEnrichmentOverallStatus EnrichmentStatus,
    ParcelDerivedSoilGroupEvidence? DerivedSoilGroup);
