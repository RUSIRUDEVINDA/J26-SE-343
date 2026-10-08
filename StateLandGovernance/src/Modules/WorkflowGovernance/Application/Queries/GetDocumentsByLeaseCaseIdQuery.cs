namespace StateLandGovernance.WorkflowGovernance.Application.Queries;

using System;
using System.Collections.Generic;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

public sealed record GetDocumentsByLeaseCaseIdQuery(Guid LeaseCaseId) : IQuery<IReadOnlyList<GovernedDocumentDto>>;
