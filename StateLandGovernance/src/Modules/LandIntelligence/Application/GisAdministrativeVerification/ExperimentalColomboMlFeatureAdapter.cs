using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;

/// <summary>
/// Inactive experimental adapter: builds Colombo RF feature payload from GIS enrichment evidence.
/// Does not call ml_service.py and must not replace <see cref="GisEnrichmentOverallStatus"/>.
/// </summary>
public sealed class ExperimentalColomboMlFeatureAdapter : IExperimentalColomboMlFeatureAdapter
{
    public ExperimentalColomboMlFeaturePayload BuildPayload(
        LandParcel parcel,
        LandUseType requestedPurpose,
        RoadAccessibilityEnrichmentResult? road,
        WaterProximityEnrichmentResult? water,
        SoilGroupEnrichmentResult? soil,
        EnvironmentalSpatialConstraintEnrichmentResult? environmental,
        LandParcelGisEnrichmentOverallStatus? overallStatus = null)
    {
        var gisDerived = new List<string>();
        var backendActual = new List<string>();
        var simulatedOrUnavailable = new List<string>();
        var mismatches = new List<string>();

        // Backend-actual parcel attributes (not fabricated when missing)
        backendActual.Add("requested_purpose");
        backendActual.Add("land_category");
        backendActual.Add("area_hectares");

        var areaHectares = (double)parcel.Area.Value;
        var landCategory = parcel.Category.Type.ToString();

        // Road distance — OSM motor roads only; never railway
        double? distanceToRoadM = null;
        if (road?.Status == RoadAccessibilityEnrichmentStatus.Available && road.DistanceMeters is not null)
        {
            distanceToRoadM = road.DistanceMeters;
            gisDerived.Add("distance_to_road_m");
            if (!string.Equals(
                    road.SourceLayer,
                    GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer,
                    StringComparison.OrdinalIgnoreCase))
            {
                mismatches.Add(
                    $"Road SourceLayer is '{road.SourceLayer}' but experimental schema expects " +
                    $"'{GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer}'.");
            }
        }
        else
        {
            simulatedOrUnavailable.Add("distance_to_road_m");
            if (road?.Status == RoadAccessibilityEnrichmentStatus.OutsideCoverage)
            {
                mismatches.Add("distance_to_road_m unavailable: OutsideCoverage (not zero).");
            }
            else if (road?.Status == RoadAccessibilityEnrichmentStatus.Unavailable)
            {
                mismatches.Add("distance_to_road_m unavailable: missing filtered road source data (not zero).");
            }
        }

        // Natural water (canals/lakes) — never WaterSupply
        double? distanceToWaterM = null;
        if (water?.Status == WaterProximityEnrichmentStatus.Available && water.DistanceMeters is not null)
        {
            distanceToWaterM = water.DistanceMeters;
            gisDerived.Add("distance_to_water_m");
        }
        else
        {
            simulatedOrUnavailable.Add("distance_to_water_m");
        }

        var derivedSoilGroup = "Unknown";
        if (soil?.Status == SoilGroupEnrichmentStatus.Available
            && !string.IsNullOrWhiteSpace(soil.PrimarySoilGroup))
        {
            derivedSoilGroup = soil.PrimarySoilGroup;
            gisDerived.Add("derived_soil_group");
        }
        else
        {
            simulatedOrUnavailable.Add("derived_soil_group");
        }

        bool? spatialConstraintPresent = null;
        string? abstentionReason = null;
        if (environmental?.Status == EnvironmentalSpatialConstraintEnrichmentStatus.Available)
        {
            spatialConstraintPresent = environmental.IntersectsSoilConservationArea;
            gisDerived.Add("spatial_constraint_present");
            mismatches.Add(
                "spatial_constraint_present reflects assessed soil-conservation layer intersection only; " +
                "False does not mean all environmental constraints are absent; " +
                "True is not automatically a legal prohibition.");
        }
        else
        {
            // Never coerce unavailable conservation assessment to false.
            simulatedOrUnavailable.Add("spatial_constraint_present");
            abstentionReason =
                "Experimental prediction abstained: conservation assessment unavailable " +
                "(spatial_constraint_present remains null; Unknown is not coerced to false). " +
                "handle_unknown=ignore is not treated as predictive validity.";
            mismatches.Add(abstentionReason);
        }

        // Backend-compatible candidate excludes simulated environmental_restriction_*.
        // Adapter still reports them as unavailable rather than inventing ForestReserve etc.
        string? envType = null;
        string? envSeverity = null;
        simulatedOrUnavailable.Add("environmental_restriction_type");
        simulatedOrUnavailable.Add("environmental_restriction_severity");
        mismatches.Add(
            "environmental_restriction_type/severity are excluded from the backend-compatible candidate schema; " +
            "adapter does not fabricate them. Conservation intersection is exposed via spatial_constraint_present.");

        var experimentalStatus = MapExperimentalStatus(overallStatus, road, water, soil, environmental);
        gisDerived.Add("gis_enrichment_status");

        if (distanceToRoadM is null && abstentionReason is null)
        {
            abstentionReason =
                "Experimental prediction abstained: distance_to_road_m unavailable for backend-compatible candidate.";
            mismatches.Add(abstentionReason);
        }

        mismatches.Add(
            "Experimental AssessedComplete/AssessedPartial/Unavailable must not replace domain " +
            "GisEnrichmentOverallStatus Complete/Partial/Unavailable in production persistence.");
        mismatches.Add(
            "Live HttpMlSuitabilityClient still includes Railway in distance_to_road_m selection; " +
            "experimental adapter never uses Railway.");
        mismatches.Add(
            "area_hectares / land_category / requested_purpose are backend parcel attributes here; " +
            "in the offline Colombo dataset they were simulated for sampled points.");

        return new ExperimentalColomboMlFeaturePayload
        {
            RequestedPurpose = requestedPurpose.ToString(),
            LandCategory = landCategory,
            AreaHectares = areaHectares,
            DistanceToRoadM = distanceToRoadM,
            DistanceToWaterM = distanceToWaterM,
            GisEnrichmentStatus = experimentalStatus.ToString(),
            DerivedSoilGroup = derivedSoilGroup,
            EnvironmentalRestrictionType = envType,
            EnvironmentalRestrictionSeverity = envSeverity,
            SpatialConstraintPresent = spatialConstraintPresent,
            ExperimentalPredictionSupported = abstentionReason is null,
            ExperimentalPredictionAbstentionReason = abstentionReason,
            GisDerivedFields = gisDerived,
            BackendActualFields = backendActual,
            SimulatedOrUnavailableFields = simulatedOrUnavailable,
            RemainingMismatches = mismatches,
            RoadSourceLayer = road?.SourceLayer ?? GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer,
            RoadHighwayClass = road?.HighwayClass,
            RoadOsmId = road?.OsmId,
            RoadFilterPolicyVersion = road?.FilterPolicyVersion
                ?? GisEnrichmentCoverageDefaults.OsmMotorRoadFilterPolicyVersion,
            ModelActivationEnabled = false
        };
    }

