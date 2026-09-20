namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;

using System;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class DocumentClassificationCode : IEquatable<DocumentClassificationCode>
{
    public string Value { get; }

    public DocumentClassificationCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDocumentCompletenessAssessmentException("DocumentClassificationCode cannot be null, empty, or whitespace.");
        }

        var trimmed = value.Trim();

        if (trimmed.Length > 255)
        {
            throw new InvalidDocumentCompletenessAssessmentException("DocumentClassificationCode exceeds maximum allowed length.");
        }

        if (trimmed.Any(char.IsControl))
        {
            throw new InvalidDocumentCompletenessAssessmentException("DocumentClassificationCode cannot contain control characters.");
        }

        Value = trimmed;
    }

    public bool Equals(DocumentClassificationCode? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => Equals(obj as DocumentClassificationCode);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public override string ToString() => Value;
}
