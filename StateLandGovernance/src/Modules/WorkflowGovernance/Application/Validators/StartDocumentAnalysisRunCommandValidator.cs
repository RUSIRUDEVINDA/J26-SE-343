namespace StateLandGovernance.WorkflowGovernance.Application.Validators;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed class StartDocumentAnalysisRunCommandValidator : IRequestValidator<StartDocumentAnalysisRunCommand>
{
    public ValidationResult Validate(StartDocumentAnalysisRunCommand request)
    {
        if (request == null)
        {
            return ValidationResult.Failure("Command details cannot be null.");
        }

        var errors = new List<string>();

        if (request.DocumentAnalysisId == Guid.Empty)
        {
            errors.Add("DocumentAnalysisId cannot be empty.");
        }

        if (request.AnalysisRunId == Guid.Empty)
        {
            errors.Add("AnalysisRunId cannot be empty.");
        }

        if (request.ExpectedRevision <= 0)
        {
            errors.Add("ExpectedRevision must be greater than zero.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
