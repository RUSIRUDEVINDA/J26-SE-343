namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using System.Collections.Generic;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

/// <summary>
/// Command to assess supporting-document completeness against a governed requirement set.
/// The legal requirements are resolved by the Application handler through IDocumentRequirementProvider.
/// Evaluates presence and validity of required documents independently from proposal-content completeness.
/// </summary>
public sealed record AssessDocumentCompletenessCommand(
    Guid LeaseCaseId,
    string RequirementSetIdentifier,
    string RequirementSetVersion,
    IReadOnlyList<AssessedDocumentBindingDto> AssessedDocuments,
    IReadOnlyList<ClassifiedDocumentDto> ClassifiedDocuments,
    Guid? AssessmentId = null
) : ICommand;
