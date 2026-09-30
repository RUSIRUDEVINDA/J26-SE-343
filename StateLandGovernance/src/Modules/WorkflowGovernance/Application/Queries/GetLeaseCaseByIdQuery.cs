namespace StateLandGovernance.WorkflowGovernance.Application.Queries;

using System;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

public sealed record GetLeaseCaseByIdQuery(Guid LeaseCaseId) : IQuery<LeaseCaseDto>;
