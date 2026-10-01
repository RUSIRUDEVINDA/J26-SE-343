namespace StateLandGovernance.WorkflowGovernance.Application.Validators;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed class ConfirmProposalContentAssessmentCommandValidator : IRequestValidator<ConfirmProposalContentAssessmentCommand>
{
    public ValidationResult Validate(ConfirmProposalContentAssessmentCommand request)
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

        if (request.AuthorityContext == null)
        {
            errors.Add("AuthorityContext is required.");
        }

        if (request.ExpectedRevision <= 0)
        {
            errors.Add("ExpectedRevision must be positive.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
