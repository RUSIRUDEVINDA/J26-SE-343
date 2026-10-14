namespace StateLandGovernance.WorkflowGovernance.Application.Queries;

using System;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

public sealed record GetLatestDocumentCompletenessAssessmentByCaseIdQuery(
    Guid LeaseCaseId
) : IQuery<DocumentCompletenessAssessmentDto?>;
