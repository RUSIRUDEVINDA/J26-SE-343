namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public sealed record LandRelationshipDto(
    string RelationshipType,
    string SourceNodeType,
    string SourceNodeId,
    string TargetNodeType,
    string TargetNodeId,
    string? Description);

public sealed record LandRelationshipsResponse(
    Guid LandParcelId,
    IReadOnlyList<LandRelationshipDto> Relationships);
