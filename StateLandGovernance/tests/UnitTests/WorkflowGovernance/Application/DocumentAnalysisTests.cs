namespace StateLandGovernance.UnitTests.WorkflowGovernance.Application;

using System;
using System.Collections.Generic;
using System.IO;
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

public class DocumentAnalysisTests
{
    private const string Sha256V1 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private const string Sha256V2 = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    private readonly SpyDocumentAnalysisRepository _analysisRepository;
    private readonly SpyGovernedDocumentRepository _documentRepository;
    private readonly SpyWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly FakeTimeProvider _timeProvider;
    private readonly DateTime _fixedUtcTime;

    private readonly RequestDocumentAnalysisCommandHandler _requestHandler;
    private readonly StartDocumentAnalysisRunCommandHandler _startRunHandler;
    private readonly CompleteDocumentAnalysisCommandHandler _completeHandler;
    private readonly FailDocumentAnalysisCommandHandler _failHandler;
    private readonly GetDocumentAnalysisByIdQueryHandler _getByIdHandler;
    private readonly GetDocumentAnalysisByVersionIdQueryHandler _getByVersionIdHandler;

    public DocumentAnalysisTests()
    {
        _fixedUtcTime = new DateTime(2026, 9, 21, 10, 0, 0, DateTimeKind.Utc);
        _analysisRepository = new SpyDocumentAnalysisRepository();
        _documentRepository = new SpyGovernedDocumentRepository();
        _unitOfWork = new SpyWorkflowGovernanceUnitOfWork();
        _timeProvider = new FakeTimeProvider(_fixedUtcTime);

        _requestHandler = new RequestDocumentAnalysisCommandHandler(
            _analysisRepository,
            _documentRepository,
            _unitOfWork,
            new RequestDocumentAnalysisCommandValidator(),
            _timeProvider);

        _startRunHandler = new StartDocumentAnalysisRunCommandHandler(
            _analysisRepository,
            _unitOfWork,
            new StartDocumentAnalysisRunCommandValidator(),
            _timeProvider);

        _completeHandler = new CompleteDocumentAnalysisCommandHandler(
            _analysisRepository,
            _unitOfWork,
            new CompleteDocumentAnalysisCommandValidator(),
            _timeProvider);

        _failHandler = new FailDocumentAnalysisCommandHandler(
            _analysisRepository,
            _unitOfWork,
            new FailDocumentAnalysisCommandValidator(),
            _timeProvider);

        _getByIdHandler = new GetDocumentAnalysisByIdQueryHandler(_analysisRepository);
        _getByVersionIdHandler = new GetDocumentAnalysisByVersionIdQueryHandler(_analysisRepository);
    }

