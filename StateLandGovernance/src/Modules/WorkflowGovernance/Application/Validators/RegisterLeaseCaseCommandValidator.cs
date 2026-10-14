namespace StateLandGovernance.WorkflowGovernance.Application.Validators;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed class RegisterLeaseCaseCommandValidator : IRequestValidator<RegisterLeaseCaseCommand>
{
    public ValidationResult Validate(RegisterLeaseCaseCommand request)
    {
        if (request == null)
        {
            return ValidationResult.Failure("Command details cannot be null.");
        }

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.ApplicationReference))
        {
            errors.Add("Application reference is required.");
        }

        if (request.ActorId == Guid.Empty)
        {
            errors.Add("Actor identifier is required.");
        }

        if (request.AuthorityContext == null)
        {
            errors.Add("Authority context is required.");
        }
        else
        {
            if (request.AuthorityContext.ActorId == Guid.Empty)
            {
                errors.Add("Authority context actor identifier is required.");
            }

            if (request.AuthorityContext.Capabilities == null || request.AuthorityContext.Capabilities.Count == 0)
            {
                errors.Add("Authority context capabilities cannot be null or empty.");
            }

            if (string.IsNullOrWhiteSpace(request.AuthorityContext.ScopeKind))
            {
                errors.Add("Authority scope kind is required.");
            }
        }

        if (request.LeaseCaseId.HasValue && request.LeaseCaseId.Value == Guid.Empty)
        {
            errors.Add("Lease case identifier cannot be empty if specified.");
        }

        if (request.ProposalIntake != null)
        {
            if (string.IsNullOrWhiteSpace(request.ProposalIntake.SourceReference))
            {
                errors.Add("Intake source reference cannot be null, empty, or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(request.ProposalIntake.SourceVersion))
            {
                errors.Add("Intake source version cannot be null, empty, or whitespace.");
            }

            if (request.ProposalIntake.RequestedExtentValue.HasValue)
            {
                if (request.ProposalIntake.RequestedExtentValue.Value <= 0)
                {
                    errors.Add("Requested extent must be greater than zero.");
                }

                if (string.IsNullOrWhiteSpace(request.ProposalIntake.RequestedExtentUnit))
                {
                    errors.Add("Measurement unit is required for land extent.");
                }
            }
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
