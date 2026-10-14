namespace StateLandGovernance.WorkflowGovernance.Application.Validators;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed class CreateVerifiedFactSnapshotCommandValidator : IRequestValidator<CreateVerifiedFactSnapshotCommand>
{
    public ValidationResult Validate(CreateVerifiedFactSnapshotCommand request)
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

        if (request.PublishingActorId == Guid.Empty)
        {
            errors.Add("PublishingActorId cannot be empty.");
        }

        if (request.AuthorityContext == null)
        {
            errors.Add("AuthorityContext is required.");
        }

        if (request.ExpectedAnalysisRevision <= 0)
        {
            errors.Add("ExpectedAnalysisRevision must be positive.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
