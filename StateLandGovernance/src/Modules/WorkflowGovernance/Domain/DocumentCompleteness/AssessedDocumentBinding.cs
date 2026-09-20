namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class AssessedDocumentBinding : IEquatable<AssessedDocumentBinding>
{
    public GovernedDocumentId GovernedDocumentId { get; }
    public DocumentVersionId DocumentVersionId { get; }
    public DocumentChecksum DocumentChecksum { get; }

    public AssessedDocumentBinding(
        GovernedDocumentId governedDocumentId,
        DocumentVersionId documentVersionId,
        DocumentChecksum documentChecksum)
    {
        if (governedDocumentId.Value == Guid.Empty) throw new InvalidDocumentCompletenessAssessmentException("GovernedDocumentId cannot be empty.");
        if (documentVersionId.Value == Guid.Empty) throw new InvalidDocumentCompletenessAssessmentException("DocumentVersionId cannot be empty.");
        if (documentChecksum == null) throw new InvalidDocumentCompletenessAssessmentException("DocumentChecksum is required.");

        GovernedDocumentId = governedDocumentId;
        DocumentVersionId = documentVersionId;
        DocumentChecksum = documentChecksum;
    }

    public bool Equals(AssessedDocumentBinding? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return GovernedDocumentId.Equals(other.GovernedDocumentId) &&
               DocumentVersionId.Equals(other.DocumentVersionId) &&
               DocumentChecksum.Equals(other.DocumentChecksum);
    }

    public override bool Equals(object? obj) => Equals(obj as AssessedDocumentBinding);

    public override int GetHashCode() => HashCode.Combine(GovernedDocumentId, DocumentVersionId, DocumentChecksum);
}
