namespace StateLandGovernance.WorkflowGovernance.Application.Validators;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed class RequestDocumentAnalysisCommandValidator : IRequestValidator<RequestDocumentAnalysisCommand>
{
    public ValidationResult Validate(RequestDocumentAnalysisCommand request)
    {
        if (request == null)
        {
            return ValidationResult.Failure("Command details cannot be null.");
        }

        var errors = new List<string>();

        if (request.GovernedDocumentId == Guid.Empty)
        {
            errors.Add("GovernedDocumentId cannot be empty.");
        }

        if (request.DocumentVersionId == Guid.Empty)
        {
            errors.Add("DocumentVersionId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.ModelProvider))
        {
            errors.Add("ModelProvider cannot be blank.");
        }

        if (string.IsNullOrWhiteSpace(request.ModelName))
        {
            errors.Add("ModelName cannot be blank.");
        }

        if (string.IsNullOrWhiteSpace(request.ModelVersion))
        {
            errors.Add("ModelVersion cannot be blank.");
        }

        if (request.RequestedCapabilities == null || request.RequestedCapabilities.Count == 0)
        {
            errors.Add("RequestedCapabilities cannot be empty.");
        }
        else if (request.RequestedCapabilities.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("RequestedCapabilities cannot contain blank entries.");
        }

        if (request.ExpectedAnalysisRevision.HasValue && request.ExpectedAnalysisRevision.Value <= 0)
        {
            errors.Add("ExpectedAnalysisRevision must be greater than zero if specified.");
        }

        if (request.AnalysisRunId.HasValue && request.AnalysisRunId.Value == Guid.Empty)
        {
            errors.Add("AnalysisRunId cannot be empty if specified.");
        }

        if (request.DocumentAnalysisId.HasValue && request.DocumentAnalysisId.Value == Guid.Empty)
        {
            errors.Add("DocumentAnalysisId cannot be empty if specified.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
