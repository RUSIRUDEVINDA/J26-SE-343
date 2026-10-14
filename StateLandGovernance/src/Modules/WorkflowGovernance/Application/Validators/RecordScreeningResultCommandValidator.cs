namespace StateLandGovernance.WorkflowGovernance.Application.Validators;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.Screening;

public sealed class RecordScreeningResultCommandValidator : IRequestValidator<RecordScreeningResultCommand>
{
    public ValidationResult Validate(RecordScreeningResultCommand request)
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

        if (request.ScreeningRequestId == Guid.Empty)
        {
            errors.Add("ScreeningRequestId cannot be empty.");
        }

        if (request.VerifiedFactSnapshotId == Guid.Empty)
        {
            errors.Add("VerifiedFactSnapshotId cannot be empty.");
        }

        if (request.ExpectedLeaseCaseRevision <= 0)
        {
            errors.Add("ExpectedLeaseCaseRevision must be positive.");
        }

        if (request.ScreeningResultId.HasValue && request.ScreeningResultId.Value == Guid.Empty)
        {
            errors.Add("ScreeningResultId cannot be empty if specified.");
        }

        if (string.IsNullOrWhiteSpace(request.Outcome))
        {
            errors.Add("Outcome cannot be empty.");
        }
        else if (!Enum.TryParse<ScreeningOutcome>(request.Outcome, ignoreCase: true, out var outcome))
        {
            errors.Add($"Invalid screening outcome '{request.Outcome}'. Valid outcomes are: Cleared, Advisory, Blocked.");
        }
        else if (outcome == ScreeningOutcome.Pending)
        {
            errors.Add("Outcome cannot be Pending for recorded screening result. Valid outcomes are: Cleared, Advisory, Blocked.");
        }

        if (request.Remarks != null && request.Remarks.Length > 2000)
        {
            errors.Add("Remarks cannot exceed 2000 characters.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
