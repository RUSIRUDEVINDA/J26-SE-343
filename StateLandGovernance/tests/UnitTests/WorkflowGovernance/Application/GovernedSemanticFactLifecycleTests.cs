namespace StateLandGovernance.UnitTests.WorkflowGovernance.Application;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Validators;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

/// <summary>
/// Proves the entire governed semantic fact lifecycle:
/// Machine candidate facts (Qwen) -> Human verification (Confirm/Correct/Unsupported) -> VerifiedFactSnapshot
/// 
/// Governance Guarantees Tested:
/// 1. Machine output is candidate-only: Never authoritative on its own.
/// 2. Human officer must verify all candidate facts before snapshot creation.
/// 3. Confirmed candidate facts preserve their machine values.
/// 4. Corrected candidate facts take the human-supplied value.
/// 5. Unsupported facts are completely excluded from the VerifiedFactSnapshot.
/// </summary>
public sealed class GovernedSemanticFactLifecycleTests
{
    private const string Sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private readonly SpyDocumentAnalysisRepository _analysisRepository;
    private readonly SpyWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly FakeTimeProvider _timeProvider;
    private readonly DateTime _fixedUtcTime;

    private readonly CompleteDocumentAnalysisCommandHandler _completeHandler;
    private readonly VerifyCandidateFactCommandHandler _verifyHandler;
    private readonly CreateVerifiedFactSnapshotCommandHandler _snapshotHandler;

    public GovernedSemanticFactLifecycleTests()
    {
        _fixedUtcTime = new DateTime(2026, 10, 11, 10, 0, 0, DateTimeKind.Utc);
        _analysisRepository = new SpyDocumentAnalysisRepository();
        _unitOfWork = new SpyWorkflowGovernanceUnitOfWork();
        _timeProvider = new FakeTimeProvider(_fixedUtcTime);

        _completeHandler = new CompleteDocumentAnalysisCommandHandler(
            _analysisRepository,
            _unitOfWork,
            new CompleteDocumentAnalysisCommandValidator(),
            _timeProvider);

        _verifyHandler = new VerifyCandidateFactCommandHandler(
            _analysisRepository,
            _unitOfWork,
            new VerifyCandidateFactCommandValidator(),
            _timeProvider);

        _snapshotHandler = new CreateVerifiedFactSnapshotCommandHandler(
            _analysisRepository,
            _unitOfWork,
            new CreateVerifiedFactSnapshotCommandValidator(),
            _timeProvider);
    }

