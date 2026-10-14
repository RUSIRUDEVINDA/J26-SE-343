namespace StateLandGovernance.WorkflowGovernance.Application.Validators;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed class CompleteDocumentAnalysisCommandValidator : IRequestValidator<CompleteDocumentAnalysisCommand>
{
    public ValidationResult Validate(CompleteDocumentAnalysisCommand request)
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

        if (string.IsNullOrWhiteSpace(request.Outcome))
        {
            errors.Add("Outcome cannot be blank.");
        }
        else if (!string.Equals(request.Outcome, "OutputsProduced", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(request.Outcome, "NoFindings", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Outcome must be either 'OutputsProduced' or 'NoFindings'.");
        }

        if (request.Artifacts == null)
        {
            errors.Add("Artifacts cannot be null.");
        }

        if (request.ExtractedCandidateFacts == null)
        {
            errors.Add("ExtractedCandidateFacts cannot be null.");
        }

        if (request.Artifacts != null && request.ExtractedCandidateFacts != null)
        {
            if (string.Equals(request.Outcome, "OutputsProduced", StringComparison.OrdinalIgnoreCase) &&
                request.Artifacts.Count == 0 && request.ExtractedCandidateFacts.Count == 0)
            {
                errors.Add("OutputsProduced requires at least one artifact or extracted candidate fact.");
            }

            if (string.Equals(request.Outcome, "NoFindings", StringComparison.OrdinalIgnoreCase) &&
                request.ExtractedCandidateFacts.Count > 0)
            {
                errors.Add("NoFindings cannot contain candidate facts.");
            }

            foreach (var a in request.Artifacts)
            {
                if (string.IsNullOrWhiteSpace(a.ArtifactKind)) errors.Add("ArtifactKind cannot be blank.");
                if (string.IsNullOrWhiteSpace(a.StorageReference)) errors.Add("StorageReference cannot be blank.");
                if (string.IsNullOrWhiteSpace(a.ContentType)) errors.Add("ContentType cannot be blank.");
                if (string.IsNullOrWhiteSpace(a.ChecksumAlgorithm)) errors.Add("ChecksumAlgorithm cannot be blank.");
                if (string.IsNullOrWhiteSpace(a.ChecksumValue)) errors.Add("ChecksumValue cannot be blank.");
            }

            foreach (var f in request.ExtractedCandidateFacts)
            {
                if (string.IsNullOrWhiteSpace(f.FactCode)) errors.Add("FactCode cannot be blank.");
                if (string.IsNullOrWhiteSpace(f.ValueKind)) errors.Add("ValueKind cannot be blank.");
                if (string.IsNullOrWhiteSpace(f.CanonicalValue)) errors.Add("CanonicalValue cannot be blank.");
                if (f.ConfidenceScore.HasValue && (f.ConfidenceScore.Value < 0.0m || f.ConfidenceScore.Value > 1.0m))
                {
                    errors.Add("ConfidenceScore must be between 0.0 and 1.0.");
                }
                if (f.PageNumber.HasValue && f.PageNumber.Value <= 0)
                {
                    errors.Add("PageNumber must be greater than zero if specified.");
                }
            }
        }

        if (request.ResultId.HasValue && request.ResultId.Value == Guid.Empty)
        {
            errors.Add("ResultId cannot be empty if specified.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
