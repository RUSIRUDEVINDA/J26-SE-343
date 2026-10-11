namespace StateLandGovernance.WorkflowGovernance.Application.Validators;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

public sealed class VerifyCandidateFactCommandValidator : IRequestValidator<VerifyCandidateFactCommand>
{
    public ValidationResult Validate(VerifyCandidateFactCommand request)
    {
        if (request == null)
        {
            return ValidationResult.Failure("Command cannot be null.");
        }

        var errors = new List<string>();

        if (request.DocumentAnalysisId == Guid.Empty)
        {
            errors.Add("DocumentAnalysisId cannot be empty.");
        }

        if (request.AnalysisRunResultId == Guid.Empty)
        {
            errors.Add("AnalysisRunResultId cannot be empty.");
        }

        if (request.ExtractedFactId == Guid.Empty)
        {
            errors.Add("ExtractedFactId cannot be empty.");
        }

        if (request.VerifyingActorId == Guid.Empty)
        {
            errors.Add("VerifyingActorId cannot be empty.");
        }

        if (request.ExpectedAnalysisRevision <= 0)
        {
            errors.Add("ExpectedAnalysisRevision must be positive.");
        }

        if (request.AuthorityContext == null)
        {
            errors.Add("AuthorityContext is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Decision) ||
            !Enum.TryParse<FactVerificationDecision>(request.Decision, ignoreCase: true, out var parsedDecision))
        {
            errors.Add("Decision must be one of: Confirmed, Corrected, Unsupported.");
        }
        else
        {
            if (parsedDecision == FactVerificationDecision.Corrected)
            {
                if (request.CorrectedValue == null)
                {
                    errors.Add("CorrectedValue is required when decision is Corrected.");
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(request.CorrectedValue.Kind))
                    {
                        errors.Add("CorrectedValue.Kind cannot be empty.");
                    }
                    if (string.IsNullOrWhiteSpace(request.CorrectedValue.CanonicalValue))
                    {
                        errors.Add("CorrectedValue.CanonicalValue cannot be empty.");
                    }
                }
            }
            else if (request.CorrectedValue != null)
            {
                errors.Add("CorrectedValue must be null when decision is not Corrected.");
            }
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