    [Fact]
    public async Task CompleteSemanticFactLifecycle_MachineCandidates_HumanVerified_SnapshotExcludesUnsupported()
    {
        // 1. Arrange: Create DocumentAnalysis aggregate in Requested state
        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var documentId = new GovernedDocumentId(Guid.NewGuid());
        var versionId = new DocumentVersionId(Guid.NewGuid());

        var analysis = new DocumentAnalysis(
            analysisId,
            documentId,
            versionId,
            new DocumentChecksum("SHA-256", Sha256),
            documentVersionNumber: 1,
            sourceDocumentRevision: 1,
            createdAt: _fixedUtcTime.AddHours(-2));

        var runId = new AnalysisRunId(Guid.NewGuid());
        analysis.RequestRun(
            runId,
            new AnalysisModelReference("FastApi-QwenSemantic", "Qwen/Qwen2.5-7B-Instruct", "qwen-proposal-extraction-v1"),
            new[] { new AnalysisCapabilityCode("Ocr"), new AnalysisCapabilityCode("SemanticExtraction") },
            _fixedUtcTime.AddHours(-1));

        analysis.StartRun(runId, _fixedUtcTime.AddMinutes(-50));
        _analysisRepository.Seed(analysis);

        // 2. Stage 1: Machine completes run with 3 candidate facts
        var fact1Id = Guid.NewGuid(); // applicant_name
        var fact2Id = Guid.NewGuid(); // land_extent
        var fact3Id = Guid.NewGuid(); // investment_amount (hallucinated / unsupported)
        var resultId = Guid.NewGuid();

        var completeCommand = new CompleteDocumentAnalysisCommand(
            DocumentAnalysisId: analysisId.Value,
            AnalysisRunId: runId.Value,
            ExpectedRevision: analysis.Revision,
            Outcome: "OutputsProduced",
            Artifacts: new[]
            {
                new AnalysisArtifactOutputDto(Guid.NewGuid(), "OcrDocumentTranscript", "analysis-artifacts/v1/document-transcript.txt", "text/plain", "SHA-256", "hash1"),
                new AnalysisArtifactOutputDto(Guid.NewGuid(), "OcrPageTranscript", "analysis-artifacts/v1/pages/1.json", "application/json", "SHA-256", "hash2"),
                new AnalysisArtifactOutputDto(Guid.NewGuid(), "SemanticRawResponse", "analysis-artifacts/v1/semantic-extraction.json", "application/json", "SHA-256", "hash3")
            },
            ExtractedCandidateFacts: new[]
            {
                new ExtractedCandidateFactDto(
                    FactId: fact1Id,
                    FactCode: "applicant_name",
                    ValueKind: "Text",
                    CanonicalValue: "Lanka Agro Enterprises Ltd",
                    ConfidenceScore: 0.96m,
                    EvidenceArtifactId: null,
                    PageNumber: 1,
                    Excerpt: "Applicant Name: Lanka Agro Enterprises Ltd"
                ),
                new ExtractedCandidateFactDto(
                    FactId: fact2Id,
                    FactCode: "land_extent",
                    ValueKind: "Decimal",
                    CanonicalValue: "15.5",
                    ConfidenceScore: 0.88m,
                    EvidenceArtifactId: null,
                    PageNumber: 1,
                    Excerpt: "Proposed Land Extent: 15.5 acres"
                ),
                new ExtractedCandidateFactDto(
                    FactId: fact3Id,
                    FactCode: "investment_amount",
                    ValueKind: "Integer",
                    CanonicalValue: "500000000",
                    ConfidenceScore: 0.35m,
                    EvidenceArtifactId: null,
                    PageNumber: 1,
                    Excerpt: "Expected Investment: 500,000,000 LKR"
                )
            },
            ResultId: resultId
        );

        var completedDto = await _completeHandler.HandleAsync(completeCommand, CancellationToken.None);
        Assert.Equal("Completed", completedDto.Runs.Single().State);
        Assert.NotNull(completedDto.Runs.Single().Result);
        Assert.Equal(3, completedDto.Runs.Single().Result!.ExtractedCandidateFacts.Count);

        // Verify machine facts are currently UNVERIFIED candidates (no snapshot exists)
        Assert.Empty(completedDto.VerifiedFactSnapshots);

        // 3. Stage 2: Human Officer Verification with authorized role
        var officerId = Guid.NewGuid();
        var authoritySnapshot = new VerifiedAuthoritySnapshot(
            officerId,
            new[] { "FactVerifier", "FactSnapshotPublisher" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, documentId.Value.ToString("D")),
            _fixedUtcTime.AddHours(-1),
            _fixedUtcTime.AddMinutes(-5),
            _fixedUtcTime.AddHours(2));

        var authorityDto = VerifiedAuthorityContext.FromDomain(authoritySnapshot);

        // Fact 1: Officer CONFIRMS applicant_name
        var verify1Command = new VerifyCandidateFactCommand(
            DocumentAnalysisId: analysisId.Value,
            AnalysisRunResultId: resultId,
            ExtractedFactId: fact1Id,
            Decision: "Confirmed",
            CorrectedValue: null,
            Reason: null,
            VerifyingActorId: officerId,
            AuthorityContext: authorityDto,
            ExpectedAnalysisRevision: completedDto.Revision
        );
        var verified1Dto = await _verifyHandler.HandleAsync(verify1Command, CancellationToken.None);
        Assert.Equal("Confirmed", verified1Dto.Decision);

        // Fact 2: Officer CORRECTS land_extent to 12.5 (survey plan established smaller area)
        var verify2Command = new VerifyCandidateFactCommand(
            DocumentAnalysisId: analysisId.Value,
            AnalysisRunResultId: resultId,
            ExtractedFactId: fact2Id,
            Decision: "Corrected",
            CorrectedValue: new FactValueDto("Decimal", "12.5"),
            Reason: "Survey plan SP-2026-104 certifies surveyed extent as 12.5 acres, not 15.0.",
            VerifyingActorId: officerId,
            AuthorityContext: authorityDto,
            ExpectedAnalysisRevision: completedDto.Revision + 1
        );
        var verified2Dto = await _verifyHandler.HandleAsync(verify2Command, CancellationToken.None);
        Assert.Equal("Corrected", verified2Dto.Decision);

        // Fact 3: Officer marks investment_amount as UNSUPPORTED
        var verify3Command = new VerifyCandidateFactCommand(
            DocumentAnalysisId: analysisId.Value,
            AnalysisRunResultId: resultId,
            ExtractedFactId: fact3Id,
            Decision: "Unsupported",
            CorrectedValue: null,
            Reason: "Figure was preliminary projection in business plan; no audited capital commitment provided.",
            VerifyingActorId: officerId,
            AuthorityContext: authorityDto,
            ExpectedAnalysisRevision: completedDto.Revision + 2
        );
        var verified3Dto = await _verifyHandler.HandleAsync(verify3Command, CancellationToken.None);
        Assert.Equal("Unsupported", verified3Dto.Decision);

        // 4. Stage 3: Officer Publishes VerifiedFactSnapshot
        var snapshotCommand = new CreateVerifiedFactSnapshotCommand(
            DocumentAnalysisId: analysisId.Value,
            AnalysisRunResultId: resultId,
            PublishingActorId: officerId,
            AuthorityContext: authorityDto,
            ExpectedAnalysisRevision: completedDto.Revision + 3
        );

        var snapshotDto = await _snapshotHandler.HandleAsync(snapshotCommand, CancellationToken.None);

        // 5. Assert: Snapshot contains Confirmed and Corrected, EXCLUDES Unsupported!
        Assert.NotNull(snapshotDto);
        Assert.Equal(3, snapshotDto.SourceFactCount);
        Assert.Equal(2, snapshotDto.PublishedFactCount);
        Assert.Equal(1, snapshotDto.ConfirmedFactCount);
        Assert.Equal(1, snapshotDto.CorrectedFactCount);
        Assert.Equal(1, snapshotDto.UnsupportedFactCount);
        Assert.Equal(2, snapshotDto.Entries.Count);

        // Confirmed fact check: retains machine canonical value
        var confirmedFact = Assert.Single(snapshotDto.Entries, f => f.FactCode == "applicant_name");
        Assert.Equal("Lanka Agro Enterprises Ltd", confirmedFact.EffectiveValue.CanonicalValue);
        Assert.Equal("Text", confirmedFact.EffectiveValue.Kind);
        Assert.Equal("Confirmed", confirmedFact.Decision);
        Assert.Equal(officerId, confirmedFact.VerifyingActorId);

        // Corrected fact check: has human-provided corrected value
        var correctedFact = Assert.Single(snapshotDto.Entries, f => f.FactCode == "land_extent");
        Assert.Equal("12.5", correctedFact.EffectiveValue.CanonicalValue);
        Assert.Equal("Decimal", correctedFact.EffectiveValue.Kind);
        Assert.Equal("Corrected", correctedFact.Decision);
        Assert.Equal(officerId, correctedFact.VerifyingActorId);

        // CRITICAL GOVERNANCE INVARIANT:
        // Unsupported fact (investment_amount) was COMPLETELY EXCLUDED from published snapshot
        Assert.DoesNotContain(snapshotDto.Entries, f => f.FactCode == "investment_amount");
    }

