namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class VerifiedFactSnapshot
{
    public VerifiedFactSnapshotId Id { get; }
    public DocumentAnalysisId DocumentAnalysisId { get; }
    public GovernedDocumentId GovernedDocumentId { get; }
    public DocumentVersionId DocumentVersionId { get; }
    public DocumentChecksum DocumentChecksum { get; }
    public AnalysisRunId AnalysisRunId { get; }
    public AnalysisRunResultId AnalysisRunResultId { get; }
    public int RunNumber { get; }
    public AnalysisModelReference ModelReference { get; }
    
    private readonly List<AnalysisCapabilityCode> _requestedCapabilities;
    public IReadOnlyCollection<AnalysisCapabilityCode> RequestedCapabilities => _requestedCapabilities.AsReadOnly();
    
    public AnalysisResultOutcome ResultOutcome { get; }
    public VerifiedFactSnapshotOutcome SnapshotOutcome { get; }
    
    private readonly List<VerifiedFactEntry> _entries;
    public IReadOnlyCollection<VerifiedFactEntry> Entries => _entries.AsReadOnly();
    
    public int SourceFactCount { get; }
    public int PublishedFactCount { get; }
    public int ConfirmedFactCount { get; }
    public int CorrectedFactCount { get; }
    public int UnsupportedFactCount { get; }
    
    public DateTime PublishedAt { get; }
    public Guid PublishingActorId { get; }
    public string VerifiedCapability { get; }
    public AuthorityScopeKind GrantedAuthorityScopeKind { get; }
    public string? GrantedAuthorityScopeIdentifier { get; }
    public AuthorityScopeKind RequiredAuthorityScopeKind { get; }
    public string RequiredAuthorityScopeIdentifier { get; }
    public DateTime AuthorityValidFrom { get; }
    public DateTime AuthorityVerificationTime { get; }
    public DateTime AuthorityValidUntil { get; }

    internal VerifiedFactSnapshot(
        VerifiedFactSnapshotId id,
        DocumentAnalysisId documentAnalysisId,
        GovernedDocumentId governedDocumentId,
        DocumentVersionId documentVersionId,
        DocumentChecksum documentChecksum,
        AnalysisRunId analysisRunId,
        AnalysisRunResultId analysisRunResultId,
        int runNumber,
        AnalysisModelReference modelReference,
        IReadOnlyCollection<AnalysisCapabilityCode> requestedCapabilities,
        AnalysisResultOutcome resultOutcome,
        VerifiedFactSnapshotOutcome snapshotOutcome,
        IReadOnlyCollection<VerifiedFactEntry> entries,
        int sourceFactCount,
        int publishedFactCount,
        int confirmedFactCount,
        int correctedFactCount,
        int unsupportedFactCount,
        DateTime publishedAt,
        Guid publishingActorId,
        string verifiedCapability,
        AuthorityScopeKind grantedAuthorityScopeKind,
        string? grantedAuthorityScopeIdentifier,
        AuthorityScopeKind requiredAuthorityScopeKind,
        string requiredAuthorityScopeIdentifier,
        DateTime authorityValidFrom,
        DateTime authorityVerificationTime,
        DateTime authorityValidUntil)
    {
        if (id.Value == Guid.Empty) throw new InvalidVerifiedFactSnapshotException("Snapshot ID cannot be empty.");
        if (sourceFactCount < 0 || publishedFactCount < 0 || confirmedFactCount < 0 || correctedFactCount < 0 || unsupportedFactCount < 0)
            throw new InvalidVerifiedFactSnapshotException("Counts must be non-negative.");

        if (publishedFactCount != confirmedFactCount + correctedFactCount)
            throw new InvalidVerifiedFactSnapshotException("Published count mismatch.");
        if (sourceFactCount != confirmedFactCount + correctedFactCount + unsupportedFactCount)
            throw new InvalidVerifiedFactSnapshotException("Source count mismatch.");
        if (entries.Count != publishedFactCount)
            throw new InvalidVerifiedFactSnapshotException("Entries count mismatch.");

        if (snapshotOutcome == VerifiedFactSnapshotOutcome.NoFindings && (sourceFactCount > 0 || publishedFactCount > 0))
            throw new InvalidVerifiedFactSnapshotException("NoFindings requires zero source and published facts.");
        if (snapshotOutcome == VerifiedFactSnapshotOutcome.VerifiedFacts && publishedFactCount == 0)
            throw new InvalidVerifiedFactSnapshotException("VerifiedFacts requires at least one published fact.");
        if (snapshotOutcome == VerifiedFactSnapshotOutcome.NoSupportedFacts && publishedFactCount > 0)
            throw new InvalidVerifiedFactSnapshotException("NoSupportedFacts requires zero published facts.");

        if (publishedAt.Kind != DateTimeKind.Utc) throw new InvalidVerifiedFactSnapshotException("PublishedAt must be UTC.");
        if (publishingActorId == Guid.Empty) throw new InvalidVerifiedFactSnapshotException("PublishingActorId cannot be empty.");
        if (verifiedCapability != "FactSnapshotPublisher") throw new InvalidVerifiedFactSnapshotException("VerifiedCapability must be FactSnapshotPublisher.");
        
        if (requiredAuthorityScopeKind != AuthorityScopeKind.GovernedDocument) throw new InvalidVerifiedFactSnapshotException("Required scope must be GovernedDocument.");
        if (string.IsNullOrWhiteSpace(requiredAuthorityScopeIdentifier)) throw new InvalidVerifiedFactSnapshotException("Required scope identifier cannot be empty.");
        if (requiredAuthorityScopeIdentifier != governedDocumentId.Value.ToString("D")) throw new InvalidVerifiedFactSnapshotException("Required scope identifier must match GovernedDocumentId.");

        if (authorityValidFrom.Kind != DateTimeKind.Utc || authorityVerificationTime.Kind != DateTimeKind.Utc || authorityValidUntil.Kind != DateTimeKind.Utc)
            throw new InvalidVerifiedFactSnapshotException("Authority timestamps must be UTC.");
        if (authorityValidFrom > authorityValidUntil || authorityVerificationTime < authorityValidFrom || authorityVerificationTime > authorityValidUntil)
            throw new InvalidVerifiedFactSnapshotException("Authority timestamps chronologically invalid.");


        Id = id;
        DocumentAnalysisId = documentAnalysisId;
        GovernedDocumentId = governedDocumentId;
        DocumentVersionId = documentVersionId;
        DocumentChecksum = documentChecksum;
        AnalysisRunId = analysisRunId;
        AnalysisRunResultId = analysisRunResultId;
        RunNumber = runNumber;
        ModelReference = modelReference;
        _requestedCapabilities = new List<AnalysisCapabilityCode>(requestedCapabilities);
        ResultOutcome = resultOutcome;
        SnapshotOutcome = snapshotOutcome;
        _entries = new List<VerifiedFactEntry>(entries);
        SourceFactCount = sourceFactCount;
        PublishedFactCount = publishedFactCount;
        ConfirmedFactCount = confirmedFactCount;
        CorrectedFactCount = correctedFactCount;
        UnsupportedFactCount = unsupportedFactCount;
        PublishedAt = publishedAt;
        PublishingActorId = publishingActorId;
        VerifiedCapability = verifiedCapability;
        GrantedAuthorityScopeKind = grantedAuthorityScopeKind;
        GrantedAuthorityScopeIdentifier = grantedAuthorityScopeIdentifier;
        RequiredAuthorityScopeKind = requiredAuthorityScopeKind;
        RequiredAuthorityScopeIdentifier = requiredAuthorityScopeIdentifier;
        AuthorityValidFrom = authorityValidFrom;
        AuthorityVerificationTime = authorityVerificationTime;
        AuthorityValidUntil = authorityValidUntil;
    }
    public bool IsCanonicallyEquivalentTo(VerifiedFactSnapshot other)
    {
        if (other == null) return false;

        if (DocumentAnalysisId.Value != other.DocumentAnalysisId.Value ||
            GovernedDocumentId.Value != other.GovernedDocumentId.Value ||
            DocumentVersionId.Value != other.DocumentVersionId.Value ||
            DocumentChecksum.Algorithm != other.DocumentChecksum.Algorithm ||
            DocumentChecksum.Value != other.DocumentChecksum.Value ||
            AnalysisRunId.Value != other.AnalysisRunId.Value ||
            AnalysisRunResultId.Value != other.AnalysisRunResultId.Value ||
            RunNumber != other.RunNumber ||
            ModelReference.Provider != other.ModelReference.Provider ||
            ModelReference.ModelName != other.ModelReference.ModelName ||
            ModelReference.ModelVersion != other.ModelReference.ModelVersion ||
            RequestedCapabilities.Count != other.RequestedCapabilities.Count ||
            ResultOutcome != other.ResultOutcome ||
            SnapshotOutcome != other.SnapshotOutcome ||
            SourceFactCount != other.SourceFactCount ||
            PublishedFactCount != other.PublishedFactCount ||
            ConfirmedFactCount != other.ConfirmedFactCount ||
            CorrectedFactCount != other.CorrectedFactCount ||
            UnsupportedFactCount != other.UnsupportedFactCount ||
            PublishedAt != other.PublishedAt ||
            PublishingActorId != other.PublishingActorId ||
            VerifiedCapability != other.VerifiedCapability ||
            GrantedAuthorityScopeKind != other.GrantedAuthorityScopeKind ||
            GrantedAuthorityScopeIdentifier != other.GrantedAuthorityScopeIdentifier ||
            RequiredAuthorityScopeKind != other.RequiredAuthorityScopeKind ||
            RequiredAuthorityScopeIdentifier != other.RequiredAuthorityScopeIdentifier ||
            AuthorityValidFrom != other.AuthorityValidFrom ||
            AuthorityVerificationTime != other.AuthorityVerificationTime ||
            AuthorityValidUntil != other.AuthorityValidUntil ||
            Entries.Count != other.Entries.Count)
        {
            return false;
        }

        var caps1 = new List<string>(RequestedCapabilities.Count);
        foreach (var c in RequestedCapabilities) caps1.Add(c.Value);
        caps1.Sort(StringComparer.Ordinal);

        var caps2 = new List<string>(other.RequestedCapabilities.Count);
        foreach (var c in other.RequestedCapabilities) caps2.Add(c.Value);
        caps2.Sort(StringComparer.Ordinal);

        for (int i = 0; i < caps1.Count; i++)
        {
            if (caps1[i] != caps2[i]) return false;
        }

        using var e1 = Entries.GetEnumerator();
        using var e2 = other.Entries.GetEnumerator();
        while (e1.MoveNext() && e2.MoveNext())
        {
            var cur1 = e1.Current;
            var cur2 = e2.Current;
            if (cur1.SourceExtractedFactId.Value != cur2.SourceExtractedFactId.Value ||
                cur1.VerificationId.Value != cur2.VerificationId.Value ||
                cur1.FactCode.Value != cur2.FactCode.Value ||
                cur1.EffectiveValue.CanonicalValue != cur2.EffectiveValue.CanonicalValue ||
                cur1.EffectiveValue.Kind != cur2.EffectiveValue.Kind ||
                cur1.Decision != cur2.Decision ||
                cur1.VerifyingActorId != cur2.VerifyingActorId ||
                cur1.VerifiedAt != cur2.VerifiedAt)
            {
                return false;
            }
        }

        return true;
    }
}
