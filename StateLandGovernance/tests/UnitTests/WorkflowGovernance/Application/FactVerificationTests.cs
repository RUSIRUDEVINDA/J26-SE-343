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
using StateLandGovernance.WorkflowGovernance.Application.Validators;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public class FactVerificationTests
{
    private const string Sha256V1 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private readonly SpyDocumentAnalysisRepository _analysisRepository;
    private readonly SpyWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly FakeTimeProvider _timeProvider;
    private readonly DateTime _fixedUtcTime;
    private readonly VerifyCandidateFactCommandHandler _verifyHandler;

    public FactVerificationTests()
    {
        _fixedUtcTime = new DateTime(2026, 9, 21, 10, 0, 0, DateTimeKind.Utc);
        _analysisRepository = new SpyDocumentAnalysisRepository();
        _unitOfWork = new SpyWorkflowGovernanceUnitOfWork();
        _timeProvider = new FakeTimeProvider(_fixedUtcTime);

        _verifyHandler = new VerifyCandidateFactCommandHandler(
            _analysisRepository,
            _unitOfWork,
            new VerifyCandidateFactCommandValidator(),
            _timeProvider);
    }

    private DocumentAnalysis CreateAnalysisWithCompletedRunAndFact(
        out ExtractedFactId factId,
        out AnalysisRunId runId,
        out AnalysisRunResultId runResultId,
        decimal confidence = 0.95m)
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
            _fixedUtcTime.AddHours(-2));

        runId = new AnalysisRunId(Guid.NewGuid());
        analysis.RequestRun(
            runId,
            new AnalysisModelReference("Qwen", "Qwen2.5-VL-7B", "v1.0"),
            new[] { new AnalysisCapabilityCode("OCR"), new AnalysisCapabilityCode("FactExtraction") },
            _fixedUtcTime.AddHours(-1));

        analysis.StartRun(runId, _fixedUtcTime.AddMinutes(-45));

        runResultId = new AnalysisRunResultId(Guid.NewGuid());
        factId = new ExtractedFactId(Guid.NewGuid());

        var factInput = new ExtractedFactInput(
            factId,
            new FactCode("SurveyPlan.PlanNumber"),
            new AnalysisFactValue(AnalysisFactValueKind.Text, "SP-2026-9901"),
            new ConfidenceScore(confidence),
            null);

        analysis.CompleteRun(
            runId,
            runResultId,
            AnalysisResultOutcome.OutputsProduced,
            new List<AnalysisResultArtifactReference>(),
            new List<ExtractedFactInput> { factInput },
            _fixedUtcTime.AddMinutes(-30));

        _analysisRepository.Seed(analysis);
        return analysis;
    }

    private VerifiedAuthoritySnapshot CreateValidAuthority(Guid actorId, GovernedDocumentId documentId)
    {
        return new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "FactVerifier" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, documentId.Value.ToString("D")),
            _fixedUtcTime.AddHours(-1),
            _fixedUtcTime.AddMinutes(-5),
            _fixedUtcTime.AddHours(2));
    }

    [Fact]
    public async Task ConfirmCandidateFact_ValidCommand_RecordsConfirmation_PreservesMachineCandidate_CommitsOnce()
    {
        // Arrange
        var analysis = CreateAnalysisWithCompletedRunAndFact(out var factId, out var runId, out var runResultId);
        var actorId = Guid.NewGuid();
        var authority = CreateValidAuthority(actorId, analysis.GovernedDocumentId);
        var authorityContext = VerifiedAuthorityContext.FromDomain(authority);

        var command = new VerifyCandidateFactCommand(
            DocumentAnalysisId: analysis.Id.Value,
            AnalysisRunResultId: runResultId.Value,
            ExtractedFactId: factId.Value,
            Decision: "Confirmed",
            CorrectedValue: null,
            Reason: null,
            VerifyingActorId: actorId,
            AuthorityContext: authorityContext,
            ExpectedAnalysisRevision: analysis.Revision);

        // Act
        var result = await _verifyHandler.HandleAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Confirmed", result.Decision);
        Assert.Equal("SP-2026-9901", result.OriginalMachineValue.CanonicalValue);
        Assert.Null(result.CorrectedValue);
        Assert.Null(result.Reason);
        Assert.Equal(actorId, result.VerifyingActorId);
        Assert.Equal("FactVerifier", result.VerifiedCapability);

        // Assert original machine candidate remains untouched
        var candidateFact = analysis.Runs.Single().Result!.ExtractedFacts.Single();
        Assert.Equal("SP-2026-9901", candidateFact.FactValue.CanonicalValue);

        // Assert UoW committed once
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task CorrectCandidateFact_ValidCommand_PreservesOriginalCandidate_RecordsHumanValueSeparately_CommitsOnce()
    {
        // Arrange
        var analysis = CreateAnalysisWithCompletedRunAndFact(out var factId, out var runId, out var runResultId);
        var actorId = Guid.NewGuid();
        var authority = CreateValidAuthority(actorId, analysis.GovernedDocumentId);
        var authorityContext = VerifiedAuthorityContext.FromDomain(authority);

        var correctedValueDto = new FactValueDto("Text", "SP-2026-9901-CORRECTED");

        var command = new VerifyCandidateFactCommand(
            DocumentAnalysisId: analysis.Id.Value,
            AnalysisRunResultId: runResultId.Value,
            ExtractedFactId: factId.Value,
            Decision: "Corrected",
            CorrectedValue: correctedValueDto,
            Reason: "OCR misread the suffix digit",
            VerifyingActorId: actorId,
            AuthorityContext: authorityContext,
            ExpectedAnalysisRevision: analysis.Revision);

        // Act
        var result = await _verifyHandler.HandleAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Corrected", result.Decision);
        Assert.Equal("SP-2026-9901", result.OriginalMachineValue.CanonicalValue);
        Assert.NotNull(result.CorrectedValue);
        Assert.Equal("SP-2026-9901-CORRECTED", result.CorrectedValue.CanonicalValue);
        Assert.Equal("OCR misread the suffix digit", result.Reason);
        Assert.Equal(actorId, result.VerifyingActorId);

        // Critical: machine candidate remains exactly original
        var machineFact = analysis.Runs.Single().Result!.ExtractedFacts.Single();
        Assert.Equal("SP-2026-9901", machineFact.FactValue.CanonicalValue);

        // Assert UoW committed once
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task UnsupportedCandidateFact_ValidCommand_RecordsUnsupported_CommitsOnce()
    {
        // Arrange
        var analysis = CreateAnalysisWithCompletedRunAndFact(out var factId, out var runId, out var runResultId);
        var actorId = Guid.NewGuid();
        var authority = CreateValidAuthority(actorId, analysis.GovernedDocumentId);
        var authorityContext = VerifiedAuthorityContext.FromDomain(authority);

        var command = new VerifyCandidateFactCommand(
            DocumentAnalysisId: analysis.Id.Value,
            AnalysisRunResultId: runResultId.Value,
            ExtractedFactId: factId.Value,
            Decision: "Unsupported",
            CorrectedValue: null,
            Reason: "Text span does not correspond to an official plan number",
            VerifyingActorId: actorId,
            AuthorityContext: authorityContext,
            ExpectedAnalysisRevision: analysis.Revision);

        // Act
        var result = await _verifyHandler.HandleAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Unsupported", result.Decision);
        Assert.Null(result.CorrectedValue);
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task VerifyCandidateFact_UnauthorizedOfficer_FailsWithoutCommit()
    {
        // Arrange
        var analysis = CreateAnalysisWithCompletedRunAndFact(out var factId, out var runId, out var runResultId);
        var actorId = Guid.NewGuid();
        // Authority snapshot missing "FactVerifier" capability
        var unauthorizedSnapshot = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "DocumentViewer" },
            new AuthorityScope(AuthorityScopeKind.GovernedDocument, analysis.GovernedDocumentId.Value.ToString("D")),
            _fixedUtcTime.AddHours(-1),
            _fixedUtcTime.AddMinutes(-5),
            _fixedUtcTime.AddHours(2));

        var authorityContext = VerifiedAuthorityContext.FromDomain(unauthorizedSnapshot);

        var command = new VerifyCandidateFactCommand(
            DocumentAnalysisId: analysis.Id.Value,
            AnalysisRunResultId: runResultId.Value,
            ExtractedFactId: factId.Value,
            Decision: "Confirmed",
            CorrectedValue: null,
            Reason: "Confirmed",
            VerifyingActorId: actorId,
            AuthorityContext: authorityContext,
            ExpectedAnalysisRevision: analysis.Revision);

        // Act & Assert
        await Assert.ThrowsAsync<MissingVerifiedAuthorityException>(() => _verifyHandler.HandleAsync(command));
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task VerifyCandidateFact_MissingAnalysis_FailsWithoutCommit()
    {
        // Arrange
        var nonExistentAnalysisId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var authority = CreateValidAuthority(actorId, new GovernedDocumentId(Guid.NewGuid()));
        var authorityContext = VerifiedAuthorityContext.FromDomain(authority);

        var command = new VerifyCandidateFactCommand(
            DocumentAnalysisId: nonExistentAnalysisId,
            AnalysisRunResultId: Guid.NewGuid(),
            ExtractedFactId: Guid.NewGuid(),
            Decision: "Confirmed",
            CorrectedValue: null,
            Reason: "Confirmed",
            VerifyingActorId: actorId,
            AuthorityContext: authorityContext,
            ExpectedAnalysisRevision: 1);

        // Act & Assert
        await Assert.ThrowsAsync<DocumentAnalysisNotFoundException>(() => _verifyHandler.HandleAsync(command));
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task VerifyCandidateFact_MissingExtractedFact_FailsWithoutCommit()
    {
        // Arrange
        var analysis = CreateAnalysisWithCompletedRunAndFact(out _, out var runId, out var runResultId);
        var actorId = Guid.NewGuid();
        var authority = CreateValidAuthority(actorId, analysis.GovernedDocumentId);
        var authorityContext = VerifiedAuthorityContext.FromDomain(authority);

        var nonExistentFactId = Guid.NewGuid();

        var command = new VerifyCandidateFactCommand(
            DocumentAnalysisId: analysis.Id.Value,
            AnalysisRunResultId: runResultId.Value,
            ExtractedFactId: nonExistentFactId,
            Decision: "Confirmed",
            CorrectedValue: null,
            Reason: "Confirmed",
            VerifyingActorId: actorId,
            AuthorityContext: authorityContext,
            ExpectedAnalysisRevision: analysis.Revision);

        // Act & Assert
        await Assert.ThrowsAsync<ExtractedFactNotFoundException>(() => _verifyHandler.HandleAsync(command));
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task VerifyCandidateFact_StaleAnalysisRevision_FailsWithoutCommit()
    {
        // Arrange
        var analysis = CreateAnalysisWithCompletedRunAndFact(out var factId, out var runId, out var runResultId);
        var actorId = Guid.NewGuid();
        var authority = CreateValidAuthority(actorId, analysis.GovernedDocumentId);
        var authorityContext = VerifiedAuthorityContext.FromDomain(authority);

        var staleRevision = analysis.Revision + 10;

        var command = new VerifyCandidateFactCommand(
            DocumentAnalysisId: analysis.Id.Value,
            AnalysisRunResultId: runResultId.Value,
            ExtractedFactId: factId.Value,
            Decision: "Confirmed",
            CorrectedValue: null,
            Reason: "Confirmed",
            VerifyingActorId: actorId,
            AuthorityContext: authorityContext,
            ExpectedAnalysisRevision: staleRevision);

        // Act & Assert
        await Assert.ThrowsAsync<AnalysisConcurrencyException>(() => _verifyHandler.HandleAsync(command));
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task VerifyCandidateFact_ConfidenceScore100_StillRequiresHumanVerificationAction()
    {
        // Arrange: candidate fact has confidence score 1.0m (100%)
        var analysis = CreateAnalysisWithCompletedRunAndFact(out var factId, out var runId, out var runResultId, confidence: 1.0m);

        // Assert: before human review, verifications collection is empty
        Assert.Empty(analysis.Verifications);

        var actorId = Guid.NewGuid();
        var authority = CreateValidAuthority(actorId, analysis.GovernedDocumentId);
        var authorityContext = VerifiedAuthorityContext.FromDomain(authority);

        var command = new VerifyCandidateFactCommand(
            DocumentAnalysisId: analysis.Id.Value,
            AnalysisRunResultId: runResultId.Value,
            ExtractedFactId: factId.Value,
            Decision: "Confirmed",
            CorrectedValue: null,
            Reason: null,
            VerifyingActorId: actorId,
            AuthorityContext: authorityContext,
            ExpectedAnalysisRevision: analysis.Revision);

        // Act: Human action performed
        var result = await _verifyHandler.HandleAsync(command);

        // Assert: Only now is it verified
        Assert.NotNull(result);
        Assert.Single(analysis.Verifications);
        Assert.Equal("Confirmed", result.Decision);
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task VerifyCandidateFact_MissingCorrectedValue_WhenDecisionIsCorrected_ThrowsValidationException_NoCommit()
    {
        // Arrange
        var analysis = CreateAnalysisWithCompletedRunAndFact(out var factId, out var runId, out var runResultId);
        var actorId = Guid.NewGuid();
        var authority = CreateValidAuthority(actorId, analysis.GovernedDocumentId);
        var authorityContext = VerifiedAuthorityContext.FromDomain(authority);

        var command = new VerifyCandidateFactCommand(
            DocumentAnalysisId: analysis.Id.Value,
            AnalysisRunResultId: runResultId.Value,
            ExtractedFactId: factId.Value,
            Decision: "Corrected",
            CorrectedValue: null, // Invalid: corrected value is required for Corrected decision
            Reason: "Should fail validation",
            VerifyingActorId: actorId,
            AuthorityContext: authorityContext,
            ExpectedAnalysisRevision: analysis.Revision);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _verifyHandler.HandleAsync(command));
        Assert.Equal(0, _unitOfWork.CommitCount);
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