    // --- Test Doubles ---

    private sealed class SpyDocumentAnalysisRepository : IDocumentAnalysisRepository
    {
        private readonly Dictionary<Guid, DocumentAnalysis> _analysesById = new();

        public void Seed(DocumentAnalysis analysis)
        {
            _analysesById[analysis.Id.Value] = analysis;
        }

        public Task<DocumentAnalysis?> GetByIdAsync(DocumentAnalysisId id, CancellationToken cancellationToken = default)
        {
            _analysesById.TryGetValue(id.Value, out var found);
            return Task.FromResult(found);
        }

        public Task<DocumentAnalysis?> GetByDocumentVersionIdAsync(DocumentVersionId versionId, CancellationToken cancellationToken = default)
        {
            var found = _analysesById.Values.FirstOrDefault(a => a.DocumentVersionId.Value == versionId.Value);
            return Task.FromResult(found);
        }

        public Task AddAsync(DocumentAnalysis analysis, CancellationToken cancellationToken = default)
        {
            Seed(analysis);
            return Task.CompletedTask;
        }

        public Task<VerifiedFactSnapshot?> GetSnapshotByIdAsync(VerifiedFactSnapshotId snapshotId, CancellationToken cancellationToken = default)
        {
            foreach (var analysis in _analysesById.Values)
            {
                var snapshot = analysis.VerifiedFactSnapshots.FirstOrDefault(s => s.Id.Value == snapshotId.Value);
                if (snapshot != null)
                {
                    return Task.FromResult<VerifiedFactSnapshot?>(snapshot);
                }
            }

            return Task.FromResult<VerifiedFactSnapshot?>(null);
        }
    }

    private sealed class SpyWorkflowGovernanceUnitOfWork : IWorkflowGovernanceUnitOfWork
    {
        public int CommitCount { get; private set; }

        public Task<int> CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTime utcNow)
        {
            _utcNow = new DateTimeOffset(utcNow, TimeSpan.Zero);
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
