namespace StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Cypher;

internal static class GisKnowledgeGraphCypher
{
    public const string ClearGisDerivedParcelRelationships = """
        MATCH (p:LandParcel { id: $parcelId })-[r]->()
        WHERE r.source = $ownershipSource
        DELETE r
        """;

    public const string UpsertProvince = """
        MERGE (n:Province { id: $id })
        SET n.name = $name,
            n.sourceName = $sourceName,
            n.updatedAt = datetime()
        RETURN n
        """;

    public const string UpsertDistrict = """
        MERGE (n:District { id: $id })
        SET n.name = $name,
            n.sourceName = $sourceName,
            n.updatedAt = datetime()
        RETURN n
        """;

    public const string UpsertRoad = """
        MERGE (n:Road { id: $id })
        SET n.name = $name,
            n.roadType = $roadType,
            n.sourceName = $sourceName,
            n.updatedAt = datetime()
        RETURN n
        """;

    public const string UpsertWaterFeature = """
        MERGE (n:WaterFeature { id: $id })
        SET n.name = $name,
            n.featureType = $featureType,
            n.sourceName = $sourceName,
            n.updatedAt = datetime()
        RETURN n
        """;

    public const string UpsertSoilGroup = """
        MERGE (n:SoilGroup { id: $id })
        SET n.name = $name,
            n.sourceName = $sourceName,
            n.updatedAt = datetime()
        RETURN n
        """;

    public const string UpsertConservationArea = """
        MERGE (n:ConservationArea { id: $id })
        SET n.name = $name,
            n.sourceName = $sourceName,
            n.updatedAt = datetime()
        RETURN n
        """;

    public const string LinkParcelToProvince = """
        MATCH (p:LandParcel { id: $parcelId })
        MATCH (n:Province { id: $targetId })
        MERGE (p)-[r:LOCATED_IN]->(n)
        SET r.source = $ownershipSource,
            r.derivedAt = datetime($derivedAt),
            r.updatedAt = datetime()
        RETURN r
        """;

    public const string LinkParcelToDistrict = """
        MATCH (p:LandParcel { id: $parcelId })
        MATCH (n:District { id: $targetId })
        MERGE (p)-[r:LOCATED_IN]->(n)
        SET r.source = $ownershipSource,
            r.derivedAt = datetime($derivedAt),
            r.updatedAt = datetime()
        RETURN r
        """;

    public const string LinkParcelToRoad = """
        MATCH (p:LandParcel { id: $parcelId })
        MATCH (n:Road { id: $targetId })
        MERGE (p)-[r:NEAR_ROAD]->(n)
        SET r.distanceMeters = $distanceMeters,
            r.source = $ownershipSource,
            r.derivedAt = datetime($derivedAt),
            r.updatedAt = datetime()
        RETURN r
        """;

    public const string LinkParcelToWaterFeature = """
        MATCH (p:LandParcel { id: $parcelId })
        MATCH (n:WaterFeature { id: $targetId })
        MERGE (p)-[r:NEAR_WATER]->(n)
        SET r.distanceMeters = $distanceMeters,
            r.featureType = $featureType,
            r.source = $ownershipSource,
            r.derivedAt = datetime($derivedAt),
            r.updatedAt = datetime()
        RETURN r
        """;

    public const string LinkParcelToSoilGroup = """
        MATCH (p:LandParcel { id: $parcelId })
        MATCH (n:SoilGroup { id: $targetId })
        MERGE (p)-[r:HAS_DERIVED_SOIL]->(n)
        SET r.overlapPercentage = $overlapPercentage,
            r.source = $ownershipSource,
            r.derivedAt = datetime($derivedAt),
            r.updatedAt = datetime()
        RETURN r
        """;

    public const string LinkParcelToConservationArea = """
        MATCH (p:LandParcel { id: $parcelId })
        MATCH (n:ConservationArea { id: $targetId })
        MERGE (p)-[r:INTERSECTS_CONSERVATION_AREA]->(n)
        SET r.overlapPercentage = $overlapPercentage,
            r.source = $ownershipSource,
            r.derivedAt = datetime($derivedAt),
            r.updatedAt = datetime()
        RETURN r
        """;

    public const string GetParcelGisGraphIntelligence = """
        MATCH (p:LandParcel { id: $parcelId })
        OPTIONAL MATCH (p)-[rp:LOCATED_IN]->(prov:Province)
        WHERE rp.source = $ownershipSource
        OPTIONAL MATCH (p)-[rd:LOCATED_IN]->(dist:District)
        WHERE rd.source = $ownershipSource
        OPTIONAL MATCH (p)-[rr:NEAR_ROAD]->(road:Road)
        WHERE rr.source = $ownershipSource
        OPTIONAL MATCH (p)-[rw:NEAR_WATER]->(water:WaterFeature)
        WHERE rw.source = $ownershipSource
        OPTIONAL MATCH (p)-[rs:HAS_DERIVED_SOIL]->(soil:SoilGroup)
        WHERE rs.source = $ownershipSource
        OPTIONAL MATCH (p)-[rc:INTERSECTS_CONSERVATION_AREA]->(cons:ConservationArea)
        WHERE rc.source = $ownershipSource
        RETURN
            p.id AS parcelId,
            prov.id AS provinceReferenceId,
            prov.name AS provinceName,
            rp.derivedAt AS provinceDerivedAt,
            dist.id AS districtReferenceId,
            dist.name AS districtName,
            rd.derivedAt AS districtDerivedAt,
            road.id AS roadReferenceId,
            road.name AS roadName,
            road.roadType AS roadType,
            rr.distanceMeters AS roadDistanceMeters,
            rr.source AS roadSource,
            rr.derivedAt AS roadDerivedAt,
            water.id AS waterReferenceId,
            water.name AS waterFeatureName,
            rw.featureType AS waterFeatureType,
            rw.distanceMeters AS waterDistanceMeters,
            rw.source AS waterSource,
            rw.derivedAt AS waterDerivedAt,
            soil.id AS soilGroupReferenceId,
            soil.name AS soilGroupName,
            rs.overlapPercentage AS soilOverlapPercentage,
            rs.source AS soilSource,
            rs.derivedAt AS soilDerivedAt,
            collect(
                CASE
                    WHEN cons IS NULL THEN NULL
                    ELSE {
                        conservationAreaReferenceId: cons.id,
                        conservationAreaName: cons.name,
                        overlapPercentage: rc.overlapPercentage,
                        source: rc.source,
                        derivedAt: rc.derivedAt
                    }
                END
            ) AS conservationAreas
        """;

    public const string GetParcelIdsByDerivedSoilGroup = """
        MATCH (soil:SoilGroup { id: $soilGroupId })<-[r:HAS_DERIVED_SOIL]-(p:LandParcel)
        WHERE r.source = $ownershipSource
        RETURN DISTINCT p.id AS parcelId
        ORDER BY parcelId
        """;
}
