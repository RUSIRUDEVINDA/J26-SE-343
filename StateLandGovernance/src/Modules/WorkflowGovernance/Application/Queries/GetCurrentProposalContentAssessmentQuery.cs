namespace StateLandGovernance.WorkflowGovernance.Application.Queries;

using System;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

public sealed record GetCurrentProposalContentAssessmentQuery(
    Guid LeaseCaseId,
    string TemplateDefinitionDigest,
    ProposalSourceBindingDto? CurrentSourceBinding = null
) : IQuery<ProposalContentAssessmentDto?>;
