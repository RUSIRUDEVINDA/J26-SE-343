namespace StateLandGovernance.WorkflowGovernance.Application.Queries;

using System;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

/// <summary>
/// Query to retrieve the current screening status for a lease case, including staleness against current verified facts.
/// Side-effect-free, zero commits.
/// </summary>
public sealed record GetCurrentScreeningStatusQuery(Guid LeaseCaseId) : IQuery<ScreeningResultDto?>;