    private GovernedDocument SeedDocumentWithTwoVersions(out DocumentVersionId v1Id, out DocumentVersionId v2Id)
    {
        var caseId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "DocumentSubmitter" },
            new AuthorityScope(AuthorityScopeKind.Global, null),
            _fixedUtcTime.AddHours(-1),
            _fixedUtcTime,
            _fixedUtcTime.AddHours(1));

        v1Id = new DocumentVersionId(Guid.NewGuid());
        v2Id = new DocumentVersionId(Guid.NewGuid());

        var document = new GovernedDocument(
            new GovernedDocumentId(Guid.NewGuid()),
            new LeaseCaseId(caseId),
            "SurveyPlan",
            v1Id,
            new DocumentChecksum("SHA-256", Sha256V1),
            new DocumentContentReference("s3://bucket/survey_v1.pdf"),
            "survey_v1.pdf",
            "application/pdf",
            5000,
            actorId,
            _fixedUtcTime,
            authority);

        document.AddVersion(
            v2Id,
            v1Id,
            new DocumentChecksum("SHA-256", Sha256V2),
            new DocumentContentReference("s3://bucket/survey_v2.pdf"),
            "survey_v2.pdf",
            "application/pdf",
            6000,
            actorId,
            _fixedUtcTime,
            authority);

        _documentRepository.Seed(document);
        return document;
    }

    // --- 1. REQUEST DOCUMENT ANALYSIS TESTS (Requested Semantics) ---

    [Fact]
    public async Task RequestAnalysis_ValidCommand_CreatesDocumentAnalysis_SetsStateToRequested_CommitsOnce()
    {
        // Arrange
        var document = SeedDocumentWithTwoVersions(out var v1Id, out var v2Id);
        _ = v2Id; // testing exact version 1 binding

        var command = new RequestDocumentAnalysisCommand(
            GovernedDocumentId: document.Id.Value,
            DocumentVersionId: v1Id.Value,
            ModelProvider: "Qwen",
            ModelName: "Qwen2.5-VL-7B",
            ModelVersion: "v1.0",
            RequestedCapabilities: new List<string> { "OCR", "Extraction" });

        // Act
        var result = await _requestHandler.HandleAsync(command);

        // Assert: Aggregate structure and exact-version binding
        Assert.NotNull(result);
        Assert.Equal(document.Id.Value, result.GovernedDocumentId);
        Assert.Equal(v1Id.Value, result.DocumentVersionId);
        Assert.Equal("SHA-256", result.ChecksumAlgorithm);
        Assert.Equal(Sha256V1, result.ChecksumValue);
        Assert.Equal(1, result.DocumentVersionNumber);
        Assert.Equal(2, result.SourceDocumentRevision);
        Assert.Equal(2, result.Revision); // Created (rev 1) -> RequestRun (rev 2)
        Assert.Equal(1, result.RunCount);

        // Assert: Active run is in Requested state (NOT Running!)
        Assert.NotNull(result.ActiveRun);
        Assert.Equal(1, result.ActiveRun.RunNumber);
        Assert.Equal("Requested", result.ActiveRun.State);
        Assert.Null(result.ActiveRun.StartedAt);
        Assert.Equal(_fixedUtcTime, result.ActiveRun.RequestedAt);
        Assert.Equal("Qwen", result.ActiveRun.ModelProvider);
        Assert.Equal("Qwen2.5-VL-7B", result.ActiveRun.ModelName);
        Assert.Equal("v1.0", result.ActiveRun.ModelVersion);
        Assert.Equal(2, result.ActiveRun.RequestedCapabilities.Count);

        // Assert: Exactly one commit issued
        Assert.Equal(1, _unitOfWork.CommitCount);
        Assert.Single(_analysisRepository.AddedAnalyses);
    }

    [Fact]
    public async Task RequestAnalysis_TargetsHistoricalVersion_DoesNotDefaultToActiveVersion()
    {
        // Arrange: Document active version is v2, but caller explicitly requests v1
        var document = SeedDocumentWithTwoVersions(out var v1Id, out var v2Id);
        Assert.Equal(v2Id, document.ActiveVersionId);

        var command = new RequestDocumentAnalysisCommand(
            GovernedDocumentId: document.Id.Value,
            DocumentVersionId: v1Id.Value, // explicit historical version
            ModelProvider: "Tesseract",
            ModelName: "Tesseract-OCR",
            ModelVersion: "5.3",
            RequestedCapabilities: new List<string> { "OCR" });

        // Act
        var result = await _requestHandler.HandleAsync(command);

        // Assert: bound strictly to v1, not v2
        Assert.Equal(v1Id.Value, result.DocumentVersionId);
        Assert.NotEqual(v2Id.Value, result.DocumentVersionId);
        Assert.Equal(Sha256V1, result.ChecksumValue);
        Assert.Equal(1, result.DocumentVersionNumber);
    }

    [Fact]
    public async Task RequestAnalysis_MissingGovernedDocument_ThrowsGovernedDocumentNotFoundException_WithoutCommit()
    {
        // Arrange
        var missingDocId = Guid.NewGuid();
        var command = new RequestDocumentAnalysisCommand(
            GovernedDocumentId: missingDocId,
            DocumentVersionId: Guid.NewGuid(),
            ModelProvider: "Qwen",
            ModelName: "Qwen2.5-VL-7B",
            ModelVersion: "v1.0",
            RequestedCapabilities: new List<string> { "OCR" });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<GovernedDocumentNotFoundException>(() => _requestHandler.HandleAsync(command));
        Assert.Equal(missingDocId, ex.GovernedDocumentId);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RequestAnalysis_MissingVersionInDocument_ThrowsDocumentVersionNotFoundException_WithoutCommit()
    {
        // Arrange
        var document = SeedDocumentWithTwoVersions(out _, out _);
        var nonExistentVersionId = Guid.NewGuid();

        var command = new RequestDocumentAnalysisCommand(
            GovernedDocumentId: document.Id.Value,
            DocumentVersionId: nonExistentVersionId,
            ModelProvider: "Qwen",
            ModelName: "Qwen2.5-VL-7B",
            ModelVersion: "v1.0",
            RequestedCapabilities: new List<string> { "OCR" });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DocumentVersionNotFoundException>(() => _requestHandler.HandleAsync(command));
        Assert.Equal(document.Id.Value, ex.GovernedDocumentId);
        Assert.Equal(nonExistentVersionId, ex.DocumentVersionId);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RequestAnalysis_ExistingAnalysisAggregate_AppendsRunInRequestedState_PreservesHistoricalRuns()
    {
        // Arrange
        var document = SeedDocumentWithTwoVersions(out var v1Id, out _);
        var analysis = new DocumentAnalysis(
            new DocumentAnalysisId(Guid.NewGuid()),
            document.Id,
            v1Id,
            new DocumentChecksum("SHA-256", Sha256V1),
            1,
            2,
            _fixedUtcTime.AddHours(-2));

        var run1Id = new AnalysisRunId(Guid.NewGuid());
        analysis.RequestRun(run1Id, new AnalysisModelReference("ProviderA", "ModelA", "v1"), new[] { new AnalysisCapabilityCode("OCR") }, _fixedUtcTime.AddHours(-2));
        analysis.StartRun(run1Id, _fixedUtcTime.AddHours(-2));
        _analysisRepository.Seed(analysis);

        var command = new RequestDocumentAnalysisCommand(
            GovernedDocumentId: document.Id.Value,
            DocumentVersionId: v1Id.Value,
            ModelProvider: "ProviderB",
            ModelName: "ModelB",
            ModelVersion: "v2",
            RequestedCapabilities: new List<string> { "Extraction" },
            ExpectedAnalysisRevision: analysis.Revision);

        // Act
        var result = await _requestHandler.HandleAsync(command);

        // Assert: 2 historical runs preserved, second run is in Requested state
        Assert.Equal(2, result.RunCount);
        Assert.Equal(2, result.Runs[1].RunNumber);
        Assert.Equal("Requested", result.Runs[1].State);
        Assert.Null(result.Runs[1].StartedAt);
        Assert.Equal("ModelB", result.Runs[1].ModelName);
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RequestAnalysis_ExistingAggregate_MissingExpectedRevision_ThrowsAnalysisConcurrencyException_WithoutCommit()
    {
        // Arrange
        var document = SeedDocumentWithTwoVersions(out var v1Id, out _);
        var analysis = new DocumentAnalysis(
            new DocumentAnalysisId(Guid.NewGuid()),
            document.Id,
            v1Id,
            new DocumentChecksum("SHA-256", Sha256V1),
            1,
            2,
            _fixedUtcTime.AddHours(-2));
        _analysisRepository.Seed(analysis);

        var command = new RequestDocumentAnalysisCommand(
            GovernedDocumentId: document.Id.Value,
            DocumentVersionId: v1Id.Value,
            ModelProvider: "Qwen",
            ModelName: "Model",
            ModelVersion: "v1",
            RequestedCapabilities: new List<string> { "OCR" },
            ExpectedAnalysisRevision: null); // Null revision on existing aggregate mutation!

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AnalysisConcurrencyException>(() => _requestHandler.HandleAsync(command));
        Assert.Equal(analysis.Id.Value, ex.DocumentAnalysisId);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RequestAnalysis_ExistingAggregate_StaleExpectedRevision_ThrowsAnalysisConcurrencyException_WithoutCommit()
    {
        // Arrange
        var document = SeedDocumentWithTwoVersions(out var v1Id, out _);
        var analysis = new DocumentAnalysis(
            new DocumentAnalysisId(Guid.NewGuid()),
            document.Id,
            v1Id,
            new DocumentChecksum("SHA-256", Sha256V1),
            1,
            2,
            _fixedUtcTime.AddHours(-2));
        _analysisRepository.Seed(analysis);

        var command = new RequestDocumentAnalysisCommand(
            GovernedDocumentId: document.Id.Value,
            DocumentVersionId: v1Id.Value,
            ModelProvider: "Qwen",
            ModelName: "Model",
            ModelVersion: "v1",
            RequestedCapabilities: new List<string> { "OCR" },
            ExpectedAnalysisRevision: 99); // Stale revision mismatch

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AnalysisConcurrencyException>(() => _requestHandler.HandleAsync(command));
        Assert.Equal(analysis.Id.Value, ex.DocumentAnalysisId);
        Assert.Equal(99, ex.ExpectedRevision);
        Assert.Equal(1, ex.ActualRevision);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RequestAnalysis_NewAggregate_SuppliedExpectedRevision_ThrowsAnalysisConcurrencyException_WithoutCommit()
    {
        // Arrange: No analysis aggregate exists yet, but caller provides an expected revision
        var document = SeedDocumentWithTwoVersions(out var v1Id, out _);

        var command = new RequestDocumentAnalysisCommand(
            GovernedDocumentId: document.Id.Value,
            DocumentVersionId: v1Id.Value,
            ModelProvider: "Qwen",
            ModelName: "Model",
            ModelVersion: "v1",
            RequestedCapabilities: new List<string> { "OCR" },
            ExpectedAnalysisRevision: 2);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AnalysisConcurrencyException>(() => _requestHandler.HandleAsync(command));
        Assert.Equal(2, ex.ExpectedRevision);
        Assert.Equal(0, ex.ActualRevision);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    // --- 2. START DOCUMENT ANALYSIS RUN TESTS (Running Semantics) ---

    [Fact]
    public async Task StartRun_ValidCommand_TransitionsRequestedToRunning_CommitsOnce()
    {
        // Arrange: Create aggregate with a run in Requested state
        var document = SeedDocumentWithTwoVersions(out var v1Id, out _);
        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var runId = new AnalysisRunId(Guid.NewGuid());
        var analysis = new DocumentAnalysis(
            analysisId,
            document.Id,
            v1Id,
            new DocumentChecksum("SHA-256", Sha256V1),
            1,
            2,
            _fixedUtcTime.AddHours(-1));

        analysis.RequestRun(runId, new AnalysisModelReference("Qwen", "Model", "v1"), new[] { new AnalysisCapabilityCode("OCR") }, _fixedUtcTime.AddHours(-1));
        _analysisRepository.Seed(analysis);

        var command = new StartDocumentAnalysisRunCommand(
            DocumentAnalysisId: analysisId.Value,
            AnalysisRunId: runId.Value,
            ExpectedRevision: analysis.Revision);

        // Act
        var result = await _startRunHandler.HandleAsync(command);

        // Assert: Transitions to Running
        Assert.NotNull(result);
        var run = result.Runs.Single(r => r.Id == runId.Value);
        Assert.Equal("Running", run.State);
        Assert.Equal(_fixedUtcTime, run.StartedAt);
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task StartRun_StaleExpectedRevision_ThrowsAnalysisConcurrencyException_WithoutCommit()
    {
        // Arrange
        var document = SeedDocumentWithTwoVersions(out var v1Id, out _);
        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var runId = new AnalysisRunId(Guid.NewGuid());
        var analysis = new DocumentAnalysis(
            analysisId,
            document.Id,
            v1Id,
            new DocumentChecksum("SHA-256", Sha256V1),
            1,
            2,
            _fixedUtcTime.AddHours(-1));

        analysis.RequestRun(runId, new AnalysisModelReference("Qwen", "Model", "v1"), new[] { new AnalysisCapabilityCode("OCR") }, _fixedUtcTime.AddHours(-1));
        _analysisRepository.Seed(analysis);

        var command = new StartDocumentAnalysisRunCommand(
            DocumentAnalysisId: analysisId.Value,
            AnalysisRunId: runId.Value,
            ExpectedRevision: 99); // Mismatch

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AnalysisConcurrencyException>(() => _startRunHandler.HandleAsync(command));
        Assert.Equal(analysisId.Value, ex.DocumentAnalysisId);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task StartRun_MissingAnalysisAggregate_ThrowsDocumentAnalysisNotFoundException_WithoutCommit()
    {
        // Arrange
        var missingId = Guid.NewGuid();
        var command = new StartDocumentAnalysisRunCommand(
            DocumentAnalysisId: missingId,
            AnalysisRunId: Guid.NewGuid(),
            ExpectedRevision: 1);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DocumentAnalysisNotFoundException>(() => _startRunHandler.HandleAsync(command));
        Assert.Equal(missingId, ex.DocumentAnalysisId);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task StartRun_AlreadyRunning_ThrowsInvalidAnalysisRunTransitionException_WithoutCommit()
    {
        // Arrange: Run is already in Running state
        var document = SeedDocumentWithTwoVersions(out var v1Id, out _);
        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var runId = new AnalysisRunId(Guid.NewGuid());
        var analysis = new DocumentAnalysis(
            analysisId,
            document.Id,
            v1Id,
            new DocumentChecksum("SHA-256", Sha256V1),
            1,
            2,
            _fixedUtcTime.AddHours(-1));

        analysis.RequestRun(runId, new AnalysisModelReference("Qwen", "Model", "v1"), new[] { new AnalysisCapabilityCode("OCR") }, _fixedUtcTime.AddHours(-1));
        analysis.StartRun(runId, _fixedUtcTime.AddMinutes(-30));
        _analysisRepository.Seed(analysis);

        var command = new StartDocumentAnalysisRunCommand(
            DocumentAnalysisId: analysisId.Value,
            AnalysisRunId: runId.Value,
            ExpectedRevision: analysis.Revision);

        // Act & Assert: Domain rejects starting an already Running run
        await Assert.ThrowsAsync<InvalidAnalysisRunTransitionException>(() => _startRunHandler.HandleAsync(command));
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    // --- 3. COMPLETE DOCUMENT ANALYSIS TESTS ---

    [Fact]
    public async Task CompleteAnalysis_ValidMachineResults_RecordsCandidatesAndArtifacts_PreservesMachineStatus()
    {
        // Arrange: Start a run
        var document = SeedDocumentWithTwoVersions(out var v1Id, out _);
        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var runId = new AnalysisRunId(Guid.NewGuid());
        var analysis = new DocumentAnalysis(
            analysisId,
            document.Id,
            v1Id,
            new DocumentChecksum("SHA-256", Sha256V1),
            1,
            2,
            _fixedUtcTime.AddHours(-1));

        analysis.RequestRun(runId, new AnalysisModelReference("Qwen", "Qwen2.5-VL-7B", "v1"), new[] { new AnalysisCapabilityCode("OCR"), new AnalysisCapabilityCode("Extraction") }, _fixedUtcTime.AddHours(-1));
        analysis.StartRun(runId, _fixedUtcTime.AddMinutes(-30));
        _analysisRepository.Seed(analysis);

        var artifactId = Guid.NewGuid();
        var artifacts = new List<AnalysisArtifactOutputDto>
        {
            new AnalysisArtifactOutputDto(
                ArtifactId: artifactId,
                ArtifactKind: "OcrText",
                StorageReference: "analysis/runs/ocr.txt",
                ContentType: "text/plain",
                ChecksumAlgorithm: "SHA-256",
                ChecksumValue: "1111111111111111111111111111111111111111111111111111111111111111")
        };

        var candidateFacts = new List<ExtractedCandidateFactDto>
        {
            new ExtractedCandidateFactDto(
                FactId: Guid.NewGuid(),
                FactCode: "SurveyPlan.PlanNumber",
                ValueKind: "Identifier",
                CanonicalValue: "SP-2026-9901",
                ConfidenceScore: 0.95m,
                EvidenceArtifactId: artifactId,
                PageNumber: 1,
                Excerpt: "Survey Plan Number: SP-2026-9901"),

            new ExtractedCandidateFactDto(
                FactId: Guid.NewGuid(),
                FactCode: "SurveyPlan.LandExtentHectares",
                ValueKind: "Decimal",
                CanonicalValue: "2.45",
                ConfidenceScore: 0.88m,
                EvidenceArtifactId: artifactId,
                PageNumber: 1,
                Excerpt: "Extent: 2.45 Hectares")
        };

        var command = new CompleteDocumentAnalysisCommand(
            DocumentAnalysisId: analysisId.Value,
            AnalysisRunId: runId.Value,
            ExpectedRevision: analysis.Revision,
            Outcome: "OutputsProduced",
            Artifacts: artifacts,
            ExtractedCandidateFacts: candidateFacts);

        // Act
        var result = await _completeHandler.HandleAsync(command);

        // Assert: Run state transitions to Completed
        Assert.NotNull(result);
        var completedRun = result.Runs.Single(r => r.Id == runId.Value);
        Assert.Equal("Completed", completedRun.State);
        Assert.Equal(_fixedUtcTime, completedRun.CompletedAt);

        // Assert: Result contains machine candidates and artifacts
        Assert.NotNull(completedRun.Result);
        Assert.Equal("OutputsProduced", completedRun.Result.Outcome);
        Assert.Single(completedRun.Result.Artifacts);
        Assert.Equal("OcrText", completedRun.Result.Artifacts[0].ArtifactKind);
        Assert.Equal(2, completedRun.Result.ExtractedCandidateFacts.Count);

        // Assert: Candidates remain strictly unverified candidates (CandidateValue, not VerifiedValue)
        var fact1 = completedRun.Result.ExtractedCandidateFacts[0];
        Assert.Equal("SurveyPlan.PlanNumber", fact1.FactCode);
        Assert.Equal("SP-2026-9901", fact1.CandidateValue);
        Assert.Equal(0.95m, fact1.ConfidenceScore);
        Assert.Equal(1, fact1.PageNumber);
        Assert.Equal("Survey Plan Number: SP-2026-9901", fact1.Excerpt);

        // Assert: Commit occurred once
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task CompleteAnalysis_StaleExpectedRevision_ThrowsAnalysisConcurrencyException_WithoutCommit()
    {
        // Arrange
        var document = SeedDocumentWithTwoVersions(out var v1Id, out _);
        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var runId = new AnalysisRunId(Guid.NewGuid());
        var analysis = new DocumentAnalysis(
            analysisId,
            document.Id,
            v1Id,
            new DocumentChecksum("SHA-256", Sha256V1),
            1,
            2,
            _fixedUtcTime.AddHours(-1));

        analysis.RequestRun(runId, new AnalysisModelReference("Qwen", "Model", "v1"), new[] { new AnalysisCapabilityCode("OCR") }, _fixedUtcTime.AddHours(-1));
        analysis.StartRun(runId, _fixedUtcTime.AddMinutes(-30));
        _analysisRepository.Seed(analysis);

        var command = new CompleteDocumentAnalysisCommand(
            DocumentAnalysisId: analysisId.Value,
            AnalysisRunId: runId.Value,
            ExpectedRevision: 999, // Mismatch
            Outcome: "NoFindings",
            Artifacts: new List<AnalysisArtifactOutputDto>(),
            ExtractedCandidateFacts: new List<ExtractedCandidateFactDto>());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AnalysisConcurrencyException>(() => _completeHandler.HandleAsync(command));
        Assert.Equal(analysisId.Value, ex.DocumentAnalysisId);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task CompleteAnalysis_BeforeRunning_ThrowsInvalidAnalysisRunTransitionException_WithoutCommit()
    {
        // Arrange: Run is still in Requested state (not Running)
        var document = SeedDocumentWithTwoVersions(out var v1Id, out _);
        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var runId = new AnalysisRunId(Guid.NewGuid());
        var analysis = new DocumentAnalysis(
            analysisId,
            document.Id,
            v1Id,
            new DocumentChecksum("SHA-256", Sha256V1),
            1,
            2,
            _fixedUtcTime.AddHours(-1));

        analysis.RequestRun(runId, new AnalysisModelReference("Qwen", "Model", "v1"), new[] { new AnalysisCapabilityCode("OCR") }, _fixedUtcTime.AddHours(-1));
        // Intentionally NOT calling StartRun: state is Requested
        _analysisRepository.Seed(analysis);

        var command = new CompleteDocumentAnalysisCommand(
            DocumentAnalysisId: analysisId.Value,
            AnalysisRunId: runId.Value,
            ExpectedRevision: analysis.Revision,
            Outcome: "NoFindings",
            Artifacts: new List<AnalysisArtifactOutputDto>(),
            ExtractedCandidateFacts: new List<ExtractedCandidateFactDto>());

        // Act & Assert: Domain rejects completing a run that has not reached Running state
        await Assert.ThrowsAsync<InvalidAnalysisRunTransitionException>(() => _completeHandler.HandleAsync(command));
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    // --- 4. FAIL DOCUMENT ANALYSIS TESTS ---

    [Fact]
    public async Task FailAnalysis_ValidCommand_MarksRunFailed_PreservesSanitizedFailure_CommitsOnce()
    {
        // Arrange
        var document = SeedDocumentWithTwoVersions(out var v1Id, out _);
        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var runId = new AnalysisRunId(Guid.NewGuid());
        var analysis = new DocumentAnalysis(
            analysisId,
            document.Id,
            v1Id,
            new DocumentChecksum("SHA-256", Sha256V1),
            1,
            2,
            _fixedUtcTime.AddHours(-1));

        analysis.RequestRun(runId, new AnalysisModelReference("Qwen", "Model", "v1"), new[] { new AnalysisCapabilityCode("OCR") }, _fixedUtcTime.AddHours(-1));
        analysis.StartRun(runId, _fixedUtcTime.AddMinutes(-30));
        _analysisRepository.Seed(analysis);

        var command = new FailDocumentAnalysisCommand(
            DocumentAnalysisId: analysisId.Value,
            AnalysisRunId: runId.Value,
            ExpectedRevision: analysis.Revision,
            FailureCode: "OCR_TIMEOUT",
            SafeDescription: "External inference server timed out after 120 seconds.");

        // Act
        var result = await _failHandler.HandleAsync(command);

        // Assert
        Assert.NotNull(result);
        var failedRun = result.Runs.Single(r => r.Id == runId.Value);
        Assert.Equal("Failed", failedRun.State);
        Assert.Equal("OCR_TIMEOUT", failedRun.FailureCode);
        Assert.Equal("External inference server timed out after 120 seconds.", failedRun.FailureDescription);
        Assert.Equal(_fixedUtcTime, failedRun.FailedAt);
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public void FailAnalysis_RejectsRawStackTraceWithNewlines_InValidator()
    {
        // Arrange: Validator should reject raw multi-line stack trace
        var validator = new FailDocumentAnalysisCommandValidator();
        var command = new FailDocumentAnalysisCommand(
            DocumentAnalysisId: Guid.NewGuid(),
            AnalysisRunId: Guid.NewGuid(),
            ExpectedRevision: 1,
            FailureCode: "INTERNAL_ERROR",
            SafeDescription: "System.Exception: crashed\r\n   at Service.Method() in file.cs:line 12");

        // Act
        var validation = validator.Validate(command);

        // Assert
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.Contains("control characters or line breaks"));
    }

    [Fact]
    public void FailAnalysis_RejectsCredentialOrAuthorizationTokens_InValidator()
    {
        // Arrange: Validator should reject sensitive authorization leak
        var validator = new FailDocumentAnalysisCommandValidator();
        var command = new FailDocumentAnalysisCommand(
            DocumentAnalysisId: Guid.NewGuid(),
            AnalysisRunId: Guid.NewGuid(),
            ExpectedRevision: 1,
            FailureCode: "AUTH_ERROR",
            SafeDescription: "Failed with Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9");

        // Act
        var validation = validator.Validate(command);

        // Assert
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.Contains("potentially sensitive credential"));
    }

    [Fact]
    public async Task FailAnalysis_StaleExpectedRevision_ThrowsAnalysisConcurrencyException_WithoutCommit()
    {
        // Arrange
        var document = SeedDocumentWithTwoVersions(out var v1Id, out _);
        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var runId = new AnalysisRunId(Guid.NewGuid());
        var analysis = new DocumentAnalysis(
            analysisId,
            document.Id,
            v1Id,
            new DocumentChecksum("SHA-256", Sha256V1),
            1,
            2,
            _fixedUtcTime.AddHours(-1));

        analysis.RequestRun(runId, new AnalysisModelReference("Qwen", "Model", "v1"), new[] { new AnalysisCapabilityCode("OCR") }, _fixedUtcTime.AddHours(-1));
        _analysisRepository.Seed(analysis);

        var command = new FailDocumentAnalysisCommand(
            DocumentAnalysisId: analysisId.Value,
            AnalysisRunId: runId.Value,
            ExpectedRevision: 55, // Mismatch
            FailureCode: "MODEL_ERROR",
            SafeDescription: "Inference failed.");

        // Act & Assert
        await Assert.ThrowsAsync<AnalysisConcurrencyException>(() => _failHandler.HandleAsync(command));
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public void FailAnalysis_RejectsLocalMachinePaths_InValidator()
    {
        // Arrange: Validator should reject descriptions containing local filesystem paths
        var validator = new FailDocumentAnalysisCommandValidator();

        var windowsPathCommand = new FailDocumentAnalysisCommand(
            DocumentAnalysisId: Guid.NewGuid(),
            AnalysisRunId: Guid.NewGuid(),
            ExpectedRevision: 1,
            FailureCode: "IO_ERROR",
            SafeDescription: @"Failed loading model from C:\models\qwen\weights.bin");

        var unixPathCommand = new FailDocumentAnalysisCommand(
            DocumentAnalysisId: Guid.NewGuid(),
            AnalysisRunId: Guid.NewGuid(),
            ExpectedRevision: 1,
            FailureCode: "IO_ERROR",
            SafeDescription: "Failed writing logs to /var/log/pipeline/err.log");

        // Act
        var winResult = validator.Validate(windowsPathCommand);
        var unixResult = validator.Validate(unixPathCommand);

        // Assert
        Assert.False(winResult.IsValid);
        Assert.Contains(winResult.Errors, e => e.Contains("local machine paths or file system references"));

        Assert.False(unixResult.IsValid);
        Assert.Contains(unixResult.Errors, e => e.Contains("local machine paths or file system references"));
    }

    [Fact]
    public async Task FailAnalysis_NonexistentRunId_ThrowsAnalysisRunNotFoundException_WithoutCommit()
    {
        // Arrange
        var document = SeedDocumentWithTwoVersions(out var v1Id, out _);
        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var runId = new AnalysisRunId(Guid.NewGuid());
        var analysis = new DocumentAnalysis(
            analysisId,
            document.Id,
            v1Id,
            new DocumentChecksum("SHA-256", Sha256V1),
            1,
            2,
            _fixedUtcTime.AddHours(-1));

        analysis.RequestRun(runId, new AnalysisModelReference("Qwen", "Model", "v1"), new[] { new AnalysisCapabilityCode("OCR") }, _fixedUtcTime.AddHours(-1));
        _analysisRepository.Seed(analysis);

        var nonexistentRunId = Guid.NewGuid();
        var command = new FailDocumentAnalysisCommand(
            DocumentAnalysisId: analysisId.Value,
            AnalysisRunId: nonexistentRunId,
            ExpectedRevision: analysis.Revision,
            FailureCode: "TIMEOUT",
            SafeDescription: "Inference timed out.");

        // Act & Assert
        await Assert.ThrowsAsync<AnalysisRunNotFoundException>(() => _failHandler.HandleAsync(command));
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task FailAnalysis_DoesNotMutateCaseOrFacts()
    {
        // Arrange
        var document = SeedDocumentWithTwoVersions(out var v1Id, out _);
        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var runId = new AnalysisRunId(Guid.NewGuid());
        var analysis = new DocumentAnalysis(
            analysisId,
            document.Id,
            v1Id,
            new DocumentChecksum("SHA-256", Sha256V1),
            1,
            2,
            _fixedUtcTime.AddHours(-1));

        analysis.RequestRun(runId, new AnalysisModelReference("Qwen", "Model", "v1"), new[] { new AnalysisCapabilityCode("OCR") }, _fixedUtcTime.AddHours(-1));
        analysis.StartRun(runId, _fixedUtcTime.AddMinutes(-30));
        _analysisRepository.Seed(analysis);

        var command = new FailDocumentAnalysisCommand(
            DocumentAnalysisId: analysisId.Value,
            AnalysisRunId: runId.Value,
            ExpectedRevision: analysis.Revision,
            FailureCode: "CRASH",
            SafeDescription: "Subprocess crashed.");

        // Act
        var result = await _failHandler.HandleAsync(command);

        // Assert: No candidate facts, no human verifications, no snapshots
        Assert.NotNull(result);
        var run = result.Runs.Single(r => r.Id == runId.Value);
        Assert.Null(run.Result);
        Assert.Empty(analysis.Verifications);
        Assert.Empty(analysis.VerifiedFactSnapshots);
    }

    [Fact]
    public async Task CompleteAnalysis_DoesNotMutateVerifiedFactSnapshot_ConfidenceScoreOneDoesNotVerifyFact()
    {
        // Invariant: Even if machine outputs Confidence = 1.0, facts remain strictly candidate facts.
        // Component 3 4A.3 does NOT create HumanFactVerification or VerifiedFactSnapshot.
        var document = SeedDocumentWithTwoVersions(out var v1Id, out _);
        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var runId = new AnalysisRunId(Guid.NewGuid());
        var analysis = new DocumentAnalysis(
            analysisId,
            document.Id,
            v1Id,
            new DocumentChecksum("SHA-256", Sha256V1),
            1,
            2,
            _fixedUtcTime.AddHours(-1));

        analysis.RequestRun(runId, new AnalysisModelReference("Qwen", "Qwen2.5-VL-7B", "v1"), new[] { new AnalysisCapabilityCode("Extraction") }, _fixedUtcTime.AddHours(-1));
        analysis.StartRun(runId, _fixedUtcTime.AddMinutes(-30));
        _analysisRepository.Seed(analysis);

        var candidateFacts = new List<ExtractedCandidateFactDto>
        {
            new ExtractedCandidateFactDto(
                FactId: Guid.NewGuid(),
                FactCode: "SurveyPlan.PlanNumber",
                ValueKind: "Identifier",
                CanonicalValue: "SP-9999",
                ConfidenceScore: 1.0m, // Maximum confidence
                EvidenceArtifactId: null,
                PageNumber: 1,
                Excerpt: "Plan: SP-9999")
        };

        var command = new CompleteDocumentAnalysisCommand(
            DocumentAnalysisId: analysisId.Value,
            AnalysisRunId: runId.Value,
            ExpectedRevision: analysis.Revision,
            Outcome: "OutputsProduced",
            Artifacts: new List<AnalysisArtifactOutputDto>(),
            ExtractedCandidateFacts: candidateFacts);

        // Act
        var result = await _completeHandler.HandleAsync(command);

        // Assert: Fact is recorded solely as CandidateValue
        Assert.NotNull(result);
        var completedRun = result.Runs.Single(r => r.Id == runId.Value);
        Assert.NotNull(completedRun.Result);
        var fact = completedRun.Result.ExtractedCandidateFacts.Single();
        Assert.Equal("SP-9999", fact.CandidateValue);
        Assert.Equal(1.0m, fact.ConfidenceScore);

        // Crucial invariant: Aggregate verifications and snapshots remain completely empty!
        Assert.Empty(analysis.Verifications);
        Assert.Empty(analysis.VerifiedFactSnapshots);
    }

    [Fact]
    public async Task CompleteAnalysis_NonexistentRunId_ThrowsAnalysisRunNotFoundException_WithoutCommit()
    {
        // Arrange
        var document = SeedDocumentWithTwoVersions(out var v1Id, out _);
        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var runId = new AnalysisRunId(Guid.NewGuid());
        var analysis = new DocumentAnalysis(
            analysisId,
            document.Id,
            v1Id,
            new DocumentChecksum("SHA-256", Sha256V1),
            1,
            2,
            _fixedUtcTime.AddHours(-1));

        analysis.RequestRun(runId, new AnalysisModelReference("Qwen", "Model", "v1"), new[] { new AnalysisCapabilityCode("OCR") }, _fixedUtcTime.AddHours(-1));
        analysis.StartRun(runId, _fixedUtcTime.AddMinutes(-30));
        _analysisRepository.Seed(analysis);

        var nonexistentRunId = Guid.NewGuid();
        var command = new CompleteDocumentAnalysisCommand(
            DocumentAnalysisId: analysisId.Value,
            AnalysisRunId: nonexistentRunId,
            ExpectedRevision: analysis.Revision,
            Outcome: "NoFindings",
            Artifacts: new List<AnalysisArtifactOutputDto>(),
            ExtractedCandidateFacts: new List<ExtractedCandidateFactDto>());

        // Act & Assert
        await Assert.ThrowsAsync<AnalysisRunNotFoundException>(() => _completeHandler.HandleAsync(command));
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task StartRun_NonexistentRunId_ThrowsAnalysisRunNotFoundException_WithoutCommit()
    {
        // Arrange
        var document = SeedDocumentWithTwoVersions(out var v1Id, out _);
        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var runId = new AnalysisRunId(Guid.NewGuid());
        var analysis = new DocumentAnalysis(
            analysisId,
            document.Id,
            v1Id,
            new DocumentChecksum("SHA-256", Sha256V1),
            1,
            2,
            _fixedUtcTime.AddHours(-1));

        analysis.RequestRun(runId, new AnalysisModelReference("Qwen", "Model", "v1"), new[] { new AnalysisCapabilityCode("OCR") }, _fixedUtcTime.AddHours(-1));
        _analysisRepository.Seed(analysis);

        var nonexistentRunId = Guid.NewGuid();
        var command = new StartDocumentAnalysisRunCommand(
            DocumentAnalysisId: analysisId.Value,
            AnalysisRunId: nonexistentRunId,
            ExpectedRevision: analysis.Revision);

        // Act & Assert
        await Assert.ThrowsAsync<AnalysisRunNotFoundException>(() => _startRunHandler.HandleAsync(command));
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task RequestAnalysis_ValidationFailure_ThrowsValidationException_WithoutCommit()
    {
        // Arrange: Missing required fields
        var command = new RequestDocumentAnalysisCommand(
            GovernedDocumentId: Guid.Empty,
            DocumentVersionId: Guid.Empty,
            ModelProvider: "",
            ModelName: "",
            ModelVersion: "",
            RequestedCapabilities: new List<string>());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _requestHandler.HandleAsync(command));
        Assert.NotEmpty(ex.Errors);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task StartRun_ValidationFailure_ThrowsValidationException_WithoutCommit()
    {
        // Arrange: Empty IDs
        var command = new StartDocumentAnalysisRunCommand(
            DocumentAnalysisId: Guid.Empty,
            AnalysisRunId: Guid.Empty,
            ExpectedRevision: -1);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _startRunHandler.HandleAsync(command));
        Assert.NotEmpty(ex.Errors);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task CompleteAnalysis_ValidationFailure_ThrowsValidationException_WithoutCommit()
    {
        // Arrange: Invalid outcome and negative revision
        var command = new CompleteDocumentAnalysisCommand(
            DocumentAnalysisId: Guid.NewGuid(),
            AnalysisRunId: Guid.NewGuid(),
            ExpectedRevision: 0,
            Outcome: "InvalidOutcome",
            Artifacts: null!,
            ExtractedCandidateFacts: null!);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _completeHandler.HandleAsync(command));
        Assert.NotEmpty(ex.Errors);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    // --- 5. EXACT CONTENT READER PORT CONTRACT TEST ---

    [Fact]
    public void DocumentContentReader_Interface_ForcesExactVersionId()
    {
        // Verify the interface contract requires exact strongly-typed GovernedDocumentId and DocumentVersionId
        var method = typeof(IDocumentContentReader).GetMethod(nameof(IDocumentContentReader.ReadContentAsync));
        Assert.NotNull(method);

        var parameters = method.GetParameters();
        Assert.Equal("documentId", parameters[0].Name);
        Assert.Equal(typeof(GovernedDocumentId), parameters[0].ParameterType);

        Assert.Equal("versionId", parameters[1].Name);
        Assert.Equal(typeof(DocumentVersionId), parameters[1].ParameterType);

        Assert.Equal("contentReference", parameters[2].Name);
        Assert.Equal(typeof(string), parameters[2].ParameterType);
    }

    // --- 6. QUERY TESTS ---

    [Fact]
    public async Task GetDocumentAnalysisById_ExistingAnalysis_ReturnsMappedDto_WithoutCommit()
    {
        // Arrange
        var document = SeedDocumentWithTwoVersions(out var v1Id, out _);
        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var analysis = new DocumentAnalysis(
            analysisId,
            document.Id,
            v1Id,
            new DocumentChecksum("SHA-256", Sha256V1),
            1,
            2,
            _fixedUtcTime);
        _analysisRepository.Seed(analysis);

        // Act
        var result = await _getByIdHandler.HandleAsync(new GetDocumentAnalysisByIdQuery(analysisId.Value));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(analysisId.Value, result.Id);
        Assert.Equal(document.Id.Value, result.GovernedDocumentId);
        Assert.Equal(v1Id.Value, result.DocumentVersionId);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task GetDocumentAnalysisById_MissingAnalysis_ThrowsDocumentAnalysisNotFoundException()
    {
        // Arrange
        var missingId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DocumentAnalysisNotFoundException>(() =>
            _getByIdHandler.HandleAsync(new GetDocumentAnalysisByIdQuery(missingId)));

        Assert.Equal(missingId, ex.DocumentAnalysisId);
    }

    [Fact]
    public async Task GetDocumentAnalysisByVersionId_ExistingAnalysis_ReturnsMappedDto()
    {
        // Arrange
        var document = SeedDocumentWithTwoVersions(out var v1Id, out _);
        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var analysis = new DocumentAnalysis(
            analysisId,
            document.Id,
            v1Id,
            new DocumentChecksum("SHA-256", Sha256V1),
            1,
            2,
            _fixedUtcTime);
        _analysisRepository.Seed(analysis);

        // Act
        var result = await _getByVersionIdHandler.HandleAsync(new GetDocumentAnalysisByVersionIdQuery(v1Id.Value));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(analysisId.Value, result.Id);
        Assert.Equal(v1Id.Value, result.DocumentVersionId);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task GetDocumentAnalysisByVersionId_MissingAnalysis_ThrowsDocumentAnalysisNotFoundException()
    {
        // Arrange
        var missingVersionId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DocumentAnalysisNotFoundException>(() =>
            _getByVersionIdHandler.HandleAsync(new GetDocumentAnalysisByVersionIdQuery(missingVersionId)));

        Assert.Equal(missingVersionId, ex.DocumentVersionId);
    }

    // --- 7. TEST DOUBLES ---

    private sealed class SpyDocumentAnalysisRepository : IDocumentAnalysisRepository
    {
        private readonly Dictionary<Guid, DocumentAnalysis> _analysesById = new();
        public List<DocumentAnalysis> AddedAnalyses { get; } = new();

        public void Seed(DocumentAnalysis analysis)
        {
            _analysesById[analysis.Id.Value] = analysis;
        }

        public Task<DocumentAnalysis?> GetByIdAsync(DocumentAnalysisId id, CancellationToken cancellationToken = default)
        {
            _analysesById.TryGetValue(id.Value, out var analysis);
            return Task.FromResult(analysis);
        }

        public Task<DocumentAnalysis?> GetByDocumentVersionIdAsync(DocumentVersionId documentVersionId, CancellationToken cancellationToken = default)
        {
            var found = _analysesById.Values.FirstOrDefault(a => a.DocumentVersionId == documentVersionId);
            return Task.FromResult(found);
        }

        public Task AddAsync(DocumentAnalysis documentAnalysis, CancellationToken cancellationToken = default)
        {
            AddedAnalyses.Add(documentAnalysis);
            Seed(documentAnalysis);
            return Task.CompletedTask;
        }
    }

    private sealed class SpyGovernedDocumentRepository : IGovernedDocumentRepository
    {
        private readonly Dictionary<Guid, GovernedDocument> _docsById = new();

        public void Seed(GovernedDocument doc)
        {
            _docsById[doc.Id.Value] = doc;
        }

        public Task<GovernedDocument?> GetByIdAsync(GovernedDocumentId id, CancellationToken cancellationToken = default)
        {
            _docsById.TryGetValue(id.Value, out var doc);
            return Task.FromResult(doc);
        }

        public Task<IReadOnlyList<GovernedDocument>> GetByLeaseCaseIdAsync(LeaseCaseId leaseCaseId, CancellationToken cancellationToken = default)
        {
            var docs = _docsById.Values.Where(d => d.LeaseCaseId == leaseCaseId).ToList();
            return Task.FromResult<IReadOnlyList<GovernedDocument>>(docs);
        }

        public Task AddAsync(GovernedDocument document, CancellationToken cancellationToken = default)
        {
            Seed(document);
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
