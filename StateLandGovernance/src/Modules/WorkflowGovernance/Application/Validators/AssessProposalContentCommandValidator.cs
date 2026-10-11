namespace StateLandGovernance.WorkflowGovernance.Application.Validators;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed class AssessProposalContentCommandValidator : IRequestValidator<AssessProposalContentCommand>
{
    public ValidationResult Validate(AssessProposalContentCommand request)
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

        if (request.ProposalDocumentId == Guid.Empty)
        {
            errors.Add("ProposalDocumentId cannot be empty.");
        }

        if (request.DocumentVersionId == Guid.Empty)
        {
            errors.Add("DocumentVersionId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.ChecksumAlgorithm))
        {
            errors.Add("ChecksumAlgorithm cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.ChecksumValue))
        {
            errors.Add("ChecksumValue cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.TemplateId))
        {
            errors.Add("TemplateId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.TemplateVersion))
        {
            errors.Add("TemplateVersion cannot be empty.");
        }

        if (request.ExpectedRevision <= 0)
        {
            errors.Add("ExpectedRevision must be positive.");
        }

        if (request.Observations == null)
        {
            errors.Add("Observations collection cannot be null.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
