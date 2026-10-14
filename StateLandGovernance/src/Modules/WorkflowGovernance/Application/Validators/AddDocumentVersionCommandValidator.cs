namespace StateLandGovernance.WorkflowGovernance.Application.Validators;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed class AddDocumentVersionCommandValidator : IRequestValidator<AddDocumentVersionCommand>
{
    private static readonly Regex Sha256HexRegex = new(@"^[a-fA-F0-9]{64}$", RegexOptions.Compiled);

    public ValidationResult Validate(AddDocumentVersionCommand request)
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

        if (request.ExpectedPredecessorVersionId == Guid.Empty)
        {
            errors.Add("ExpectedPredecessorVersionId cannot be empty.");
        }

        if (request.ExpectedRevision <= 0)
        {
            errors.Add("ExpectedRevision must be greater than zero.");
        }

        if (request.ActorId == Guid.Empty)
        {
            errors.Add("ActorId cannot be empty.");
        }

        if (request.AuthorityContext == null)
        {
            errors.Add("Authority context is required.");
        }

        if (request.ContentReceipt == null)
        {
            errors.Add("Content receipt is required.");
        }
        else
        {
            RegisterGovernedDocumentCommandValidator.ValidateContentReceipt(request.ContentReceipt, errors);
        }

        if (request.NewVersionId.HasValue && request.NewVersionId.Value == Guid.Empty)
        {
            errors.Add("NewVersionId cannot be empty if specified.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
