namespace StateLandGovernance.WorkflowGovernance.Application.Validators;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed class FailDocumentAnalysisCommandValidator : IRequestValidator<FailDocumentAnalysisCommand>
{
    private static readonly string[] SensitivePatterns = new[]
    {
        "bearer ",
        "authorization:",
        "apikey",
        "api-key",
        "password",
        "pwd=",
        "token=",
        "secret",
        "aws_access_key",
        "aws_secret_access_key"
    };

    private static readonly string[] MachinePathPatterns = new[]
    {
        ":\\",
        ":/",
        "/home/",
        "/var/",
        "/tmp/",
        "/usr/",
        "/etc/",
        "/opt/"
    };

    public ValidationResult Validate(FailDocumentAnalysisCommand request)
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

        if (string.IsNullOrWhiteSpace(request.FailureCode))
        {
            errors.Add("FailureCode cannot be blank.");
        }
        else
        {
            var trimmedCode = request.FailureCode.Trim();
            if (trimmedCode.Length > 100)
            {
                errors.Add("FailureCode exceeds maximum length of 100.");
            }

            foreach (var c in trimmedCode)
            {
                if (char.IsControl(c))
                {
                    errors.Add("FailureCode contains control characters.");
                    break;
                }

                if (!char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-')
                {
                    errors.Add("FailureCode contains invalid characters.");
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(request.SafeDescription))
        {
            errors.Add("SafeDescription cannot be blank.");
        }
        else
        {
            var trimmedDesc = request.SafeDescription.Trim();
            if (trimmedDesc.Length > 500)
            {
                errors.Add("SafeDescription exceeds maximum length of 500.");
            }

            foreach (var c in trimmedDesc)
            {
                if (char.IsControl(c))
                {
                    errors.Add("SafeDescription contains control characters or line breaks.");
                    break;
                }
            }

            foreach (var pattern in SensitivePatterns)
            {
                if (trimmedDesc.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("SafeDescription contains potentially sensitive credential or authorization information.");
                    break;
                }
            }

            foreach (var pathPattern in MachinePathPatterns)
            {
                if (trimmedDesc.Contains(pathPattern, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("SafeDescription contains local machine paths or file system references.");
                    break;
                }
            }
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
