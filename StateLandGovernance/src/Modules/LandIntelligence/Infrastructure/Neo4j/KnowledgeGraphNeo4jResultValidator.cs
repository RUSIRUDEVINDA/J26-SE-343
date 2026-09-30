using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Models;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Neo4j;

internal static class KnowledgeGraphNeo4jResultValidator
{
    public static bool HasGisIntelligenceConflict(
        LandParcelGisGraphIntelligenceDto baseline,
        LandParcelGisGraphIntelligenceDto neo4j,
        out string reason)
    {
        if (HasAdministrativeConflict(baseline, neo4j, out reason))
        {
            return true;
        }

        if (HasReferenceConflict(
                baseline.ProvinceReferenceId,
                neo4j.ProvinceReferenceId,
                "province",
                out reason))
        {
            return true;
        }

        if (HasReferenceConflict(
                baseline.DistrictReferenceId,
                neo4j.DistrictReferenceId,
                "district",
                out reason))
        {
            return true;
        }

        if (HasReferenceConflict(
                baseline.NearestRoad?.RoadReferenceId,
                neo4j.NearestRoad?.RoadReferenceId,
                "road",
                out reason))
        {
            return true;
        }

        if (HasReferenceConflict(
                baseline.NearestWater?.WaterReferenceId,
                neo4j.NearestWater?.WaterReferenceId,
                "water feature",
                out reason))
        {
            return true;
        }

        if (HasReferenceConflict(
                baseline.DerivedSoil?.SoilGroupReferenceId,
                neo4j.DerivedSoil?.SoilGroupReferenceId,
                "derived soil group",
                out reason))
        {
            return true;
        }

        if (HasConservationConflict(baseline.ConservationAreas, neo4j.ConservationAreas, out reason))
        {
            return true;
        }

        reason = string.Empty;
        return false;
    }

