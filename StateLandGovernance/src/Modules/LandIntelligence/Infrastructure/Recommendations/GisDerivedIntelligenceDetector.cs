using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;

internal static class GisDerivedIntelligenceDetector
{
    public static bool IsGisDerivedInfrastructure(InfrastructureFeature feature) =>
        feature.DistanceProvenance?.SourceName == GisDerivedIntelligenceOwnership.SourceName;

    public static bool IsGisDerivedNaturalWater(InfrastructureFeature feature) =>
        feature.Type == InfrastructureFeatureType.Other && IsGisDerivedInfrastructure(feature);

    public static bool IsGisDerivedEnvironmentalRestriction(EnvironmentalRestriction restriction) =>
        restriction.DataProvenance?.SourceName == GisDerivedIntelligenceOwnership.SourceName;
}
