namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using System.Collections.Generic;
using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.Events;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;

public sealed class DocumentAnalysis
{
    public DocumentAnalysisId Id { get; }
    public GovernedDocumentId GovernedDocumentId { get; }
    public DocumentVersionId DocumentVersionId { get; }
    public DocumentChecksum DocumentChecksum { get; }
    public int DocumentVersionNumber { get; }
    public int SourceDocumentRevision { get; }
    public DateTime CreatedAt { get; }
    public int Revision { get; private set; }

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private readonly List<AnalysisRun> _runs = new();
    public IReadOnlyCollection<AnalysisRun> Runs => _runs.AsReadOnly();

    private readonly List<HumanFactVerification> _verifications = new();
    public IReadOnlyCollection<HumanFactVerification> Verifications => _verifications.AsReadOnly();

    private const string RequiredFactVerificationCapability = "FactVerifier";

    public DocumentAnalysis(
        DocumentAnalysisId id,
        GovernedDocumentId governedDocumentId,
        DocumentVersionId documentVersionId,
        DocumentChecksum documentChecksum,
        int documentVersionNumber,
        int sourceDocumentRevision,
        DateTime createdAt)
    {
        if (id == default || id.Value == Guid.Empty) throw new InvalidDocumentAnalysisException("DocumentAnalysisId cannot be empty.");
        if (governedDocumentId == default || governedDocumentId.Value == Guid.Empty) throw new InvalidDocumentAnalysisException("GovernedDocumentId cannot be empty.");
        if (documentVersionId == default || documentVersionId.Value == Guid.Empty) throw new InvalidDocumentAnalysisException("DocumentVersionId cannot be empty.");
        if (documentChecksum == null) throw new InvalidDocumentAnalysisException("DocumentChecksum is required.");
        if (documentVersionNumber <= 0) throw new InvalidDocumentAnalysisException("DocumentVersionNumber must be positive.");
        if (sourceDocumentRevision <= 0) throw new InvalidDocumentAnalysisException("SourceDocumentRevision must be positive.");
        if (createdAt.Kind != DateTimeKind.Utc) throw new InvalidDocumentAnalysisException("CreatedAt must be UTC.");

        Id = id;
        GovernedDocumentId = governedDocumentId;
        DocumentVersionId = documentVersionId;
        DocumentChecksum = documentChecksum;
        DocumentVersionNumber = documentVersionNumber;
        SourceDocumentRevision = sourceDocumentRevision;
        CreatedAt = createdAt;
        Revision = 1;

        _domainEvents.Add(new DocumentAnalysisCreated(
            EventId: Guid.NewGuid(),
            OccurredOn: createdAt,
            DocumentAnalysisId: id,
            GovernedDocumentId: governedDocumentId,
            DocumentVersionId: documentVersionId,
            DocumentVersionNumber: documentVersionNumber,
            SourceDocumentRevision: sourceDocumentRevision,
            ChecksumAlgorithm: documentChecksum.Algorithm,
            ChecksumValue: documentChecksum.Value,
            DocumentAnalysisRevision: Revision
        ));
    }

    internal static int CalculateNextRunNumber(int? currentMaximum)
    {
        if (currentMaximum == null) return 1;
        if (currentMaximum <= 0) throw new DocumentAnalysisRunNumberOverflowException("Current maximum run number must be positive.");

        try
        {
            return checked(currentMaximum.Value + 1);
        }
        catch (OverflowException)
        {
            throw new DocumentAnalysisRunNumberOverflowException("Run number overflowed.");
        }
    }

    internal static int CalculateNextRevision(int currentRevision)
    {
        if (currentRevision <= 0) throw new DocumentAnalysisRevisionOverflowException("Current revision must be positive.");
        try
        {
            return checked(currentRevision + 1);
        }
        catch (OverflowException)
        {
            throw new DocumentAnalysisRevisionOverflowException("DocumentAnalysis revision overflowed.");
        }
    }

