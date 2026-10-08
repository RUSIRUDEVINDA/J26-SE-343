namespace StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Cypher;

/// <summary>
/// Cypher statements for Component 1 knowledge graph operations.
/// </summary>
internal static class KnowledgeGraphCypher
{
    public const string UpsertLandParcel = """
        MERGE (p:LandParcel { id: $id })
        SET p.cadastralNumber = $cadastralNumber,
            p.surveyPlanReference = $surveyPlanReference,
            p.updatedAt = datetime()
        RETURN p
        """;

    public const string UpsertAdministrativeArea = """
        MERGE (a:AdministrativeArea { id: $id })
        SET a.province = $province,
            a.district = $district,
            a.divisionalSecretariat = $divisionalSecretariat,
            a.gramaNiladhariDivision = $gramaNiladhariDivision,
            a.updatedAt = datetime()
        RETURN a
        """;

    public const string UpsertLandCategory = """
        MERGE (c:LandCategory { id: $id })
        SET c.categoryType = $categoryType,
            c.name = $name,
            c.description = $description,
            c.updatedAt = datetime()
        RETURN c
        """;

    public const string UpsertLandUse = """
        MERGE (u:LandUse { id: $id })
        SET u.useType = $useType,
            u.name = $name,
            u.description = $description,
            u.updatedAt = datetime()
        RETURN u
        """;

    public const string UpsertSpatialConstraint = """
        MERGE (s:SpatialConstraint { id: $id })
        SET s.constraintType = $constraintType,
            s.description = $description,
            s.severity = $severity,
            s.updatedAt = datetime()
        RETURN s
        """;

    public const string UpsertRegulation = """
        MERGE (r:Regulation { id: $id })
        SET r.gazetteNumber = $gazetteNumber,
            r.title = $title,
            r.effectiveDate = $effectiveDate,
            r.summary = $summary,
            r.updatedAt = datetime()
        RETURN r
        """;

    public const string UpsertInfrastructureFeature = """
        MERGE (i:InfrastructureFeature { id: $id })
        SET i.featureType = $featureType,
            i.name = $name,
            i.distanceMeters = $distanceMeters,
            i.description = $description,
            i.updatedAt = datetime()
        RETURN i
        """;

    public const string UpsertEnvironmentalArea = """
        MERGE (e:EnvironmentalArea { id: $id })
        SET e.restrictionType = $restrictionType,
            e.description = $description,
            e.severity = $severity,
            e.updatedAt = datetime()
        RETURN e
        """;

    public const string LinkParcelToAdministrativeArea = """
        MATCH (p:LandParcel { id: $parcelId })
        MATCH (a:AdministrativeArea { id: $targetId })
        MERGE (p)-[r:LOCATED_IN]->(a)
        SET r.updatedAt = datetime()
        RETURN r
        """;

    public const string LinkParcelToCategory = """
        MATCH (p:LandParcel { id: $parcelId })
        MATCH (c:LandCategory { id: $targetId })
        MERGE (p)-[r:HAS_CATEGORY]->(c)
        SET r.updatedAt = datetime()
        RETURN r
        """;

    public const string LinkParcelToUse = """
        MATCH (p:LandParcel { id: $parcelId })
        MATCH (u:LandUse { id: $targetId })
        MERGE (p)-[r:HAS_USE]->(u)
        SET r.updatedAt = datetime()
        RETURN r
        """;

    public const string LinkParcelToRegulation = """
        MATCH (p:LandParcel { id: $parcelId })
        MATCH (reg:Regulation { id: $targetId })
        MERGE (p)-[r:SUBJECT_TO]->(reg)
        SET r.updatedAt = datetime()
        RETURN r
        """;

    public const string LinkParcelToSpatialConstraint = """
        MATCH (p:LandParcel { id: $parcelId })
        MATCH (s:SpatialConstraint { id: $targetId })
        MERGE (p)-[r:HAS_RESTRICTION]->(s)
        SET r.updatedAt = datetime()
        RETURN r
        """;

    public const string LinkParcelToInfrastructureFeature = """
        MATCH (p:LandParcel { id: $parcelId })
        MATCH (i:InfrastructureFeature { id: $targetId })
        MERGE (p)-[r:NEAR]->(i)
        SET r.distanceMeters = $distanceMeters,
            r.updatedAt = datetime()
        RETURN r
        """;

    public const string LinkParcelToEnvironmentalArea = """
        MATCH (p:LandParcel { id: $parcelId })
        MATCH (e:EnvironmentalArea { id: $targetId })
        MERGE (p)-[r:RELATED_TO]->(e)
        SET r.updatedAt = datetime()
        RETURN r
        """;

    public const string GetParcelRelationships = """
        MATCH (p:LandParcel { id: $parcelId })-[r]->(target)
        RETURN type(r) AS relationshipType,
               labels(p)[0] AS sourceNodeType,
               p.id AS sourceNodeId,
               labels(target)[0] AS targetNodeType,
               target.id AS targetNodeId,
               coalesce(
                   target.description,
                   target.title,
                   target.name,
                   target.district,
                   target.categoryType,
                   target.useType
               ) AS description
        ORDER BY relationshipType, targetNodeType
        """;

    public const string GetParcelIdsByCategory = """
        MATCH (p:LandParcel)-[:HAS_CATEGORY]->(c:LandCategory { id: $categoryId })
        RETURN DISTINCT p.id AS parcelId
        ORDER BY parcelId
        """;

    public const string DeleteLandParcelGraph = """
        MATCH (p:LandParcel { id: $parcelId })
        DETACH DELETE p
        """;

    public const string ClearParcelRelationships = """
        MATCH (p:LandParcel { id: $parcelId })-[r]->()
        DELETE r
        """;

    /// <summary>
    /// Variable-length path depth cannot be parameterized in Cypher; depth is validated and injected server-side.
    /// </summary>
    public static string BuildTraverseFromParcelQuery(int maxDepth) => $$"""
        MATCH path = (origin:LandParcel { id: $parcelId })-[*1..{{maxDepth}}]-(related:LandParcel)
        WHERE origin <> related
        UNWIND relationships(path) AS rel
        WITH origin, related, rel
        RETURN DISTINCT
            type(rel) AS relationshipType,
            labels(startNode(rel))[0] AS sourceNodeType,
            startNode(rel).id AS sourceNodeId,
            labels(endNode(rel))[0] AS targetNodeType,
            endNode(rel).id AS targetNodeId,
            coalesce(
                endNode(rel).description,
                endNode(rel).title,
                endNode(rel).name,
                endNode(rel).district,
                endNode(rel).categoryType,
                endNode(rel).useType,
                startNode(rel).description,
                startNode(rel).title,
                startNode(rel).name
            ) AS description,
            related.id AS relatedParcelId
        """;
}
