namespace StateLandGovernance.WorkflowGovernance.Application.Queries;

using System;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

public sealed record GetGovernedDocumentByIdQuery(Guid GovernedDocumentId) : IQuery<GovernedDocumentDto>;
