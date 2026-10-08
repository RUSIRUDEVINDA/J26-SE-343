namespace StateLandGovernance.WorkflowGovernance.Application.Validators;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed class AssessDocumentCompletenessCommandValidator : IRequestValidator<AssessDocumentCompletenessCommand>
{
    public ValidationResult Validate(AssessDocumentCompletenessCommand request)
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

        if (string.IsNullOrWhiteSpace(request.RequirementSetIdentifier))
        {
            errors.Add("RequirementSetIdentifier cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.RequirementSetVersion))
        {
            errors.Add("RequirementSetVersion cannot be empty.");
        }

        if (request.AssessedDocuments == null || request.AssessedDocuments.Count == 0)
        {
            errors.Add("At least one assessed document is required.");
        }

        if (request.ClassifiedDocuments == null)
        {
            errors.Add("ClassifiedDocuments collection cannot be null.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
