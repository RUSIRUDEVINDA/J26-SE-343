namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using System.Collections.Generic;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

/// <summary>
/// Command to assess proposal document content against a governed proposal template.
/// The legal proposal template is resolved through IProposalTemplateProvider by TemplateId and TemplateVersion.
/// Produces an unreviewed proposal-content assessment recorded on the LeaseCase.
/// </summary>
public sealed record AssessProposalContentCommand(
    Guid LeaseCaseId,
    Guid ProposalDocumentId,
    Guid DocumentVersionId,
    string ChecksumAlgorithm,
    string ChecksumValue,
    string TemplateId,
    string TemplateVersion,
    string? ExtractionReference,
    IReadOnlyList<ProposalObservationDto> Observations,
    int ExpectedRevision
) : ICommand;