    public static bool HasRelationshipConflict(
        IReadOnlyList<LandRelationshipDto> baseline,
        IReadOnlyList<LandRelationshipDto> neo4j,
        out string reason)
    {
        if (HasLocatedInConflict(baseline, neo4j, out reason))
        {
            return true;
        }

        if (HasRelationshipReferenceConflict(
                baseline,
                neo4j,
                GraphRelationshipTypes.NearRoad,
                "Road",
                out reason))
        {
            return true;
        }

        if (HasRelationshipReferenceConflict(
                baseline,
                neo4j,
                GraphRelationshipTypes.NearWater,
                "WaterFeature",
                out reason))
        {
            return true;
        }

        if (HasRelationshipReferenceConflict(
                baseline,
                neo4j,
                GraphRelationshipTypes.HasDerivedSoil,
                "SoilGroup",
                out reason))
        {
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool HasAdministrativeConflict(
        LandParcelGisGraphIntelligenceDto baseline,
        LandParcelGisGraphIntelligenceDto neo4j,
        out string reason)
    {
        if (!string.IsNullOrWhiteSpace(baseline.DetectedProvince)
            && !string.IsNullOrWhiteSpace(neo4j.DetectedProvince)
            && !string.Equals(baseline.DetectedProvince, neo4j.DetectedProvince, StringComparison.OrdinalIgnoreCase))
        {
            reason =
                $"LOCATED_IN province mismatch: Neo4j '{neo4j.DetectedProvince}' conflicts with PostGIS '{baseline.DetectedProvince}'.";
            return true;
        }

        if (!string.IsNullOrWhiteSpace(baseline.DetectedDistrict)
            && !string.IsNullOrWhiteSpace(neo4j.DetectedDistrict)
            && !string.Equals(baseline.DetectedDistrict, neo4j.DetectedDistrict, StringComparison.OrdinalIgnoreCase))
        {
            reason =
                $"LOCATED_IN district mismatch: Neo4j '{neo4j.DetectedDistrict}' conflicts with PostGIS '{baseline.DetectedDistrict}'.";
            return true;
        }

        if (string.IsNullOrWhiteSpace(baseline.DetectedProvince)
            && !string.IsNullOrWhiteSpace(neo4j.DetectedProvince))
        {
            reason =
                $"Neo4j reports LOCATED_IN province '{neo4j.DetectedProvince}' but PostGIS has no GIS administrative baseline.";
            return true;
        }

        if (string.IsNullOrWhiteSpace(baseline.DetectedDistrict)
            && !string.IsNullOrWhiteSpace(neo4j.DetectedDistrict))
        {
            reason =
                $"Neo4j reports LOCATED_IN district '{neo4j.DetectedDistrict}' but PostGIS has no GIS administrative baseline.";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool HasLocatedInConflict(
        IReadOnlyList<LandRelationshipDto> baseline,
        IReadOnlyList<LandRelationshipDto> neo4j,
        out string reason)
    {
        foreach (var targetNodeType in new[] { "Province", "District" })
        {
            var baselineTargetId = FindRelationshipTargetId(baseline, GraphRelationshipTypes.LocatedIn, targetNodeType);
            var neo4jTargetId = FindRelationshipTargetId(neo4j, GraphRelationshipTypes.LocatedIn, targetNodeType);

            if (HasReferenceConflict(baselineTargetId, neo4jTargetId, targetNodeType.ToLowerInvariant(), out reason))
            {
                reason = $"LOCATED_IN {reason}";
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }

    private static bool HasRelationshipReferenceConflict(
        IReadOnlyList<LandRelationshipDto> baseline,
        IReadOnlyList<LandRelationshipDto> neo4j,
        string relationshipType,
        string targetNodeType,
        out string reason)
    {
        var baselineTargetId = FindRelationshipTargetId(baseline, relationshipType, targetNodeType);
        var neo4jTargetId = FindRelationshipTargetId(neo4j, relationshipType, targetNodeType);

        if (HasReferenceConflict(baselineTargetId, neo4jTargetId, targetNodeType, out reason))
        {
            reason = $"{relationshipType} {reason}";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool HasReferenceConflict(
        Guid? baselineReferenceId,
        Guid? neo4jReferenceId,
        string featureName,
        out string reason)
    {
        if (baselineReferenceId.HasValue
            && neo4jReferenceId.HasValue
            && baselineReferenceId.Value != neo4jReferenceId.Value)
        {
            reason =
                $"{featureName} reference mismatch: Neo4j '{neo4jReferenceId}' conflicts with PostGIS '{baselineReferenceId}'.";
            return true;
        }

        if (!baselineReferenceId.HasValue && neo4jReferenceId.HasValue)
        {
            reason =
                $"{featureName} reference '{neo4jReferenceId}' exists in Neo4j but PostGIS has no corresponding baseline.";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool HasConservationConflict(
        IReadOnlyList<GisDerivedConservationGraphSync> baseline,
        IReadOnlyList<GisDerivedConservationGraphSync> neo4j,
        out string reason)
    {
        var baselineIds = baseline
            .Select(item => item.ConservationAreaReferenceId)
            .OrderBy(id => id)
            .ToList();
        var neo4jIds = neo4j
            .Select(item => item.ConservationAreaReferenceId)
            .OrderBy(id => id)
            .ToList();

        if (baselineIds.SequenceEqual(neo4jIds))
        {
            reason = string.Empty;
            return false;
        }

        reason =
            "Conservation-area relationships differ between Neo4j and PostGIS baseline.";
        return true;
    }

    private static Guid? FindRelationshipTargetId(
        IReadOnlyList<LandRelationshipDto> relationships,
        string relationshipType,
        string targetNodeType)
    {
        var relationship = relationships.FirstOrDefault(item =>
            string.Equals(item.RelationshipType, relationshipType, StringComparison.Ordinal)
            && string.Equals(item.TargetNodeType, targetNodeType, StringComparison.Ordinal));

        return relationship is null || !Guid.TryParse(relationship.TargetNodeId, out var parsed)
            ? null
            : parsed;
    }
}
