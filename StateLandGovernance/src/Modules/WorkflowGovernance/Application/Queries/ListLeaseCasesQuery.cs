namespace StateLandGovernance.WorkflowGovernance.Application.Queries;

using System;
using System.Collections.Generic;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

public sealed record ListLeaseCasesQuery(
    int Skip = 0,
    int Take = 20,
    Guid? OwnerActorId = null
) : IQuery<PagedLeaseCasesDto>;

public sealed record PagedLeaseCasesDto(
    IReadOnlyList<LeaseCaseDto> Items,
    int TotalCount,
    int Skip,
    int Take
);