    public static ExperimentalGisAssessmentStatus MapExperimentalStatus(
        LandParcelGisEnrichmentOverallStatus? overallStatus,
        RoadAccessibilityEnrichmentResult? road,
        WaterProximityEnrichmentResult? water,
        SoilGroupEnrichmentResult? soil,
        EnvironmentalSpatialConstraintEnrichmentResult? environmental)
    {
        if (overallStatus == LandParcelGisEnrichmentOverallStatus.Unavailable
            || AllOutsideOrMissing(road, water, soil, environmental))
        {
            return ExperimentalGisAssessmentStatus.Unavailable;
        }

        if (overallStatus == LandParcelGisEnrichmentOverallStatus.Complete
            && road?.Status == RoadAccessibilityEnrichmentStatus.Available
            && water?.Status == WaterProximityEnrichmentStatus.Available
            && soil?.Status == SoilGroupEnrichmentStatus.Available
            && environmental?.Status == EnvironmentalSpatialConstraintEnrichmentStatus.Available)
        {
            return ExperimentalGisAssessmentStatus.AssessedComplete;
        }

        return ExperimentalGisAssessmentStatus.AssessedPartial;
    }

    private static bool AllOutsideOrMissing(
        RoadAccessibilityEnrichmentResult? road,
        WaterProximityEnrichmentResult? water,
        SoilGroupEnrichmentResult? soil,
        EnvironmentalSpatialConstraintEnrichmentResult? environmental)
    {
        static bool MissingRoad(RoadAccessibilityEnrichmentResult? r) =>
            r is null
            || r.Status is RoadAccessibilityEnrichmentStatus.Unavailable
                or RoadAccessibilityEnrichmentStatus.OutsideCoverage;

        static bool MissingWater(WaterProximityEnrichmentResult? w) =>
            w is null
            || w.Status is WaterProximityEnrichmentStatus.Unavailable
                or WaterProximityEnrichmentStatus.OutsideCoverage;

        static bool MissingSoil(SoilGroupEnrichmentResult? s) =>
            s is null
            || s.Status is SoilGroupEnrichmentStatus.Unavailable
                or SoilGroupEnrichmentStatus.OutsideCoverage;

        static bool MissingEnv(EnvironmentalSpatialConstraintEnrichmentResult? e) =>
            e is null
            || e.Status is EnvironmentalSpatialConstraintEnrichmentStatus.Unavailable
                or EnvironmentalSpatialConstraintEnrichmentStatus.OutsideCoverage;

        return MissingRoad(road) && MissingWater(water) && MissingSoil(soil) && MissingEnv(environmental);
    }
}
