namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using System.Collections.Generic;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

/// <summary>
/// Command for an authorized officer to correct a proposal-content assessment result.
/// Operates through the Domain correction method, creating an authoritative corrected assessment
/// superseding the original unreviewed assessment result.
/// Resolves the governed template through IProposalTemplateProvider.
/// </summary>
public sealed record CorrectProposalContentAssessmentCommand(
    Guid LeaseCaseId,
    Guid AssessmentResultId,
    Guid ActorId,
    VerifiedAuthorityContext AuthorityContext,
    int ExpectedRevision,
    string Reason,
    string EvidenceReference,
    IReadOnlyList<ProposalObservationDto> CorrectedObservations
) : ICommand;
