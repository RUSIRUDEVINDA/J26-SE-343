namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class VerifiedFactEntry
{
    public ExtractedFactId? SourceExtractedFactId { get; }
    public HumanEvidenceFact? HumanEvidence { get; }
    public string SourceKind => HumanEvidence == null ? "MachineCandidateReview" : "HumanDocumentEvidence";
    public HumanFactVerificationId VerificationId { get; }
    public FactCode FactCode { get; }
    public AnalysisFactValue EffectiveValue { get; }
    public FactVerificationDecision Decision { get; }
    public Guid VerifyingActorId { get; }
    public DateTime VerifiedAt { get; }

    internal VerifiedFactEntry(HumanEvidenceFact evidence)
    {
        HumanEvidence = evidence ?? throw new InvalidVerifiedFactSnapshotException("Human evidence is required.");
        SourceExtractedFactId = null;
        VerificationId = new HumanFactVerificationId(evidence.Id);
        FactCode = evidence.FactCode; EffectiveValue = evidence.Value;
        Decision = FactVerificationDecision.Confirmed;
        VerifyingActorId = evidence.ActorId; VerifiedAt = evidence.RecordedAtUtc;
    }

    internal VerifiedFactEntry(
        ExtractedFactId sourceExtractedFactId,
        HumanFactVerificationId verificationId,
        FactCode factCode,
        AnalysisFactValue effectiveValue,
        FactVerificationDecision decision,
        Guid verifyingActorId,
        DateTime verifiedAt)
    {
        if (sourceExtractedFactId.Value == Guid.Empty) throw new InvalidVerifiedFactSnapshotException("SourceExtractedFactId cannot be empty.");
        if (verificationId.Value == Guid.Empty) throw new InvalidVerifiedFactSnapshotException("VerificationId cannot be empty.");
        if (factCode == null) throw new InvalidVerifiedFactSnapshotException("FactCode required.");
        if (effectiveValue == null) throw new InvalidVerifiedFactSnapshotException("EffectiveValue required.");
        if (verifyingActorId == Guid.Empty) throw new InvalidVerifiedFactSnapshotException("VerifyingActorId cannot be empty.");
        if (verifiedAt.Kind != DateTimeKind.Utc) throw new InvalidVerifiedFactSnapshotException("VerifiedAt must be UTC.");

        if (decision == FactVerificationDecision.Unsupported)
            throw new InvalidVerifiedFactSnapshotException("Unsupported facts cannot be entries.");
        if (decision != FactVerificationDecision.Confirmed && decision != FactVerificationDecision.Corrected)
            throw new InvalidVerifiedFactSnapshotException("Invalid decision.");

        SourceExtractedFactId = sourceExtractedFactId;
        VerificationId = verificationId;
        FactCode = factCode;
        EffectiveValue = effectiveValue;
        Decision = decision;
        VerifyingActorId = verifyingActorId;
        VerifiedAt = verifiedAt;
    }
}
