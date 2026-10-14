namespace StateLandGovernance.WorkflowGovernance.Application.Queries;

using System;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

public sealed record GetProposalContentAssessmentQuery(
    Guid LeaseCaseId,
    Guid AssessmentResultId
) : IQuery<ProposalContentAssessmentDto?>;
