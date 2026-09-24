namespace StateLandGovernance.WorkflowGovernance.Application.Validators;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed class RegisterGovernedDocumentCommandValidator : IRequestValidator<RegisterGovernedDocumentCommand>
{
    private static readonly Regex Sha256HexRegex = new(@"^[a-fA-F0-9]{64}$", RegexOptions.Compiled);

    public ValidationResult Validate(RegisterGovernedDocumentCommand request)
    {
        if (request == null)
        {
            return ValidationResult.Failure("Command details cannot be null.");
        }

        var errors = new List<string>();

        if (request.LeaseCaseId == Guid.Empty)
        {
            errors.Add("LeaseCaseId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.LogicalCategory))
        {
            errors.Add("LogicalCategory cannot be blank.");
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
            ValidateContentReceipt(request.ContentReceipt, errors);
        }

        if (request.GovernedDocumentId.HasValue && request.GovernedDocumentId.Value == Guid.Empty)
        {
            errors.Add("GovernedDocumentId cannot be empty if specified.");
        }

        if (request.InitialVersionId.HasValue && request.InitialVersionId.Value == Guid.Empty)
        {
            errors.Add("InitialVersionId cannot be empty if specified.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }

    internal static void ValidateContentReceipt(DocumentContentReceipt receipt, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(receipt.ContentReference))
        {
            errors.Add("Content reference cannot be null or blank.");
        }

        if (string.IsNullOrWhiteSpace(receipt.OriginalFileName))
        {
            errors.Add("OriginalFileName cannot be blank.");
        }

        if (string.IsNullOrWhiteSpace(receipt.MediaType))
        {
            errors.Add("MediaType cannot be blank.");
        }

        if (receipt.FileSizeInBytes <= 0)
        {
            errors.Add("FileSizeInBytes must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(receipt.ChecksumAlgorithm))
        {
            errors.Add("Checksum algorithm cannot be null or blank.");
        }
        else if (!string.Equals(receipt.ChecksumAlgorithm, DocumentContentReceipt.CanonicalAlgorithm, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Checksum algorithm must be 'SHA-256'.");
        }

        if (string.IsNullOrWhiteSpace(receipt.ChecksumValue))
        {
            errors.Add("Checksum value cannot be null or blank.");
        }
        else if (!Sha256HexRegex.IsMatch(receipt.ChecksumValue))
        {
            errors.Add("Checksum value must be a 64-character hexadecimal string.");
        }
    }
}
