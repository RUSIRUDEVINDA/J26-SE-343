using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;

public sealed record SoilGroupOverlapCandidate(
    Guid SoilGroupId,
    string SoilGroupName,
    string SourceName,
    string SourceLayer,
    double OverlapAreaSquareMeters,
    decimal OverlapPercentage);

public static class SoilGroupEnrichmentEvaluator
{
    public static bool ShouldPreserveOfficialSoilType(AttributeProvenanceSourceType? sourceType) =>
        sourceType == AttributeProvenanceSourceType.Official;

    public static SoilGroupOverlapCandidate? SelectPrimaryOverlap(
        IReadOnlyList<SoilGroupOverlapCandidate> overlaps)
    {
        if (overlaps.Count == 0)
        {
            return null;
        }

        return overlaps
            .OrderByDescending(item => item.OverlapAreaSquareMeters)
            .ThenBy(item => item.SoilGroupName, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    public static IReadOnlyList<SoilGroupOverlapEvidence> ToOverlapEvidence(
        IReadOnlyList<SoilGroupOverlapCandidate> overlaps) =>
        overlaps
            .OrderByDescending(item => item.OverlapAreaSquareMeters)
            .Select(item => new SoilGroupOverlapEvidence
            {
                SoilGroupId = item.SoilGroupId,
                SoilGroupName = item.SoilGroupName,
                OverlapAreaSquareMeters = item.OverlapAreaSquareMeters,
                OverlapPercentage = item.OverlapPercentage
            })
            .ToList();
}
