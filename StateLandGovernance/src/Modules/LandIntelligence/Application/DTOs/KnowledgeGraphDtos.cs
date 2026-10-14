namespace StateLandGovernance.LandIntelligence.Application.DTOs;

/// <summary>
/// Minimal land parcel node — references PostgreSQL record; does not duplicate spatial/attribute data.
/// </summary>
public sealed record LandParcelGraphNodeDto(
    Guid Id,
    string CadastralNumber,
    string? SurveyPlanReference);

public sealed record AdministrativeAreaGraphNodeDto(
    Guid Id,
    string Province,
    string District,
    string DivisionalSecretariat,
    string? GramaNiladhariDivision);

public sealed record LandCategoryGraphNodeDto(
    Guid Id,
    string CategoryType,
    string Name,
    string? Description);

public sealed record LandUseGraphNodeDto(
    Guid Id,
    string UseType,
    string Name,
    string? Description);

public sealed record SpatialConstraintGraphNodeDto(
    Guid Id,
    string ConstraintType,
    string Description,
    string Severity);

public sealed record RegulationGraphNodeDto(
    Guid Id,
    string GazetteNumber,
    string Title,
    DateOnly EffectiveDate,
    string? Summary);

public sealed record InfrastructureFeatureGraphNodeDto(
    Guid Id,
    string FeatureType,
    string Name,
    decimal? DistanceMeters,
    string? Description);

public sealed record EnvironmentalAreaGraphNodeDto(
    Guid Id,
    string RestrictionType,
    string Description,
    string Severity);

public sealed record GraphTraversalResultDto(
    Guid OriginParcelId,
    int Depth,
    IReadOnlyList<LandRelationshipDto> Relationships,
    IReadOnlyList<Guid> RelatedParcelIds);
