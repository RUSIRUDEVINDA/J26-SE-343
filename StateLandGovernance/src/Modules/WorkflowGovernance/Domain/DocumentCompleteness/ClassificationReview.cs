namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;

using System;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class ClassificationReview
{
    public ClassificationReviewId Id { get; }
    public ClassifiedDocumentId ClassifiedDocumentId { get; }
    public ClassificationReviewDecision Decision { get; }
    public DocumentClassificationCode? CorrectedClassificationCode { get; }
    public string? Reason { get; }
    public Guid ReviewingActorId { get; }
    public DateTime ReviewedAt { get; }
    public string VerifiedCapability { get; }
    public AuthorityScopeKind GrantedAuthorityScopeKind { get; }
    public string? GrantedAuthorityScopeIdentifier { get; }
    public AuthorityScopeKind RequiredAuthorityScopeKind { get; }
    public string RequiredAuthorityScopeIdentifier { get; }
    public DateTime AuthorityVerificationTime { get; }

    public ClassificationReview(
        ClassificationReviewId id,
        ClassifiedDocumentId classifiedDocumentId,
        DocumentClassificationCode? originalClassificationCode,
        ClassificationReviewDecision decision,
        DocumentClassificationCode? correctedClassificationCode,
        string? reason,
        Guid reviewingActorId,
        DateTime reviewedAt,
        string verifiedCapability,
        AuthorityScopeKind grantedAuthorityScopeKind,
        string? grantedAuthorityScopeIdentifier,
        AuthorityScopeKind requiredAuthorityScopeKind,
        string requiredAuthorityScopeIdentifier,
        DateTime authorityVerificationTime)
    {
        if (id.Value == Guid.Empty) throw new InvalidClassificationReviewException("Id cannot be empty.");
        if (classifiedDocumentId.Value == Guid.Empty) throw new InvalidClassificationReviewException("ClassifiedDocumentId cannot be empty.");
        if (reviewingActorId == Guid.Empty) throw new InvalidClassificationReviewException("ReviewingActorId cannot be empty.");
        if (reviewedAt.Kind != DateTimeKind.Utc) throw new InvalidClassificationReviewException("ReviewedAt must be UTC.");

        if (decision == ClassificationReviewDecision.Confirmed)
        {
            if (originalClassificationCode == null) throw new InvalidClassificationReviewException("Cannot confirm an Unclassified document.");
            if (correctedClassificationCode != null) throw new InvalidClassificationReviewException("Corrected code must be null when confirmed.");
            if (reason != null) throw new InvalidClassificationReviewException("Reason must be null when confirmed.");
        }
        else if (decision == ClassificationReviewDecision.Corrected)
        {
            if (correctedClassificationCode == null) throw new InvalidClassificationReviewException("Corrected code is required.");
            if (originalClassificationCode != null && correctedClassificationCode.Equals(originalClassificationCode))
                throw new InvalidClassificationReviewException("Corrected code must differ from original.");
            if (string.IsNullOrWhiteSpace(reason)) throw new InvalidClassificationReviewException("Reason is required.");
            reason = reason.Trim();
            if (reason.Length > 2000 || reason.Any(char.IsControl)) throw new InvalidClassificationReviewException("Invalid Reason.");
        }
        else if (decision == ClassificationReviewDecision.Unsupported)
        {
            if (correctedClassificationCode != null) throw new InvalidClassificationReviewException("Corrected code must be null when unsupported.");
            if (string.IsNullOrWhiteSpace(reason)) throw new InvalidClassificationReviewException("Reason is required.");
            reason = reason.Trim();
            if (reason.Length > 2000 || reason.Any(char.IsControl)) throw new InvalidClassificationReviewException("Invalid Reason.");
        }

        Id = id;
        ClassifiedDocumentId = classifiedDocumentId;
        Decision = decision;
        CorrectedClassificationCode = correctedClassificationCode;
        Reason = reason;
        ReviewingActorId = reviewingActorId;
        ReviewedAt = reviewedAt;
        VerifiedCapability = verifiedCapability ?? string.Empty;
        GrantedAuthorityScopeKind = grantedAuthorityScopeKind;
        GrantedAuthorityScopeIdentifier = grantedAuthorityScopeIdentifier;
        RequiredAuthorityScopeKind = requiredAuthorityScopeKind;
        RequiredAuthorityScopeIdentifier = requiredAuthorityScopeIdentifier ?? string.Empty;
        AuthorityVerificationTime = authorityVerificationTime;
    }
}
