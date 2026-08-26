using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;

public static class LandParcelGisEnrichmentStatusEvaluator
{
    public static LandParcelGisEnrichmentOverallStatus DetermineOverallStatus(
        AdministrativeLocationVerificationResult? administrative,
        RoadAccessibilityEnrichmentResult? road,
        WaterProximityEnrichmentResult? water,
        SoilGroupEnrichmentResult? soil,
        EnvironmentalSpatialConstraintEnrichmentResult? environmental,
        IReadOnlyList<LandParcelGisEnrichmentSectionFailure> failures)
    {
        if (administrative?.Status == AdministrativeLocationVerificationStatus.Unavailable)
        {
            return LandParcelGisEnrichmentOverallStatus.Unavailable;
        }

        if (failures.Count > 0)
        {
            return LandParcelGisEnrichmentOverallStatus.Partial;
        }

        if (HasUnavailableSection(road, water, soil, environmental))
        {
            return LandParcelGisEnrichmentOverallStatus.Partial;
        }

        if (environmental?.ErosionDataStatus == ErosionDataStatus.Unavailable)
        {
            return LandParcelGisEnrichmentOverallStatus.Partial;
        }

        return LandParcelGisEnrichmentOverallStatus.Complete;
    }

    public static AdministrativeLocationGeometryBasis? ResolveGeometryBasis(
        AdministrativeLocationVerificationResult? administrative,
        RoadAccessibilityEnrichmentResult? road,
        WaterProximityEnrichmentResult? water,
        SoilGroupEnrichmentResult? soil,
        EnvironmentalSpatialConstraintEnrichmentResult? environmental)
    {
        var bases = new[]
            {
                administrative?.GeometryBasis,
                road?.GeometryBasis,
                water?.GeometryBasis,
                soil?.GeometryBasis,
                environmental?.GeometryBasis
            }
            .Where(basis => basis is not null)
            .Select(basis => basis!.Value)
            .Distinct()
            .ToList();

        return bases.Count == 1 ? bases[0] : null;
    }

    public static IReadOnlyList<string> BuildWarnings(
        AdministrativeLocationVerificationResult? administrative,
        RoadAccessibilityEnrichmentResult? road,
        WaterProximityEnrichmentResult? water,
        SoilGroupEnrichmentResult? soil,
        EnvironmentalSpatialConstraintEnrichmentResult? environmental,
        IReadOnlyList<LandParcelGisEnrichmentSectionFailure> failures)
    {
        var warnings = new List<string>();

        foreach (var failure in failures)
        {
            warnings.Add($"{failure.Section} enrichment failed: {failure.Message}");
        }

        if (administrative?.Status == AdministrativeLocationVerificationStatus.Mismatch)
        {
            warnings.Add(
                "Stored administrative location does not match GIS-detected province/district.");
        }

        if (road?.Status == RoadAccessibilityEnrichmentStatus.Unavailable)
        {
            warnings.Add("Road accessibility evidence is unavailable for this parcel.");
        }

        if (water?.Status == WaterProximityEnrichmentStatus.Unavailable)
        {
            warnings.Add("Water proximity evidence is unavailable for this parcel.");
        }

        if (soil?.Status == SoilGroupEnrichmentStatus.Unavailable)
        {
            warnings.Add("Soil group evidence is unavailable for this parcel.");
        }

        if (environmental?.Status == EnvironmentalSpatialConstraintEnrichmentStatus.Unavailable)
        {
            warnings.Add("Environmental/spatial constraint evidence is unavailable for this parcel.");
        }

        if (environmental?.ErosionDataStatus == ErosionDataStatus.Unavailable)
        {
            warnings.Add(
                "Soil erosion GIS observations are unavailable; absence of observations must not be interpreted as evidence of environmental safety.");
        }

        return warnings;
    }

    private static bool HasUnavailableSection(
        RoadAccessibilityEnrichmentResult? road,
        WaterProximityEnrichmentResult? water,
        SoilGroupEnrichmentResult? soil,
        EnvironmentalSpatialConstraintEnrichmentResult? environmental) =>
        road?.Status == RoadAccessibilityEnrichmentStatus.Unavailable
        || water?.Status == WaterProximityEnrichmentStatus.Unavailable
        || soil?.Status == SoilGroupEnrichmentStatus.Unavailable
        || environmental?.Status == EnvironmentalSpatialConstraintEnrichmentStatus.Unavailable;
}