    public void RequestRun(
        AnalysisRunId analysisRunId,
        AnalysisModelReference modelReference,
        IReadOnlyCollection<AnalysisCapabilityCode> requestedCapabilities,
        DateTime requestedAt)
    {
        if (analysisRunId == default || analysisRunId.Value == Guid.Empty) throw new InvalidAnalysisRunException("AnalysisRunId cannot be empty.");
        if (modelReference == null) throw new InvalidAnalysisRunException("AnalysisModelReference cannot be null.");
        if (requestedCapabilities == null || !requestedCapabilities.Any()) throw new InvalidAnalysisRunException("Requested capabilities cannot be null or empty.");
        if (requestedAt.Kind != DateTimeKind.Utc) throw new InvalidAnalysisRunException("RequestedAt must be UTC.");

        var localCaps = requestedCapabilities.ToList();
        var distinctCaps = new HashSet<AnalysisCapabilityCode>();
        foreach (var cap in localCaps)
        {
            if (cap.Value == null) throw new InvalidAnalysisRunException("Requested capabilities contain invalid entries.");
            if (!distinctCaps.Add(cap))
            {
                throw new InvalidAnalysisRunException("Requested capabilities contain duplicates.");
            }
        }
        
        var normalizedCapabilities = distinctCaps
            .OrderBy(c => c.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingRun = _runs.FirstOrDefault(r => r.Id.Value == analysisRunId.Value);
        if (existingRun != null)
        {
            var isSameModel = existingRun.ModelReference.Equals(modelReference);

            var isSameTime = existingRun.RequestedAt == requestedAt;
            var isSameCaps = existingRun.RequestedCapabilities.SequenceEqual(normalizedCapabilities);

            if (isSameModel && isSameTime && isSameCaps)
            {
                throw new DuplicateAnalysisRunException("AnalysisRunId already exists with identical request data.");
            }

            throw new ConflictingAnalysisRunException("AnalysisRunId already exists with differing request data.");
        }

        var nextRunNumber = CalculateNextRunNumber(_runs.Count > 0 ? _runs.Max(r => r.RunNumber) : null);
        var nextRevision = CalculateNextRevision(Revision);

        var run = new AnalysisRun(
            analysisRunId,
            nextRunNumber,
            DocumentVersionId,
            DocumentChecksum,
            modelReference,
            normalizedCapabilities,
            requestedAt
        );

        var evt = new AnalysisRunRequested(
            Guid.NewGuid(),
            requestedAt,
            Id,
            analysisRunId,
            GovernedDocumentId,
            DocumentVersionId,
            DocumentChecksum.Algorithm,
            DocumentChecksum.Value,
            nextRunNumber,
            modelReference.Provider,
            modelReference.ModelName,
            modelReference.ModelVersion,
            normalizedCapabilities.Select(c => c.Value).ToArray(),
            nextRevision
        );

        _runs.Add(run);
        Revision = nextRevision;
        _domainEvents.Add(evt);
    }

    public void StartRun(AnalysisRunId analysisRunId, DateTime startedAt)
    {
        if (analysisRunId == default || analysisRunId.Value == Guid.Empty) throw new InvalidAnalysisRunTransitionException("AnalysisRunId cannot be empty.");
        
        var run = _runs.FirstOrDefault(r => r.Id.Value == analysisRunId.Value);
        if (run == null) throw new AnalysisRunNotFoundException($"AnalysisRun with ID {analysisRunId.Value} was not found.");

        var nextRevision = CalculateNextRevision(Revision);

        var evt = new AnalysisRunStarted(
            Guid.NewGuid(),
            startedAt,
            Id,
            analysisRunId,
            GovernedDocumentId,
            DocumentVersionId,
            DocumentChecksum.Algorithm,
            DocumentChecksum.Value,
            run.RunNumber,
            nextRevision
        );

        run.MarkStarted(startedAt);

        Revision = nextRevision;
        _domainEvents.Add(evt);
    }

    public void FailRun(AnalysisRunId analysisRunId, AnalysisRunFailure failure, DateTime failedAt)
    {
        if (analysisRunId == default || analysisRunId.Value == Guid.Empty) throw new InvalidAnalysisRunTransitionException("AnalysisRunId cannot be empty.");
        if (failure == null) throw new InvalidAnalysisRunTransitionException("Failure cannot be null.");
        
        var run = _runs.FirstOrDefault(r => r.Id.Value == analysisRunId.Value);
        if (run == null) throw new AnalysisRunNotFoundException($"AnalysisRun with ID {analysisRunId.Value} was not found.");

        var nextRevision = CalculateNextRevision(Revision);

        var evt = new AnalysisRunFailed(
            Guid.NewGuid(),
            failedAt,
            Id,
            analysisRunId,
            GovernedDocumentId,
            DocumentVersionId,
            DocumentChecksum.Algorithm,
            DocumentChecksum.Value,
            run.RunNumber,
            failure.Code,
            failure.Description,
            nextRevision
        );

        run.MarkFailed(failure, failedAt);

        Revision = nextRevision;
        _domainEvents.Add(evt);
    }

    public void SupersedeRun(AnalysisRunId analysisRunId, string supersessionReason, DateTime supersededAt)
    {
        if (analysisRunId == default || analysisRunId.Value == Guid.Empty) throw new InvalidAnalysisRunTransitionException("AnalysisRunId cannot be empty.");
        if (string.IsNullOrWhiteSpace(supersessionReason)) throw new InvalidAnalysisRunTransitionException("Supersession reason cannot be blank.");
        
        var trimmedReason = supersessionReason.Trim();
        var run = _runs.FirstOrDefault(r => r.Id.Value == analysisRunId.Value);
        if (run == null) throw new AnalysisRunNotFoundException($"AnalysisRun with ID {analysisRunId.Value} was not found.");

        var nextRevision = CalculateNextRevision(Revision);

        var evt = new AnalysisRunSuperseded(
            Guid.NewGuid(),
            supersededAt,
            Id,
            analysisRunId,
            GovernedDocumentId,
            DocumentVersionId,
            DocumentChecksum.Algorithm,
            DocumentChecksum.Value,
            run.RunNumber,
            trimmedReason,
            nextRevision
        );

        run.MarkSuperseded(trimmedReason, supersededAt);

        Revision = nextRevision;
        _domainEvents.Add(evt);
    }

    public void CompleteRun(
        AnalysisRunId analysisRunId,
        AnalysisRunResultId resultId,
        AnalysisResultOutcome outcome,
        IReadOnlyCollection<AnalysisResultArtifactReference> artifacts,
        IReadOnlyCollection<ExtractedFactInput> facts,
        DateTime completedAt)
    {
        if (analysisRunId.Value == Guid.Empty) throw new InvalidAnalysisRunTransitionException("AnalysisRunId empty.");
        var run = _runs.FirstOrDefault(r => r.Id.Value == analysisRunId.Value);
        if (run == null) throw new AnalysisRunNotFoundException($"Run {analysisRunId.Value} not found.");
        
        if (run.State != AnalysisRunState.Running) throw new InvalidAnalysisRunTransitionException("Run is not running.");
        
        if (completedAt.Kind != DateTimeKind.Utc) throw new InvalidAnalysisRunTransitionException("completedAt must be UTC.");
        if (run.StartedAt.HasValue && completedAt < run.StartedAt.Value) throw new InvalidAnalysisRunTransitionException("Chronology violation.");
        
        foreach (var r in _runs)
        {
            if (r.Result != null && r.Result.Id.Value == resultId.Value)
            {
                throw new DuplicateAnalysisRunResultException("ResultId already exists.");
            }
        }

        var result = new AnalysisRunResult(
            resultId,
            run.Id,
            run.DocumentVersionId,
            run.DocumentChecksum,
            run.ModelReference,
            run.RequestedCapabilities,
            outcome,
            artifacts,
            facts,
            completedAt
        );

        var nextRevision = CalculateNextRevision(Revision);

        var evt = new AnalysisRunCompleted(
            Guid.NewGuid(),
            completedAt,
            Id,
            analysisRunId,
            resultId,
            GovernedDocumentId,
            DocumentVersionId,
            DocumentChecksum.Algorithm,
            DocumentChecksum.Value,
            run.RunNumber,
            run.ModelReference.Provider,
            run.ModelReference.ModelName,
            run.ModelReference.ModelVersion,
            result.RequestedCapabilities.Select(c => c.Value),
            result.Outcome,
            result.Artifacts.Count,
            result.ExtractedFacts.Count,
            nextRevision
        );

        run.MarkCompleted(result, completedAt);
        Revision = nextRevision;
        _domainEvents.Add(evt);
    }

    public void RecordFactVerification(
        HumanFactVerificationId verificationId,
        AnalysisRunResultId analysisRunResultId,
        ExtractedFactId extractedFactId,
        FactVerificationDecision decision,
        AnalysisFactValue? correctedValue,
        string? reason,
        Guid verifyingActorId,
        DateTime verifiedAt,
        VerifiedAuthoritySnapshot authoritySnapshot)
    {
        if (verificationId.Value == Guid.Empty) throw new InvalidFactVerificationException("Verification ID cannot be empty.");
        if (analysisRunResultId.Value == Guid.Empty) throw new InvalidFactVerificationException("AnalysisRunResultId cannot be empty.");
        if (extractedFactId.Value == Guid.Empty) throw new InvalidFactVerificationException("ExtractedFactId cannot be empty.");
        if (authoritySnapshot == null) throw new MissingVerifiedAuthorityException("Authority snapshot is required.");
        if (verifiedAt.Kind != DateTimeKind.Utc) throw new InvalidFactVerificationException("VerifiedAt must be UTC.");

        AnalysisRun? targetRun = null;
        AnalysisRunResult? targetResult = null;
        foreach (var run in _runs)
        {
            if (run.Result?.Id.Value == analysisRunResultId.Value)
            {
                targetRun = run;
                targetResult = run.Result;
                break;
            }
        }

        if (targetRun == null || targetResult == null)
            throw new AnalysisRunResultNotFoundException($"AnalysisRunResult {analysisRunResultId.Value} not found.");

        var targetFact = targetResult.ExtractedFacts.FirstOrDefault(f => f.Id.Value == extractedFactId.Value);
        if (targetFact == null)
            throw new ExtractedFactNotFoundException($"ExtractedFact {extractedFactId.Value} not found in result {analysisRunResultId.Value}.");

        if (verifiedAt < targetRun.CompletedAt!.Value)
            throw new InvalidFactVerificationException("VerifiedAt cannot be before run completion time.");

        if (verifiedAt < authoritySnapshot.VerificationTime)
            throw new InvalidFactVerificationException("VerifiedAt cannot be before authority verification time.");

        if (_runs.Any(r => r.RunNumber > targetRun.RunNumber && r.State == AnalysisRunState.Completed))
            throw new StaleAnalysisResultException("A newer completed run makes this result stale.");

        var requiredScope = new AuthorityScope(AuthorityScopeKind.GovernedDocument, GovernedDocumentId.Value.ToString("D"));
        authoritySnapshot.EnsureAuthorizes(verifyingActorId, RequiredFactVerificationCapability, requiredScope, verifiedAt);

        var candidate = new HumanFactVerification(
            verificationId,
            Id,
            targetRun.Id,
            analysisRunResultId,
            extractedFactId,
            DocumentVersionId,
            DocumentChecksum,
            targetRun.RunNumber,
            targetRun.ModelReference,
            targetFact.FactCode,
            targetFact.FactValue,
            decision,
            correctedValue,
            reason,
            verifiedAt,
            verifyingActorId,
            RequiredFactVerificationCapability,
            authoritySnapshot.Scope.Kind,
            authoritySnapshot.Scope.TargetIdentifier,
            requiredScope.Kind,
            requiredScope.TargetIdentifier!,
            authoritySnapshot.ValidFrom,
            authoritySnapshot.VerificationTime,
            authoritySnapshot.ValidUntil
        );

        foreach (var v in _verifications)
        {
            if (v.Id.Value == verificationId.Value)
            {
                bool isExactMatch = 
                    v.AnalysisRunResultId.Value == analysisRunResultId.Value &&
                    v.ExtractedFactId.Value == extractedFactId.Value &&
                    v.Decision == decision &&
                    (v.CorrectedValue == null && correctedValue == null || v.CorrectedValue != null && correctedValue != null && string.Equals(v.CorrectedValue.CanonicalValue, correctedValue.CanonicalValue, StringComparison.Ordinal) && v.CorrectedValue.Kind == correctedValue.Kind) &&
                    string.Equals(v.Reason, candidate.Reason, StringComparison.Ordinal) &&
                    v.VerifyingActorId == verifyingActorId &&
                    v.GrantedAuthorityScopeKind == authoritySnapshot.Scope.Kind &&
                    string.Equals(v.GrantedAuthorityScopeIdentifier, authoritySnapshot.Scope.TargetIdentifier, StringComparison.Ordinal) &&
                    v.AuthorityVerificationTime == authoritySnapshot.VerificationTime &&
                    v.AuthorityValidFrom == authoritySnapshot.ValidFrom &&
                    v.AuthorityValidUntil == authoritySnapshot.ValidUntil &&
                    v.VerifiedAt == verifiedAt;

                if (isExactMatch)
                {
                    throw new DuplicateFactVerificationException("Duplicate verification ID with identical canonical data.");
                }
                else
                {
                    throw new ConflictingFactVerificationException("Verification ID already exists with conflicting canonical data.");
                }
            }

            if (v.AnalysisRunResultId.Value == analysisRunResultId.Value && v.ExtractedFactId.Value == extractedFactId.Value)
            {
                throw new FactAlreadyVerifiedException("Fact is already verified in this result.");
            }
        }

        var nextRevision = CalculateNextRevision(Revision);

        var evt = new HumanFactVerificationRecorded(
            Guid.NewGuid(),
            verifiedAt,
            Id,
            targetRun.Id,
            analysisRunResultId,
            verificationId,
            extractedFactId,
            targetFact.FactCode.Value,
            decision,
            verifyingActorId,
            RequiredFactVerificationCapability,
            authoritySnapshot.Scope.Kind,
            authoritySnapshot.Scope.TargetIdentifier,
            requiredScope.Kind,
            requiredScope.TargetIdentifier!,
            authoritySnapshot.VerificationTime,
            DocumentVersionId,
            DocumentChecksum.Algorithm,
            DocumentChecksum.Value,
            targetRun.RunNumber,
            nextRevision
        );

        _verifications.Add(candidate);
        Revision = nextRevision;
        _domainEvents.Add(evt);
    }
}

