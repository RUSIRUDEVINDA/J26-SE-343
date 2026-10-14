namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.Events;

using System;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

public sealed record VerifiedFactSnapshotPublished : IDomainEvent
{
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }
    public DocumentAnalysisId DocumentAnalysisId { get; }
    public GovernedDocumentId GovernedDocumentId { get; }
    public DocumentVersionId DocumentVersionId { get; }
    public string DocumentChecksumAlgorithm { get; }
    public string DocumentChecksumValue { get; }
    public AnalysisRunId AnalysisRunId { get; }
    public AnalysisRunResultId AnalysisRunResultId { get; }
    public VerifiedFactSnapshotId VerifiedFactSnapshotId { get; }
    public int RunNumber { get; }
    public AnalysisResultOutcome ResultOutcome { get; }
    public VerifiedFactSnapshotOutcome SnapshotOutcome { get; }
    public int SourceFactCount { get; }
    public int PublishedFactCount { get; }
    public int ConfirmedFactCount { get; }
    public int CorrectedFactCount { get; }
    public int UnsupportedFactCount { get; }
    public Guid PublishingActorId { get; }
    public string VerifiedCapability { get; }
    public AuthorityScopeKind GrantedAuthorityScopeKind { get; }
    public string? GrantedAuthorityScopeIdentifier { get; }
    public AuthorityScopeKind RequiredAuthorityScopeKind { get; }
    public string RequiredAuthorityScopeIdentifier { get; }
    public DateTime AuthorityVerificationTime { get; }
    public int DocumentAnalysisRevision { get; }

    public VerifiedFactSnapshotPublished(
        Guid eventId,
        DateTime occurredOn,
        DocumentAnalysisId documentAnalysisId,
        GovernedDocumentId governedDocumentId,
        DocumentVersionId documentVersionId,
        string documentChecksumAlgorithm,
        string documentChecksumValue,
        AnalysisRunId analysisRunId,
        AnalysisRunResultId analysisRunResultId,
        VerifiedFactSnapshotId verifiedFactSnapshotId,
        int runNumber,
        AnalysisResultOutcome resultOutcome,
        VerifiedFactSnapshotOutcome snapshotOutcome,
        int sourceFactCount,
        int publishedFactCount,
        int confirmedFactCount,
        int correctedFactCount,
        int unsupportedFactCount,
        Guid publishingActorId,
        string verifiedCapability,
        AuthorityScopeKind grantedAuthorityScopeKind,
        string? grantedAuthorityScopeIdentifier,
        AuthorityScopeKind requiredAuthorityScopeKind,
        string requiredAuthorityScopeIdentifier,
        DateTime authorityVerificationTime,
        int documentAnalysisRevision)
    {
        EventId = eventId;
        OccurredOn = occurredOn;
        DocumentAnalysisId = documentAnalysisId;
        GovernedDocumentId = governedDocumentId;
        DocumentVersionId = documentVersionId;
        DocumentChecksumAlgorithm = documentChecksumAlgorithm;
        DocumentChecksumValue = documentChecksumValue;
        AnalysisRunId = analysisRunId;
        AnalysisRunResultId = analysisRunResultId;
        VerifiedFactSnapshotId = verifiedFactSnapshotId;
        RunNumber = runNumber;
        ResultOutcome = resultOutcome;
        SnapshotOutcome = snapshotOutcome;
        SourceFactCount = sourceFactCount;
        PublishedFactCount = publishedFactCount;
        ConfirmedFactCount = confirmedFactCount;
        CorrectedFactCount = correctedFactCount;
        UnsupportedFactCount = unsupportedFactCount;
        PublishingActorId = publishingActorId;
        VerifiedCapability = verifiedCapability;
        GrantedAuthorityScopeKind = grantedAuthorityScopeKind;
        GrantedAuthorityScopeIdentifier = grantedAuthorityScopeIdentifier;
        RequiredAuthorityScopeKind = requiredAuthorityScopeKind;
        RequiredAuthorityScopeIdentifier = requiredAuthorityScopeIdentifier;
        AuthorityVerificationTime = authorityVerificationTime;
        DocumentAnalysisRevision = documentAnalysisRevision;
    }
}
