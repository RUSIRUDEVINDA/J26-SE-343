namespace StateLandGovernance.WorkflowGovernance.Application.Validators;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed class CorrectProposalContentAssessmentCommandValidator : IRequestValidator<CorrectProposalContentAssessmentCommand>
{
    public ValidationResult Validate(CorrectProposalContentAssessmentCommand request)
    {
        if (request == null)
        {
            return ValidationResult.Failure("Command cannot be null.");
        }

        var errors = new List<string>();

        if (request.LeaseCaseId == Guid.Empty)
        {
            errors.Add("LeaseCaseId cannot be empty.");
        }

        if (request.AssessmentResultId == Guid.Empty)
        {
            errors.Add("AssessmentResultId cannot be empty.");
        }

        if (request.ActorId == Guid.Empty)
        {
            errors.Add("ActorId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            errors.Add("Correction reason is required.");
        }

        if (string.IsNullOrWhiteSpace(request.EvidenceReference))
        {
            errors.Add("EvidenceReference is required for correction.");
        }

        if (request.AuthorityContext == null)
        {
            errors.Add("AuthorityContext is required.");
        }

        if (request.ExpectedRevision <= 0)
        {
            errors.Add("ExpectedRevision must be positive.");
        }

        if (request.CorrectedObservations == null)
        {
            errors.Add("CorrectedObservations collection cannot be null.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
