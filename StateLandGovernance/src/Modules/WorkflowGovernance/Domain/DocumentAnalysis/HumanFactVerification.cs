namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class HumanFactVerification
{
    public HumanFactVerificationId Id { get; }
    public DocumentAnalysisId DocumentAnalysisId { get; }
    public AnalysisRunId AnalysisRunId { get; }
    public AnalysisRunResultId AnalysisRunResultId { get; }
    public ExtractedFactId ExtractedFactId { get; }
    public DocumentVersionId DocumentVersionId { get; }
    public DocumentChecksum DocumentChecksum { get; }
    public int RunNumber { get; }
    public AnalysisModelReference ModelReference { get; }
    public FactCode FactCode { get; }
    public AnalysisFactValue OriginalValue { get; }
    public FactVerificationDecision Decision { get; }
    public AnalysisFactValue? CorrectedValue { get; }
    public string? Reason { get; }
    public DateTime VerifiedAt { get; }
    public Guid VerifyingActorId { get; }
    public string VerifiedCapability { get; }
    public AuthorityScopeKind GrantedAuthorityScopeKind { get; }
    public string? GrantedAuthorityScopeIdentifier { get; }
    public AuthorityScopeKind RequiredAuthorityScopeKind { get; }
    public string RequiredAuthorityScopeIdentifier { get; }
    public DateTime AuthorityValidFrom { get; }
    public DateTime AuthorityVerificationTime { get; }
    public DateTime AuthorityValidUntil { get; }

    internal HumanFactVerification(
        HumanFactVerificationId id,
        DocumentAnalysisId documentAnalysisId,
        AnalysisRunId analysisRunId,
        AnalysisRunResultId analysisRunResultId,
        ExtractedFactId extractedFactId,
        DocumentVersionId documentVersionId,
        DocumentChecksum documentChecksum,
        int runNumber,
        AnalysisModelReference modelReference,
        FactCode factCode,
        AnalysisFactValue originalValue,
        FactVerificationDecision decision,
        AnalysisFactValue? correctedValue,
        string? reason,
        DateTime verifiedAt,
        Guid verifyingActorId,
        string verifiedCapability,
        AuthorityScopeKind grantedAuthorityScopeKind,
        string? grantedAuthorityScopeIdentifier,
        AuthorityScopeKind requiredAuthorityScopeKind,
        string requiredAuthorityScopeIdentifier,
        DateTime authorityValidFrom,
        DateTime authorityVerificationTime,
        DateTime authorityValidUntil)
    {
        if (id.Value == Guid.Empty) throw new InvalidFactVerificationException("Verification ID is empty.");
        if (!Enum.IsDefined(typeof(FactVerificationDecision), decision)) throw new InvalidFactVerificationException("Undefined decision.");
        if (verifiedAt.Kind != DateTimeKind.Utc) throw new InvalidFactVerificationException("VerifiedAt must be UTC.");

        if (decision == FactVerificationDecision.Confirmed)
        {
            if (correctedValue != null) throw new InvalidFactVerificationException("Confirmed verification cannot have a corrected value.");
            if (reason != null) throw new InvalidFactVerificationException("Confirmed verification cannot have a reason.");
        }
        else if (decision == FactVerificationDecision.Corrected)
        {
            if (correctedValue == null) throw new InvalidFactVerificationException("Corrected verification must have a corrected value.");
            if (correctedValue.Kind != originalValue.Kind) throw new InvalidFactVerificationException("Corrected value kind must match original value kind.");
            if (string.Equals(correctedValue.CanonicalValue, originalValue.CanonicalValue, StringComparison.Ordinal)) throw new InvalidFactVerificationException("Corrected canonical value must differ from original.");
            if (string.IsNullOrWhiteSpace(reason)) throw new InvalidFactVerificationException("Corrected verification must have a reason.");
        }
        else if (decision == FactVerificationDecision.Unsupported)
        {
            if (correctedValue != null) throw new InvalidFactVerificationException("Unsupported verification cannot have a corrected value.");
            if (string.IsNullOrWhiteSpace(reason)) throw new InvalidFactVerificationException("Unsupported verification must have a reason.");
        }

        string? canonicalReason = null;
        if (reason != null)
        {
            canonicalReason = reason.Trim();
            if (canonicalReason.Length > 500) throw new InvalidFactVerificationException("Reason cannot exceed 500 characters.");
            foreach (char c in canonicalReason)
            {
                if (char.IsControl(c)) throw new InvalidFactVerificationException("Reason cannot contain control characters.");
            }
        }

        Id = id;
        DocumentAnalysisId = documentAnalysisId;
        AnalysisRunId = analysisRunId;
        AnalysisRunResultId = analysisRunResultId;
        ExtractedFactId = extractedFactId;
        DocumentVersionId = documentVersionId;
        DocumentChecksum = documentChecksum;
        RunNumber = runNumber;
        ModelReference = modelReference;
        FactCode = factCode;
        OriginalValue = originalValue;
        Decision = decision;
        CorrectedValue = correctedValue;
        Reason = canonicalReason;
        VerifiedAt = verifiedAt;
        VerifyingActorId = verifyingActorId;
        VerifiedCapability = verifiedCapability;
        GrantedAuthorityScopeKind = grantedAuthorityScopeKind;
        GrantedAuthorityScopeIdentifier = grantedAuthorityScopeIdentifier;
        RequiredAuthorityScopeKind = requiredAuthorityScopeKind;
        RequiredAuthorityScopeIdentifier = requiredAuthorityScopeIdentifier;
        AuthorityValidFrom = authorityValidFrom;
        AuthorityVerificationTime = authorityVerificationTime;
        AuthorityValidUntil = authorityValidUntil;
    }
}
