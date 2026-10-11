namespace StateLandGovernance.WorkflowGovernance.Application.Mappings;

using System;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

public static class FactVerificationMapper
{
    public static HumanFactVerificationDto ToDto(HumanFactVerification verification)
    {
        if (verification == null)
        {
            throw new ArgumentNullException(nameof(verification));
        }

        return new HumanFactVerificationDto(
            Id: verification.Id.Value,
            DocumentAnalysisId: verification.DocumentAnalysisId.Value,
            AnalysisRunId: verification.AnalysisRunId.Value,
            AnalysisRunResultId: verification.AnalysisRunResultId.Value,
            ExtractedFactId: verification.ExtractedFactId.Value,
            DocumentVersionId: verification.DocumentVersionId.Value,
            RunNumber: verification.RunNumber,
            FactCode: verification.FactCode.Value,
            OriginalMachineValue: ToDto(verification.OriginalValue),
            Decision: verification.Decision.ToString(),
            CorrectedValue: verification.CorrectedValue != null ? ToDto(verification.CorrectedValue) : null,
            Reason: verification.Reason,
            VerifiedAt: verification.VerifiedAt,
            VerifyingActorId: verification.VerifyingActorId,
            VerifiedCapability: verification.VerifiedCapability,
            GrantedAuthorityScopeKind: verification.GrantedAuthorityScopeKind.ToString(),
            GrantedAuthorityScopeIdentifier: verification.GrantedAuthorityScopeIdentifier
        );
    }

    public static VerifiedFactSnapshotDto ToDto(VerifiedFactSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var entries = snapshot.Entries
            .Select(ToDto)
            .ToList();

        return new VerifiedFactSnapshotDto(
            Id: snapshot.Id.Value,
            DocumentAnalysisId: snapshot.DocumentAnalysisId.Value,
            GovernedDocumentId: snapshot.GovernedDocumentId.Value,
            DocumentVersionId: snapshot.DocumentVersionId.Value,
            ChecksumAlgorithm: snapshot.DocumentChecksum.Algorithm,
            ChecksumValue: snapshot.DocumentChecksum.Value,
            AnalysisRunId: snapshot.AnalysisRunId.Value,
            AnalysisRunResultId: snapshot.AnalysisRunResultId.Value,
            RunNumber: snapshot.RunNumber,
            ResultOutcome: snapshot.ResultOutcome.ToString(),
            SnapshotOutcome: snapshot.SnapshotOutcome.ToString(),
            Entries: entries,
            SourceFactCount: snapshot.SourceFactCount,
            PublishedFactCount: snapshot.PublishedFactCount,
            ConfirmedFactCount: snapshot.ConfirmedFactCount,
            CorrectedFactCount: snapshot.CorrectedFactCount,
            UnsupportedFactCount: snapshot.UnsupportedFactCount,
            PublishedAt: snapshot.PublishedAt,
            PublishingActorId: snapshot.PublishingActorId
        );
    }

    public static VerifiedFactEntryDto ToDto(VerifiedFactEntry entry)
    {
        if (entry == null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        return new VerifiedFactEntryDto(
            SourceExtractedFactId: entry.SourceExtractedFactId.Value,
            VerificationId: entry.VerificationId.Value,
            FactCode: entry.FactCode.Value,
            EffectiveValue: ToDto(entry.EffectiveValue),
            Decision: entry.Decision.ToString(),
            VerifyingActorId: entry.VerifyingActorId,
            VerifiedAt: entry.VerifiedAt
        );
    }

    public static FactValueDto ToDto(AnalysisFactValue value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return new FactValueDto(
            Kind: value.Kind.ToString(),
            CanonicalValue: value.CanonicalValue
        );
    }

    public static AnalysisFactValue ToDomain(FactValueDto dto)
    {
        if (dto == null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        var kind = Enum.Parse<AnalysisFactValueKind>(dto.Kind, ignoreCase: true);
        return new AnalysisFactValue(kind, dto.CanonicalValue);
    }
}
