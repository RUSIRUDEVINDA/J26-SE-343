namespace StateLandGovernance.WorkflowGovernance.Application.Validators;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed class RequestScreeningCommandValidator : IRequestValidator<RequestScreeningCommand>
{
    public ValidationResult Validate(RequestScreeningCommand request)
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

        if (request.ExpectedLeaseCaseRevision <= 0)
        {
            errors.Add("ExpectedLeaseCaseRevision must be positive.");
        }

        if (request.ScreeningRequestId.HasValue && request.ScreeningRequestId.Value == Guid.Empty)
        {
            errors.Add("ScreeningRequestId cannot be empty if specified.");
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
