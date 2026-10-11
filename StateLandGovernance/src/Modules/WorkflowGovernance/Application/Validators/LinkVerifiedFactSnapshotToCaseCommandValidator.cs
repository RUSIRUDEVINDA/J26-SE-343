namespace StateLandGovernance.WorkflowGovernance.Application.Validators;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed class LinkVerifiedFactSnapshotToCaseCommandValidator : IRequestValidator<LinkVerifiedFactSnapshotToCaseCommand>
{
    public ValidationResult Validate(LinkVerifiedFactSnapshotToCaseCommand request)
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

        if (request.DocumentAnalysisId == Guid.Empty)
        {
            errors.Add("DocumentAnalysisId cannot be empty.");
        }

        if (request.SnapshotId == Guid.Empty)
        {
            errors.Add("SnapshotId cannot be empty.");
        }

        if (request.ExpectedLeaseCaseRevision <= 0)
        {
            errors.Add("ExpectedLeaseCaseRevision must be positive.");
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
