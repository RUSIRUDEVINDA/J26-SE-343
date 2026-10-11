namespace StateLandGovernance.WorkflowGovernance.Application.Validators;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;

public sealed class ReviewDocumentClassificationCommandValidator : IRequestValidator<ReviewDocumentClassificationCommand>
{
    public ValidationResult Validate(ReviewDocumentClassificationCommand request)
    {
        if (request == null)
        {
            return ValidationResult.Failure("Command cannot be null.");
        }

        var errors = new List<string>();

        if (request.AssessmentId == Guid.Empty)
        {
            errors.Add("AssessmentId cannot be empty.");
        }

        if (request.ClassifiedDocumentId == Guid.Empty)
        {
            errors.Add("ClassifiedDocumentId cannot be empty.");
        }

        if (request.ReviewingActorId == Guid.Empty)
        {
            errors.Add("ReviewingActorId cannot be empty.");
        }

        if (request.ExpectedRevision <= 0)
        {
            errors.Add("ExpectedRevision must be positive.");
        }

        if (request.AuthorityContext == null)
        {
            errors.Add("AuthorityContext is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Decision) ||
            !Enum.TryParse<ClassificationReviewDecision>(request.Decision, ignoreCase: true, out var parsedDecision))
        {
            errors.Add("Decision must be one of: Confirmed, Corrected, Unsupported.");
        }
        else
        {
            if (parsedDecision == ClassificationReviewDecision.Corrected && string.IsNullOrWhiteSpace(request.CorrectedClassificationCode))
            {
                errors.Add("CorrectedClassificationCode is required when decision is Corrected.");
            }
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
