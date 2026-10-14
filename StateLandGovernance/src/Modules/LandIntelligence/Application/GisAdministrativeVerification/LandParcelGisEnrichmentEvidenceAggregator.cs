using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;

public static class LandParcelGisEnrichmentEvidenceAggregator
{
    public static IReadOnlyList<string> Aggregate(
        AdministrativeLocationVerificationResult? administrative,
        RoadAccessibilityEnrichmentResult? road,
        WaterProximityEnrichmentResult? water,
        SoilGroupEnrichmentResult? soil,
        EnvironmentalSpatialConstraintEnrichmentResult? environmental)
    {
        var evidence = new List<string>
        {
            "Unified GIS enrichment aggregates H4-H8 evidence without altering individual enrichment semantics."
        };

        AppendSectionEvidence(evidence, "Administrative", administrative?.Evidence);
        AppendSectionEvidence(evidence, "Road accessibility", road?.Evidence);
        AppendSectionEvidence(evidence, "Water proximity", water?.Evidence);
        AppendSectionEvidence(evidence, "Soil group", soil?.Evidence);
        AppendSectionEvidence(evidence, "Environmental/spatial", environmental?.Evidence);

        AppendAdministrativeSummary(evidence, administrative);
        AppendRoadSummary(evidence, road);
        AppendWaterSummary(evidence, water);
        AppendSoilSummary(evidence, soil);
        AppendEnvironmentalSummary(evidence, environmental);

        return evidence;
    }

    private static void AppendSectionEvidence(
        List<string> evidence,
        string sectionName,
        IReadOnlyList<string>? sectionEvidence)
    {
        if (sectionEvidence is null || sectionEvidence.Count == 0)
        {
            return;
        }

        evidence.Add($"{sectionName}:");
        evidence.AddRange(sectionEvidence.Select(item => $"  {item}"));
    }

    private static void AppendAdministrativeSummary(
        List<string> evidence,
        AdministrativeLocationVerificationResult? administrative)
    {
        if (administrative is null)
        {
            evidence.Add("Administrative summary: unavailable (enrichment did not complete).");
            return;
        }

        if (administrative.Status == AdministrativeLocationVerificationStatus.Unavailable)
        {
            evidence.Add("Administrative summary: unavailable.");
            return;
        }

        evidence.Add(
            $"Administrative summary: parcel spatially verified against GIS as " +
            $"{administrative.DetectedDistrict ?? "(unknown district)"} District and " +
            $"{administrative.DetectedProvince ?? "(unknown province)"} Province " +
            $"(status: {administrative.Status}).");
    }

    private static void AppendRoadSummary(
        List<string> evidence,
        RoadAccessibilityEnrichmentResult? road)
    {
        if (road is null)
        {
            evidence.Add("Road summary: unavailable (enrichment did not complete).");
            return;
        }

        if (road.Status != RoadAccessibilityEnrichmentStatus.Available)
        {
            evidence.Add("Road summary: nearest mapped road evidence is unavailable.");
            return;
        }

        evidence.Add(
            $"Road summary: nearest mapped {road.RoadType} '{road.RoadName ?? "(unnamed)"}' " +
            $"is {road.DistanceMeters:F2} m from the parcel.");
    }

    private static void AppendWaterSummary(
        List<string> evidence,
        WaterProximityEnrichmentResult? water)
    {
        if (water is null)
        {
            evidence.Add("Water summary: unavailable (enrichment did not complete).");
            return;
        }

        if (water.Status != WaterProximityEnrichmentStatus.Available)
        {
            evidence.Add("Water summary: nearest mapped water feature evidence is unavailable.");
            return;
        }

        evidence.Add(
            $"Water summary: nearest mapped {water.FeatureType} '{water.FeatureName ?? "(unnamed)"}' " +
            $"is {water.DistanceMeters:F2} m from the parcel.");
    }

    private static void AppendSoilSummary(
        List<string> evidence,
        SoilGroupEnrichmentResult? soil)
    {
        if (soil is null)
        {
            evidence.Add("Soil summary: unavailable (enrichment did not complete).");
            return;
        }

        if (soil.Status != SoilGroupEnrichmentStatus.Available)
        {
            evidence.Add("Soil summary: GIS soil group overlap evidence is unavailable.");
            return;
        }

        evidence.Add(
            $"Soil summary: parcel overlaps GIS soil group '{soil.PrimarySoilGroup}'" +
            (soil.OverlapPercentage is null ? "." : $" ({soil.OverlapPercentage:F2}% overlap)."));
    }

    private static void AppendEnvironmentalSummary(
        List<string> evidence,
        EnvironmentalSpatialConstraintEnrichmentResult? environmental)
    {
        if (environmental is null)
        {
            evidence.Add("Environmental summary: unavailable (enrichment did not complete).");
            return;
        }

        if (environmental.Status != EnvironmentalSpatialConstraintEnrichmentStatus.Available)
        {
            evidence.Add("Environmental summary: conservation/erosion GIS evidence is unavailable.");
            return;
        }

        evidence.Add(
            environmental.IntersectsSoilConservationArea
                ? $"Environmental summary: parcel intersects {environmental.ConservationAreas.Count} mapped soil conservation area(s)."
                : "Environmental summary: parcel does not intersect any mapped soil conservation areas within imported GIS coverage.");

        if (environmental.ErosionDataStatus == ErosionDataStatus.Unavailable)
        {
            evidence.Add(
                "Environmental summary: soil erosion observation evidence is unavailable; " +
                "this must not be interpreted as low erosion risk or environmental safety.");
        }
        else if (environmental.ErosionObservations.Count > 0)
        {
            evidence.Add(
                $"Environmental summary: {environmental.ErosionObservations.Count} relevant soil erosion observation(s) detected.");
        }
        else
        {
            evidence.Add(
                "Environmental summary: no relevant soil erosion observations intersect or fall within proximity of the parcel.");
        }
    }
}
