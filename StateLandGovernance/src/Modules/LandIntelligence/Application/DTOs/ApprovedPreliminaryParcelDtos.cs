using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.DTOs;

public sealed record ApprovedPreliminaryParcelSummaryDto(
    Guid LandParcelId,
    string CadastralNumber,
    string Province,
    string District,
    decimal AreaValue,
    AreaUnit AreaUnit,
    string? SourceReportedLandUse,
    DateTimeOffset ApprovedAtUtc,
    Guid LeaseInterestRequestId,
    string? RequestReference);

public sealed record ApprovedPreliminaryParcelListResponse(
    IReadOnlyList<ApprovedPreliminaryParcelSummaryDto> Results,
    int Page,
    int PageSize,
    int TotalCount);
