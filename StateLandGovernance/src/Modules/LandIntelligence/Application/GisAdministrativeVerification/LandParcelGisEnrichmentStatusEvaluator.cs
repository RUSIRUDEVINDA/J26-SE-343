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

        if (road?.Status is RoadAccessibilityEnrichmentStatus.Unavailable
            or RoadAccessibilityEnrichmentStatus.OutsideCoverage)
        {
            warnings.Add(
                road.Status == RoadAccessibilityEnrichmentStatus.OutsideCoverage
                    ? "Road accessibility evidence is outside configured GIS enrichment coverage."
                    : "Road accessibility evidence is unavailable for this parcel.");
        }

        if (water?.Status is WaterProximityEnrichmentStatus.Unavailable
            or WaterProximityEnrichmentStatus.OutsideCoverage)
        {
            warnings.Add(
                water.Status == WaterProximityEnrichmentStatus.OutsideCoverage
                    ? "Water proximity evidence is outside configured GIS enrichment coverage."
                    : "Water proximity evidence is unavailable for this parcel.");
        }

        if (soil?.Status is SoilGroupEnrichmentStatus.Unavailable
            or SoilGroupEnrichmentStatus.OutsideCoverage)
        {
            warnings.Add(
                soil.Status == SoilGroupEnrichmentStatus.OutsideCoverage
                    ? "Soil group evidence is outside configured GIS enrichment coverage."
                    : "Soil group evidence is unavailable for this parcel.");
        }

        if (environmental?.Status is EnvironmentalSpatialConstraintEnrichmentStatus.Unavailable
            or EnvironmentalSpatialConstraintEnrichmentStatus.OutsideCoverage)
        {
            warnings.Add(
                environmental.Status == EnvironmentalSpatialConstraintEnrichmentStatus.OutsideCoverage
                    ? "Environmental/spatial constraint evidence is outside configured GIS enrichment coverage."
                    : "Environmental/spatial constraint evidence is unavailable for this parcel.");
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
        road?.Status is RoadAccessibilityEnrichmentStatus.Unavailable
            or RoadAccessibilityEnrichmentStatus.OutsideCoverage
        || water?.Status is WaterProximityEnrichmentStatus.Unavailable
            or WaterProximityEnrichmentStatus.OutsideCoverage
        || soil?.Status is SoilGroupEnrichmentStatus.Unavailable
            or SoilGroupEnrichmentStatus.OutsideCoverage
        || environmental?.Status is EnvironmentalSpatialConstraintEnrichmentStatus.Unavailable
            or EnvironmentalSpatialConstraintEnrichmentStatus.OutsideCoverage;
}
