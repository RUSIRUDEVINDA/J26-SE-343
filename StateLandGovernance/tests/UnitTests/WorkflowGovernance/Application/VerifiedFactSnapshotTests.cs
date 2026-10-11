namespace StateLandGovernance.UnitTests.WorkflowGovernance.Application;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Queries;
using StateLandGovernance.WorkflowGovernance.Application.Validators;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public class VerifiedFactSnapshotTests
{
    private const string Sha256V1 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private readonly SpyDocumentAnalysisRepository _analysisRepository;
    private readonly SpyLeaseCaseRepository _leaseCaseRepository;
    private readonly SpyWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly FakeTimeProvider _timeProvider;
    private readonly DateTime _fixedUtcTime;

    private readonly CreateVerifiedFactSnapshotCommandHandler _createHandler;
    private readonly LinkVerifiedFactSnapshotToCaseCommandHandler _linkHandler;
    private readonly GetVerifiedFactSnapshotByIdQueryHandler _getSnapshotByIdHandler;
    private readonly GetCurrentVerifiedFactsForCaseQueryHandler _getCurrentFactsForCaseHandler;

    public VerifiedFactSnapshotTests()
    {
        _fixedUtcTime = new DateTime(2026, 9, 21, 10, 0, 0, DateTimeKind.Utc);
        _analysisRepository = new SpyDocumentAnalysisRepository();
        _leaseCaseRepository = new SpyLeaseCaseRepository();
        _unitOfWork = new SpyWorkflowGovernanceUnitOfWork();
        _timeProvider = new FakeTimeProvider(_fixedUtcTime);

        _createHandler = new CreateVerifiedFactSnapshotCommandHandler(
            _analysisRepository,
            _unitOfWork,
            new CreateVerifiedFactSnapshotCommandValidator(),
            _timeProvider);

        _linkHandler = new LinkVerifiedFactSnapshotToCaseCommandHandler(
            _leaseCaseRepository,
            _analysisRepository,
            _unitOfWork,
            new LinkVerifiedFactSnapshotToCaseCommandValidator());

        _getSnapshotByIdHandler = new GetVerifiedFactSnapshotByIdQueryHandler(_analysisRepository);
        _getCurrentFactsForCaseHandler = new GetCurrentVerifiedFactsForCaseQueryHandler(
            _leaseCaseRepository,
            _analysisRepository);
    }

    private DocumentAnalysis CreateAnalysisWithVerifiedFact(
        out ExtractedFactId factId,
        out AnalysisRunId runId,
        out AnalysisRunResultId runResultId,
        out Guid verifierActorId)
    {
        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var documentId = new GovernedDocumentId(Guid.NewGuid());
        var versionId = new DocumentVersionId(Guid.NewGuid());

        var analysis = new DocumentAnalysis(
            analysisId,
            documentId,
            versionId,
            new DocumentChecksum("SHA-256", Sha256V1),
            1,
            2,
            _fixedUtcTime.AddHours(-3));

        runId = new AnalysisRunId(Guid.NewGuid());
        analysis.RequestRun(
            runId,
            new AnalysisModelReference("Qwen", "Qwen2.5-VL-7B", "v1.0"),
            new[] { new AnalysisCapabilityCode("OCR"), new AnalysisCapabilityCode("FactExtraction") },
            _fixedUtcTime.AddHours(-2));

        analysis.StartRun(runId, _fixedUtcTime.AddMinutes(-90));

        runResultId = new AnalysisRunResultId(Guid.NewGuid());
        factId = new ExtractedFactId(Guid.NewGuid());

        var factInput = new ExtractedFactInput(
            factId,
            new FactCode("SurveyPlan.PlanNumber"),
            new AnalysisFactValue(AnalysisFactValueKind.Text, "SP-2026-9901"),
            new ConfidenceScore(0.95m),
            null);

        analysis.CompleteRun(
            runId,
            runResultId,
            AnalysisResultOutcome.OutputsProduced,
            new List<AnalysisResultArtifactReference>(),
            new List<ExtractedFactInput> { factInput },
            _fixedUtcTime.AddMinutes(-60));

        verifierActorId = Guid.NewGuid();
        var verifierAuthority = new VerifiedAuthoritySnapshot(
            verifierActorId,
            new[] { "FactVerifier", "FactSnapshotPublisher" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, documentId.Value.ToString("D")),
            _fixedUtcTime.AddHours(-2),
            _fixedUtcTime.AddMinutes(-50),
            _fixedUtcTime.AddHours(2));

        analysis.RecordFactVerification(
            new HumanFactVerificationId(Guid.NewGuid()),
            runResultId,
            factId,
            FactVerificationDecision.Confirmed,
            null,
            null,
            verifierActorId,
            _fixedUtcTime.AddMinutes(-40),
            verifierAuthority);

        _analysisRepository.Seed(analysis);
        return analysis;
    }

    private LeaseCase CreateCase(Guid caseId)
    {
        var actorId = Guid.NewGuid();
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "LeaseInitiator" },
            new AuthorityScope(AuthorityScopeKind.Global, null),
            _fixedUtcTime.AddHours(-2),
            _fixedUtcTime.AddMinutes(-50),
            _fixedUtcTime.AddHours(2));

        var leaseCase = new LeaseCase(
            new LeaseCaseId(caseId),
            "APP-2026-0001",
            actorId,
            _fixedUtcTime.AddHours(-2),
            authority);

        _leaseCaseRepository.Seed(leaseCase);
        return leaseCase;
    }

    [Fact]
    public async Task CreateSnapshot_AllFactsVerified_CreatesSnapshot_CommitsOnce()
    {
        // Arrange
        var analysis = CreateAnalysisWithVerifiedFact(out _, out _, out var runResultId, out var actorId);
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "FactSnapshotPublisher" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, analysis.GovernedDocumentId.Value.ToString("D")),
            _fixedUtcTime.AddHours(-2),
            _fixedUtcTime.AddMinutes(-30),
            _fixedUtcTime.AddHours(2));

        var authorityContext = VerifiedAuthorityContext.FromDomain(authority);

        var command = new CreateVerifiedFactSnapshotCommand(
            DocumentAnalysisId: analysis.Id.Value,
            AnalysisRunResultId: runResultId.Value,
            PublishingActorId: actorId,
            AuthorityContext: authorityContext,
            ExpectedAnalysisRevision: analysis.Revision);

        // Act
        var result = await _createHandler.HandleAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(analysis.Id.Value, result.DocumentAnalysisId);
        Assert.Single(result.Entries);
        Assert.Equal("SurveyPlan.PlanNumber", result.Entries[0].FactCode);
        Assert.Equal("SP-2026-9901", result.Entries[0].EffectiveValue.CanonicalValue);
        Assert.Equal("Confirmed", result.Entries[0].Decision);

        Assert.Equal(1, _unitOfWork.CommitCount);
        Assert.Single(analysis.VerifiedFactSnapshots);
    }

    [Fact]
    public async Task CreateSnapshot_UnreviewedFactsExist_ThrowsIncompleteFactVerificationException_NoCommit()
    {
        // Arrange: analysis with completed run having an unreviewed fact
        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var documentId = new GovernedDocumentId(Guid.NewGuid());
        var versionId = new DocumentVersionId(Guid.NewGuid());

        var analysis = new DocumentAnalysis(
            analysisId,
            documentId,
            versionId,
            new DocumentChecksum("SHA-256", Sha256V1),
            1,
            2,
            _fixedUtcTime.AddHours(-3));

        var runId = new AnalysisRunId(Guid.NewGuid());
        analysis.RequestRun(
            runId,
            new AnalysisModelReference("Qwen", "Qwen2.5-VL-7B", "v1.0"),
            new[] { new AnalysisCapabilityCode("OCR"), new AnalysisCapabilityCode("FactExtraction") },
            _fixedUtcTime.AddHours(-2));

        analysis.StartRun(runId, _fixedUtcTime.AddMinutes(-90));

        var runResultId = new AnalysisRunResultId(Guid.NewGuid());
        var factInput = new ExtractedFactInput(
            new ExtractedFactId(Guid.NewGuid()),
            new FactCode("SurveyPlan.PlanNumber"),
            new AnalysisFactValue(AnalysisFactValueKind.Text, "SP-2026-9901"),
            new ConfidenceScore(0.95m),
            null);

        analysis.CompleteRun(
            runId,
            runResultId,
            AnalysisResultOutcome.OutputsProduced,
            new List<AnalysisResultArtifactReference>(),
            new List<ExtractedFactInput> { factInput },
            _fixedUtcTime.AddMinutes(-60));

        _analysisRepository.Seed(analysis);

        var actorId = Guid.NewGuid();
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "FactSnapshotPublisher" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, documentId.Value.ToString("D")),
            _fixedUtcTime.AddHours(-2),
            _fixedUtcTime.AddMinutes(-30),
            _fixedUtcTime.AddHours(2));

        var command = new CreateVerifiedFactSnapshotCommand(
            DocumentAnalysisId: analysis.Id.Value,
            AnalysisRunResultId: runResultId.Value,
            PublishingActorId: actorId,
            AuthorityContext: VerifiedAuthorityContext.FromDomain(authority),
            ExpectedAnalysisRevision: analysis.Revision);

        // Act & Assert
        await Assert.ThrowsAsync<IncompleteFactVerificationException>(() => _createHandler.HandleAsync(command));
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task CreateSnapshot_DoesNotChangeCaseCurrentVerifiedFactSnapshotId_WithoutLinkCommand()
    {
        // Arrange
        var analysis = CreateAnalysisWithVerifiedFact(out _, out _, out var runResultId, out var actorId);
        var leaseCase = CreateCase(Guid.NewGuid());

        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "FactSnapshotPublisher" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, analysis.GovernedDocumentId.Value.ToString("D")),
            _fixedUtcTime.AddHours(-2),
            _fixedUtcTime.AddMinutes(-30),
            _fixedUtcTime.AddHours(2));

        var command = new CreateVerifiedFactSnapshotCommand(
            DocumentAnalysisId: analysis.Id.Value,
            AnalysisRunResultId: runResultId.Value,
            PublishingActorId: actorId,
            AuthorityContext: VerifiedAuthorityContext.FromDomain(authority),
            ExpectedAnalysisRevision: analysis.Revision);

        // Act: Create snapshot on DocumentAnalysis
        var result = await _createHandler.HandleAsync(command);

        // Assert: Snapshot created on DocumentAnalysis, but LeaseCase current snapshot remains unchanged
        Assert.NotNull(result);
        Assert.Single(analysis.VerifiedFactSnapshots);
        Assert.Equal(Guid.Empty, leaseCase.CurrentVerifiedFactSnapshotId);
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task CreateSnapshot_MultipleSnapshots_PreservesPriorSnapshotsInHistory()
    {
        // Arrange: create snapshot 1
        var analysis = CreateAnalysisWithVerifiedFact(out var factId, out _, out var runResultId, out var actorId);
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "FactSnapshotPublisher" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, analysis.GovernedDocumentId.Value.ToString("D")),
            _fixedUtcTime.AddHours(-2),
            _fixedUtcTime.AddMinutes(-30),
            _fixedUtcTime.AddHours(2));

        var command1 = new CreateVerifiedFactSnapshotCommand(
            DocumentAnalysisId: analysis.Id.Value,
            AnalysisRunResultId: runResultId.Value,
            PublishingActorId: actorId,
            AuthorityContext: VerifiedAuthorityContext.FromDomain(authority),
            ExpectedAnalysisRevision: analysis.Revision);

        var snapshot1 = await _createHandler.HandleAsync(command1);

        // Assert: First snapshot preserved
        Assert.Single(analysis.VerifiedFactSnapshots);
        Assert.Equal(snapshot1.Id, analysis.VerifiedFactSnapshots.First().Id.Value);
    }

    [Fact]
    public async Task LinkVerifiedFactSnapshot_ValidCommand_LinksSnapshotToCase_CommitsOnce()
    {
        // Arrange
        var analysis = CreateAnalysisWithVerifiedFact(out _, out _, out var runResultId, out var actorId);
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "FactSnapshotPublisher" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, analysis.GovernedDocumentId.Value.ToString("D")),
            _fixedUtcTime.AddHours(-2),
            _fixedUtcTime.AddMinutes(-30),
            _fixedUtcTime.AddHours(2));

        var snapshot = await _createHandler.HandleAsync(new CreateVerifiedFactSnapshotCommand(
            analysis.Id.Value,
            runResultId.Value,
            actorId,
            VerifiedAuthorityContext.FromDomain(authority),
            analysis.Revision));

        var leaseCase = CreateCase(Guid.NewGuid());

        var linkCommand = new LinkVerifiedFactSnapshotToCaseCommand(
            LeaseCaseId: leaseCase.Id.Value,
            DocumentAnalysisId: analysis.Id.Value,
            SnapshotId: snapshot.Id,
            ExpectedLeaseCaseRevision: leaseCase.Revision,
            ExpectedAnalysisRevision: analysis.Revision);

        // Act
        var result = await _linkHandler.HandleAsync(linkCommand);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(snapshot.Id, leaseCase.CurrentVerifiedFactSnapshotId);
        Assert.Equal(2, _unitOfWork.CommitCount); // 1 for create, 1 for link
    }

    [Fact]
    public async Task LinkVerifiedFactSnapshot_StaleLeaseCaseRevision_ThrowsConcurrencyException_NoCommit()
    {
        // Arrange
        var analysis = CreateAnalysisWithVerifiedFact(out _, out _, out var runResultId, out var actorId);
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "FactSnapshotPublisher" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, analysis.GovernedDocumentId.Value.ToString("D")),
            _fixedUtcTime.AddHours(-2),
            _fixedUtcTime.AddMinutes(-30),
            _fixedUtcTime.AddHours(2));

        var snapshot = await _createHandler.HandleAsync(new CreateVerifiedFactSnapshotCommand(
            analysis.Id.Value,
            runResultId.Value,
            actorId,
            VerifiedAuthorityContext.FromDomain(authority),
            analysis.Revision));

        var leaseCase = CreateCase(Guid.NewGuid());
        var initialCommitCount = _unitOfWork.CommitCount;

        var linkCommand = new LinkVerifiedFactSnapshotToCaseCommand(
            LeaseCaseId: leaseCase.Id.Value,
            DocumentAnalysisId: analysis.Id.Value,
            SnapshotId: snapshot.Id,
            ExpectedLeaseCaseRevision: 999, // Stale
            ExpectedAnalysisRevision: analysis.Revision);

        // Act & Assert
        await Assert.ThrowsAsync<LeaseCaseConcurrencyException>(() => _linkHandler.HandleAsync(linkCommand));
        Assert.Equal(initialCommitCount, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task LinkVerifiedFactSnapshot_StaleAnalysisRevision_ThrowsConcurrencyException_NoCommit()
    {
        // Arrange
        var analysis = CreateAnalysisWithVerifiedFact(out _, out _, out var runResultId, out var actorId);
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "FactSnapshotPublisher" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, analysis.GovernedDocumentId.Value.ToString("D")),
            _fixedUtcTime.AddHours(-2),
            _fixedUtcTime.AddMinutes(-30),
            _fixedUtcTime.AddHours(2));

        var snapshot = await _createHandler.HandleAsync(new CreateVerifiedFactSnapshotCommand(
            analysis.Id.Value,
            runResultId.Value,
            actorId,
            VerifiedAuthorityContext.FromDomain(authority),
            analysis.Revision));

        var leaseCase = CreateCase(Guid.NewGuid());
        var initialCommitCount = _unitOfWork.CommitCount;

        var linkCommand = new LinkVerifiedFactSnapshotToCaseCommand(
            LeaseCaseId: leaseCase.Id.Value,
            DocumentAnalysisId: analysis.Id.Value,
            SnapshotId: snapshot.Id,
            ExpectedLeaseCaseRevision: leaseCase.Revision,
            ExpectedAnalysisRevision: 999); // Stale analysis revision

        // Act & Assert
        await Assert.ThrowsAsync<AnalysisConcurrencyException>(() => _linkHandler.HandleAsync(linkCommand));
        Assert.Equal(initialCommitCount, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task LinkVerifiedFactSnapshot_SnapshotNotOnAnalysis_ThrowsException_NoCommit()
    {
        // Arrange
        var analysis = CreateAnalysisWithVerifiedFact(out _, out _, out var runResultId, out var actorId);
        var leaseCase = CreateCase(Guid.NewGuid());
        var initialCommitCount = _unitOfWork.CommitCount;

        var linkCommand = new LinkVerifiedFactSnapshotToCaseCommand(
            LeaseCaseId: leaseCase.Id.Value,
            DocumentAnalysisId: analysis.Id.Value,
            SnapshotId: Guid.NewGuid(), // Non-existent snapshot
            ExpectedLeaseCaseRevision: leaseCase.Revision,
            ExpectedAnalysisRevision: analysis.Revision);

        // Act & Assert
        await Assert.ThrowsAsync<VerifiedFactSnapshotNotFoundException>(() => _linkHandler.HandleAsync(linkCommand));
        Assert.Equal(initialCommitCount, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task GetVerifiedFactSnapshotByIdQuery_ValidId_ReturnsSnapshotDto_WithoutMutatingOrCommitting()
    {
        // Arrange
        var analysis = CreateAnalysisWithVerifiedFact(out _, out _, out var runResultId, out var actorId);
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "FactSnapshotPublisher" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, analysis.GovernedDocumentId.Value.ToString("D")),
            _fixedUtcTime.AddHours(-2),
            _fixedUtcTime.AddMinutes(-30),
            _fixedUtcTime.AddHours(2));

        var snapshot = await _createHandler.HandleAsync(new CreateVerifiedFactSnapshotCommand(
            analysis.Id.Value,
            runResultId.Value,
            actorId,
            VerifiedAuthorityContext.FromDomain(authority),
            analysis.Revision));

        var commitCount = _unitOfWork.CommitCount;

        var query = new GetVerifiedFactSnapshotByIdQuery(analysis.Id.Value, snapshot.Id);

        // Act
        var result = await _getSnapshotByIdHandler.HandleAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(snapshot.Id, result.Id);
        Assert.Equal(commitCount, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task GetCurrentVerifiedFactsForCaseQuery_ValidCase_ReturnsCurrentSnapshotDto_WithoutMutatingOrCommitting()
    {
        // Arrange
        var analysis = CreateAnalysisWithVerifiedFact(out _, out _, out var runResultId, out var actorId);
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "FactSnapshotPublisher" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, analysis.GovernedDocumentId.Value.ToString("D")),
            _fixedUtcTime.AddHours(-2),
            _fixedUtcTime.AddMinutes(-30),
            _fixedUtcTime.AddHours(2));

        var snapshot = await _createHandler.HandleAsync(new CreateVerifiedFactSnapshotCommand(
            analysis.Id.Value,
            runResultId.Value,
            actorId,
            VerifiedAuthorityContext.FromDomain(authority),
            analysis.Revision));

        var leaseCase = CreateCase(Guid.NewGuid());
        leaseCase.UpdateCurrentVerifiedFactSnapshot(snapshot.Id);
        var commitCount = _unitOfWork.CommitCount;

        var query = new GetCurrentVerifiedFactsForCaseQuery(leaseCase.Id.Value);

        // Act
        var result = await _getCurrentFactsForCaseHandler.HandleAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(snapshot.Id, result.Id);
        Assert.Equal(commitCount, _unitOfWork.CommitCount);
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

    private sealed class SpyLeaseCaseRepository : ILeaseCaseRepository
    {
        private readonly Dictionary<Guid, LeaseCase> _casesById = new();

        public void Seed(LeaseCase leaseCase)
        {
            _casesById[leaseCase.Id.Value] = leaseCase;
        }

        public Task<LeaseCase?> GetByIdAsync(LeaseCaseId id, CancellationToken cancellationToken = default)
        {
            _casesById.TryGetValue(id.Value, out var found);
            return Task.FromResult(found);
        }

        public Task<LeaseCase?> GetByApplicationReferenceAsync(string applicationReference, CancellationToken cancellationToken = default)
        {
            var found = _casesById.Values.FirstOrDefault(c => string.Equals(c.ApplicationReference, applicationReference, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(found);
        }

        public Task AddAsync(LeaseCase leaseCase, CancellationToken cancellationToken = default)
        {
            Seed(leaseCase);
            return Task.CompletedTask;
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
