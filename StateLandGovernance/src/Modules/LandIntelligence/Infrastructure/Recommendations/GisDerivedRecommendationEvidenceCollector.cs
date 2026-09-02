using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;

/// <summary>
/// Collects supplementary GIS-derived evidence for recommendation explanations.
/// Natural-water proximity is explanation-only and does not affect scoring weights.
/// </summary>
internal static class GisDerivedRecommendationEvidenceCollector
{
    public static IReadOnlyList<string> CollectSupplementarySummaries(LandParcel parcel)
    {
        if (parcel.GisDerivedIntelligence?.EnrichmentStatus == GisEnrichmentOverallStatus.Unavailable)
        {
            return [];
        }

        var summaries = new List<string>();

        var gisWater = parcel.InfrastructureFeatures
            .FirstOrDefault(GisDerivedIntelligenceDetector.IsGisDerivedNaturalWater);
        if (gisWater?.DistanceMeters is not null)
        {
            summaries.Add(
                $"Nearest mapped water feature ({gisWater.Name}) is {FormatDistanceKm(gisWater.DistanceMeters.Value)} from the parcel.");
        }

        if (parcel.GisDerivedIntelligence?.DerivedSoilGroup is { } derivedSoil)
        {
            var overlapText = derivedSoil.OverlapPercentage is null
                ? string.Empty
                : $" ({derivedSoil.OverlapPercentage:F1}% overlap)";
            summaries.Add($"GIS-derived soil group: {derivedSoil.SoilGroupName}{overlapText}.");
        }

        if (parcel.GisDerivedIntelligence?.EnrichmentStatus == GisEnrichmentOverallStatus.Partial)
        {
            summaries.Add("Soil erosion observations are unavailable for the current pilot area.");
        }

        return summaries;
    }

    public static IReadOnlyList<RecommendationEvidenceDto> CollectEvidence(LandParcel parcel)
    {
        var summaries = CollectSupplementarySummaries(parcel);
        if (summaries.Count == 0)
        {
            return [];
        }

        return summaries
            .Select(summary => new RecommendationEvidenceDto(
                "Derived GIS data",
                summary,
                "GIS-Derived Intelligence",
                null))
            .ToList();
    }

    private static string FormatDistanceKm(decimal distanceMeters) =>
        distanceMeters >= 1000m
            ? $"{distanceMeters / 1000m:F1} km"
            : $"{distanceMeters:F0} m";
}
