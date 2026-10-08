namespace StateLandGovernance.UnitTests.WorkflowGovernance.Infrastructure;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.Exceptions;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Services;
using Xunit;

public sealed class DocumentAnalysisExecutionServiceTests
{
    private sealed class InMemoryDocumentAnalysisRepository : IDocumentAnalysisRepository
    {
        private readonly Dictionary<Guid, DocumentAnalysis> _analyses = new();

        public void Seed(DocumentAnalysis analysis) => _analyses[analysis.Id.Value] = analysis;

        public Task<DocumentAnalysis?> GetByIdAsync(DocumentAnalysisId id, CancellationToken cancellationToken = default)
        {
            _analyses.TryGetValue(id.Value, out var analysis);
            return Task.FromResult(analysis);
        }

        public Task<DocumentAnalysis?> GetByDocumentVersionIdAsync(DocumentVersionId documentVersionId, CancellationToken cancellationToken = default)
        {
            var analysis = _analyses.Values.FirstOrDefault(a => a.DocumentVersionId == documentVersionId);
            return Task.FromResult(analysis);
        }

        public Task AddAsync(DocumentAnalysis documentAnalysis, CancellationToken cancellationToken = default)
        {
            Seed(documentAnalysis);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryGovernedDocumentRepository : IGovernedDocumentRepository
    {
        private readonly Dictionary<Guid, GovernedDocument> _documents = new();

        public void Seed(GovernedDocument document) => _documents[document.Id.Value] = document;

        public Task<GovernedDocument?> GetByIdAsync(GovernedDocumentId id, CancellationToken cancellationToken = default)
        {
            _documents.TryGetValue(id.Value, out var doc);
            return Task.FromResult(doc);
        }

        public Task<IReadOnlyList<GovernedDocument>> GetByLeaseCaseIdAsync(LeaseCaseId leaseCaseId, CancellationToken cancellationToken = default)
        {
            var docs = _documents.Values.Where(d => d.LeaseCaseId == leaseCaseId).ToList();
            return Task.FromResult<IReadOnlyList<GovernedDocument>>(docs);
        }

        public Task AddAsync(GovernedDocument document, CancellationToken cancellationToken = default)
        {
            Seed(document);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryUnitOfWork : IWorkflowGovernanceUnitOfWork
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
        public FakeTimeProvider(DateTime utcNow) => _utcNow = new DateTimeOffset(utcNow, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class FakeDocumentIntelligenceService : IDocumentIntelligenceService
    {
        public int CallCount { get; private set; }
        public DocumentIntelligenceRequest? LastRequest { get; private set; }
        public Func<DocumentIntelligenceRequest, CancellationToken, Task<DocumentIntelligenceResult>> Handler { get; set; } =
            (req, _) => Task.FromResult(new DocumentIntelligenceResult(
                Provider: "FastApi-TesseractOCR",
                ModelName: "Tesseract-5",
                ModelVersion: "5.4.0",
                Outcome: "Succeeded",
                Artifacts: new[]
                {
                    new AnalysisArtifactOutputDto(Guid.NewGuid(), "OcrDocumentTranscript", $"analysis-artifacts/{req.DocumentVersionId:D}/document-transcript.txt", "text/plain", "SHA-256", "doc-hash"),
                    new AnalysisArtifactOutputDto(Guid.NewGuid(), "OcrPageTranscript", $"analysis-artifacts/{req.DocumentVersionId:D}/pages/1.json", "application/json", "SHA-256", "page-hash")
                },
                Candidates: Array.Empty<ExtractedCandidateFactDto>()
            ));

        public Task<DocumentIntelligenceResult> AnalyzeDocumentAsync(
            DocumentIntelligenceRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            return Handler(request, cancellationToken);
        }
    }

    private readonly DateTime _fixedUtcTime = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
    private readonly InMemoryDocumentAnalysisRepository _analysisRepository = new();
    private readonly InMemoryGovernedDocumentRepository _documentRepository = new();
    private readonly FakeDocumentIntelligenceService _intelligenceService = new();
    private readonly InMemoryUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider;
    private readonly IOptions<DocumentIntelligenceOptions> _options;

    private readonly StartDocumentAnalysisRunCommandHandler _startRunHandler;
    private readonly CompleteDocumentAnalysisCommandHandler _completeRunHandler;
    private readonly FailDocumentAnalysisCommandHandler _failRunHandler;
    private readonly DocumentAnalysisExecutionService _executionService;

    public DocumentAnalysisExecutionServiceTests()
    {
        _timeProvider = new FakeTimeProvider(_fixedUtcTime);
        _options = Options.Create(new DocumentIntelligenceOptions
        {
            BaseUrl = "http://127.0.0.1:8000",
            DefaultLanguageMode = "sin+eng",
            RequestTimeoutSeconds = 30,
            MaxUploadBytes = 25 * 1024 * 1024
        });

        _startRunHandler = new StartDocumentAnalysisRunCommandHandler(
            _analysisRepository,
            _unitOfWork,
            new StartDocumentAnalysisRunCommandValidator(),
            _timeProvider);

        _completeRunHandler = new CompleteDocumentAnalysisCommandHandler(
            _analysisRepository,
            _unitOfWork,
            new CompleteDocumentAnalysisCommandValidator(),
            _timeProvider);

        _failRunHandler = new FailDocumentAnalysisCommandHandler(
            _analysisRepository,
            _unitOfWork,
            new FailDocumentAnalysisCommandValidator(),
            _timeProvider);

        _executionService = new DocumentAnalysisExecutionService(
            _analysisRepository,
            _documentRepository,
            _intelligenceService,
            _startRunHandler,
            _completeRunHandler,
            _failRunHandler,
            _options,
            NullLogger<DocumentAnalysisExecutionService>.Instance);
    }

    private (GovernedDocument doc, DocumentAnalysis analysis, AnalysisRun run, byte[] contentBytes) CreateSampleGovernedAggregate(
        string textContent = "Sample Lease Deed Text",
        string capability = "Ocr",
        string? requestedChecksum = null)
    {
        var caseId = Guid.NewGuid();
        var docId = new GovernedDocumentId(Guid.NewGuid());
        var versionId = new DocumentVersionId(Guid.NewGuid());
        var actorId = Guid.NewGuid();

        var contentBytes = Encoding.UTF8.GetBytes(textContent);
        var sha256Hex = Convert.ToHexString(SHA256.HashData(contentBytes)).ToLowerInvariant();
        var contentRef = "store://docs/sample-deed-v1.pdf";

        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "DocumentSubmitter" },
            new AuthorityScope(AuthorityScopeKind.Global, null),
            _fixedUtcTime.AddHours(-1),
            _fixedUtcTime,
            _fixedUtcTime.AddHours(1));

        var doc = new GovernedDocument(
            docId,
            new LeaseCaseId(caseId),
            "Deed",
            versionId,
            new DocumentChecksum("SHA-256", sha256Hex),
            new DocumentContentReference(contentRef),
            "sample_deed.pdf",
            "application/pdf",
            contentBytes.LongLength,
            actorId,
            _fixedUtcTime,
            authority);

        var runChecksum = requestedChecksum ?? sha256Hex;

        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var analysis = new DocumentAnalysis(
            analysisId,
            docId,
            versionId,
            new DocumentChecksum("SHA-256", runChecksum),
            documentVersionNumber: 1,
            sourceDocumentRevision: 1,
            _fixedUtcTime);

        var runId = new AnalysisRunId(Guid.NewGuid());
        var modelRef = new AnalysisModelReference("FastApi", "Tesseract-5", "5.4.0");
        var capabilities = new[] { new AnalysisCapabilityCode(capability) };

        analysis.RequestRun(runId, modelRef, capabilities, _fixedUtcTime);

        _documentRepository.Seed(doc);
        _analysisRepository.Seed(analysis);

        var run = analysis.Runs.First(r => r.Id == runId);
        return (doc, analysis, run, contentBytes);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_CompletesRunAndAdvancesRevision()
    {
        // Arrange
        var (_, analysis, run, _) = CreateSampleGovernedAggregate();
        var initialRevision = analysis.Revision;

        // Act
        var result = await _executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: analysis.Revision);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Completed", result.Status);
        Assert.Equal(analysis.Id.Value, result.DocumentAnalysisId);
        Assert.Equal(run.Id.Value, result.AnalysisRunId);
        Assert.Equal(2, result.ArtifactCount);
        Assert.Null(result.FailureCode);

        // Verify aggregate state
        var reloaded = await _analysisRepository.GetByIdAsync(analysis.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(initialRevision + 2, reloaded.Revision); // Start (+1) -> Complete (+1)

        var executedRun = reloaded.Runs.First(r => r.Id == run.Id);
        Assert.Equal(AnalysisRunState.Completed, executedRun.State);
        Assert.NotNull(executedRun.Result);
        Assert.Equal(AnalysisResultOutcome.OutputsProduced, executedRun.Result.Outcome);

        // CRITICAL INVARIANT: Zero semantic facts from OCR execution
        Assert.Empty(executedRun.Result.ExtractedFacts);
        Assert.Equal(2, executedRun.Result.Artifacts.Count);

        // Verify commit boundaries: Exactly 2 commits (1 Start, 1 Complete; 0 during external OCR)
        Assert.Equal(2, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task ExecuteAsync_ExactLanguageHintPassed_MapsCorrectlyToRequest()
    {
        // Arrange
        var (_, analysis, run, _) = CreateSampleGovernedAggregate();

        // Act
        await _executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: analysis.Revision,
            languageHint: "sin+eng");

        // Assert
        Assert.Equal(1, _intelligenceService.CallCount);
        Assert.Equal("sin+eng", _intelligenceService.LastRequest!.LanguageHint);
    }

    [Fact]
    public async Task ExecuteAsync_ChecksumMismatch_LastMileStorageIntegrityFailure_FailsRun()
    {
        // Arrange
        var (_, analysis, run, _) = CreateSampleGovernedAggregate();
        _intelligenceService.Handler = (_, _) =>
            throw new DocumentContentIntegrityException(
                "Source document content SHA-256 checksum does not match registered version checksum.",
                "expected-hash",
                "actual-computed-hash");

        // Act
        var result = await _executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: analysis.Revision);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Failed", result.Status);
        Assert.Equal("CHECKSUM_MISMATCH", result.FailureCode);
        Assert.Contains("checksum does not match", result.FailureDescription!);

        var reloaded = await _analysisRepository.GetByIdAsync(analysis.Id);
        Assert.Equal(AnalysisRunState.Failed, reloaded!.Runs.First(r => r.Id == run.Id).State);
        Assert.Equal("CHECKSUM_MISMATCH", reloaded.Runs.First(r => r.Id == run.Id).Failure!.Code);

        // Assert commit boundaries: 1 for Start, 1 for Fail
        Assert.Equal(2, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task ExecuteAsync_ChecksumMismatch_EarlyGuard_FailsRunAndNeverCallsOcr()
    {
        // Arrange: requested run checksum differs from version checksum
        var (_, analysis, run, _) = CreateSampleGovernedAggregate(requestedChecksum: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");

        // Act
        var result = await _executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: analysis.Revision);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Failed", result.Status);
        Assert.Equal("CHECKSUM_MISMATCH", result.FailureCode);

        // Adapter was NEVER called because early guard caught the mismatch
        Assert.Equal(0, _intelligenceService.CallCount);

        var reloaded = await _analysisRepository.GetByIdAsync(analysis.Id);
        Assert.Equal(AnalysisRunState.Failed, reloaded!.Runs.First(r => r.Id == run.Id).State);
        Assert.Equal(2, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task ExecuteAsync_ExactVersionBinding_EnforcesRequestedVersionNotActiveVersion()
    {
        // Arrange: Document has Version 1 and Version 2. Version 2 is active.
        var (doc, analysis, run, _) = CreateSampleGovernedAggregate(textContent: "Version 1 Content");

        var v2Id = new DocumentVersionId(Guid.NewGuid());
        var v2Bytes = Encoding.UTF8.GetBytes("Version 2 Content - Newer Upload");
        var v2Checksum = Convert.ToHexString(SHA256.HashData(v2Bytes)).ToLowerInvariant();
        var v2ContentRef = "store://docs/sample-deed-v2.pdf";

        var v2ActorId = Guid.NewGuid();
        var v2Authority = new VerifiedAuthoritySnapshot(
            v2ActorId,
            new[] { "DocumentSubmitter" },
            new AuthorityScope(AuthorityScopeKind.LeaseCase, doc.LeaseCaseId.Value.ToString()),
            _fixedUtcTime.AddHours(-1),
            _fixedUtcTime,
            _fixedUtcTime.AddHours(1));

        doc.AddVersion(
            newVersionId: v2Id,
            expectedPredecessorVersionId: run.DocumentVersionId,
            documentChecksum: new DocumentChecksum("SHA-256", v2Checksum),
            documentContentReference: new DocumentContentReference(v2ContentRef),
            originalFileName: "sample_deed_v2.pdf",
            mediaType: "application/pdf",
            fileSizeInBytes: v2Bytes.LongLength,
            actorId: v2ActorId,
            actionTime: _fixedUtcTime.AddMinutes(5),
            authoritySnapshot: v2Authority);

        _documentRepository.Seed(doc);

        // Confirm active version is Version 2, but AnalysisRun is bound to Version 1
        Assert.Equal(v2Id, doc.ActiveVersionId);
        Assert.NotEqual(v2Id, run.DocumentVersionId);

        // Act: Execute analysis for Version 1
        var result = await _executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: analysis.Revision);

        // Assert: Coordinator passed Version 1, not Version 2
        Assert.Equal("Completed", result.Status);
        Assert.NotNull(_intelligenceService.LastRequest);
        Assert.Equal(run.DocumentVersionId.Value, _intelligenceService.LastRequest.DocumentVersionId);
        Assert.Equal("store://docs/sample-deed-v1.pdf", _intelligenceService.LastRequest.ContentReference);
    }

    [Fact]
    public async Task ExecuteAsync_ContentNotFound_FailsRun()
    {
        // Arrange
        var (_, analysis, run, _) = CreateSampleGovernedAggregate();
        _intelligenceService.Handler = (_, _) =>
            throw new FileNotFoundException("Document content stream not found in storage.");

        // Act
        var result = await _executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: analysis.Revision);

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Equal("CONTENT_NOT_FOUND", result.FailureCode);
        Assert.Equal(1, _intelligenceService.CallCount);

        var reloaded = await _analysisRepository.GetByIdAsync(analysis.Id);
        Assert.Equal(AnalysisRunState.Failed, reloaded!.Runs.First(r => r.Id == run.Id).State);
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedCapability_FailsRunWithUnsupportedCapability()
    {
        // Arrange
        var (_, analysis, run, _) = CreateSampleGovernedAggregate(capability: "SemanticFactExtraction");

        // Act
        var result = await _executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: analysis.Revision);

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Equal("UNSUPPORTED_CAPABILITY", result.FailureCode);
        Assert.Equal(0, _intelligenceService.CallCount);
    }

    [Theory]
    [InlineData("french")]
    [InlineData("german")]
    [InlineData("invalid-lang")]
    public async Task ExecuteAsync_UnsupportedLanguageHint_ThrowsArgumentExceptionBeforeStarting(string unsupportedLang)
    {
        // Arrange
        var (_, analysis, run, _) = CreateSampleGovernedAggregate();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: analysis.Revision,
            languageHint: unsupportedLang));

        // Aggregate remains untouched in Requested state
        var reloaded = await _analysisRepository.GetByIdAsync(analysis.Id);
        Assert.Equal(AnalysisRunState.Requested, reloaded!.Runs.First(r => r.Id == run.Id).State);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task ExecuteAsync_FileSizeExceedsLimit_FailsRunWithFileTooLarge()
    {
        // Arrange: Options limit is 100 bytes, document is 200 bytes
        var customOptions = Options.Create(new DocumentIntelligenceOptions
        {
            BaseUrl = "http://127.0.0.1:8000",
            DefaultLanguageMode = "sin+eng",
            RequestTimeoutSeconds = 30,
            MaxUploadBytes = 100 // small limit
        });

        var executionService = new DocumentAnalysisExecutionService(
            _analysisRepository,
            _documentRepository,
            _intelligenceService,
            _startRunHandler,
            _completeRunHandler,
            _failRunHandler,
            customOptions,
            NullLogger<DocumentAnalysisExecutionService>.Instance);

        var (_, analysis, run, _) = CreateSampleGovernedAggregate(textContent: new string('A', 200));

        // Act
        var result = await executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: analysis.Revision);

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Equal("FILE_TOO_LARGE", result.FailureCode);
        Assert.Equal(0, _intelligenceService.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_OcrTimeout_FailsRunWithOcrTimeout()
    {
        // Arrange
        var (_, analysis, run, _) = CreateSampleGovernedAggregate();
        _intelligenceService.Handler = (_, _) =>
            throw new DocumentIntelligenceTimeoutException("FastAPI OCR service timed out after 30 seconds.");

        // Act
        var result = await _executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: analysis.Revision);

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Equal("OCR_TIMEOUT", result.FailureCode);
        Assert.Contains("timed out", result.FailureDescription!);

        var reloaded = await _analysisRepository.GetByIdAsync(analysis.Id);
        Assert.Equal(AnalysisRunState.Failed, reloaded!.Runs.First(r => r.Id == run.Id).State);
    }

    [Fact]
    public async Task ExecuteAsync_OcrUnavailable_FailsRunWithServiceUnavailable()
    {
        // Arrange
        var (_, analysis, run, _) = CreateSampleGovernedAggregate();
        _intelligenceService.Handler = (_, _) =>
            throw new DocumentIntelligenceServiceUnavailableException("Service unavailable.");

        // Act
        var result = await _executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: analysis.Revision);

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Equal("SERVICE_UNAVAILABLE", result.FailureCode);
    }

    [Fact]
    public async Task ExecuteAsync_OcrUnsupportedMedia_FailsRunWithUnsupportedMedia()
    {
        // Arrange
        var (_, analysis, run, _) = CreateSampleGovernedAggregate();
        _intelligenceService.Handler = (_, _) =>
            throw new DocumentIntelligenceUnsupportedMediaException("Unsupported media type.");

        // Act
        var result = await _executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: analysis.Revision);

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Equal("UNSUPPORTED_MEDIA", result.FailureCode);
    }

    [Fact]
    public async Task ExecuteAsync_OcrLanguageUnavailable_FailsRunWithLanguageUnavailable()
    {
        // Arrange
        var (_, analysis, run, _) = CreateSampleGovernedAggregate();
        _intelligenceService.Handler = (_, _) =>
            throw new DocumentIntelligenceLanguageUnavailableException("Language pack missing.");

        // Act
        var result = await _executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: analysis.Revision);

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Equal("LANGUAGE_UNAVAILABLE", result.FailureCode);
    }

    [Fact]
    public async Task ExecuteAsync_ArtifactPersistenceFailure_FailsRunWithOcrServerError()
    {
        // Arrange
        var (_, analysis, run, _) = CreateSampleGovernedAggregate();
        _intelligenceService.Handler = (_, _) =>
            throw new DocumentIntelligenceServerException("Artifact persistence failed.");

        // Act
        var result = await _executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: analysis.Revision);

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Equal("OCR_SERVER_ERROR", result.FailureCode);
    }

    [Fact]
    public async Task ExecuteAsync_StaleRevision_ThrowsAnalysisConcurrencyException()
    {
        // Arrange
        var (_, analysis, run, _) = CreateSampleGovernedAggregate();
        var staleRevision = analysis.Revision + 99;

        // Act & Assert
        await Assert.ThrowsAsync<AnalysisConcurrencyException>(() =>
            _executionService.ExecuteAsync(
                analysis.Id.Value,
                run.Id.Value,
                expectedAnalysisRevision: staleRevision));

        Assert.Equal(0, _intelligenceService.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyCompleted_ReturnsCompletedIdempotentlyWithoutOcr()
    {
        // Arrange
        var (_, analysis, run, _) = CreateSampleGovernedAggregate();

        // Transition run to Completed
        analysis.StartRun(run.Id, _fixedUtcTime);
        var artifact = new AnalysisResultArtifactReference(
            new AnalysisResultArtifactId(Guid.NewGuid()),
            "OcrDocumentTranscript",
            "analysis-artifacts/v1/transcript.txt",
            "text/plain",
            new AnalysisArtifactChecksum("SHA-256", "hash"));

        analysis.CompleteRun(
            run.Id,
            new AnalysisRunResultId(Guid.NewGuid()),
            AnalysisResultOutcome.OutputsProduced,
            new[] { artifact },
            Array.Empty<ExtractedFactInput>(),
            _fixedUtcTime.AddSeconds(10));

        _analysisRepository.Seed(analysis);

        // Act
        var result = await _executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: analysis.Revision);

        // Assert
        Assert.Equal("Completed", result.Status);
        Assert.Equal(1, result.ArtifactCount);
        Assert.Equal(0, _intelligenceService.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyFailed_ReturnsFailedIdempotentlyWithoutOcr()
    {
        // Arrange
        var (_, analysis, run, _) = CreateSampleGovernedAggregate();

        // Transition run to Failed
        analysis.StartRun(run.Id, _fixedUtcTime);
        analysis.FailRun(run.Id, new AnalysisRunFailure("PREV_FAIL", "Previous failure"), _fixedUtcTime.AddSeconds(5));
        _analysisRepository.Seed(analysis);

        // Act
        var result = await _executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: analysis.Revision);

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Equal("PREV_FAIL", result.FailureCode);
        Assert.Equal(0, _intelligenceService.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyRunning_ThrowsInvalidOperationException()
    {
        // Arrange
        var (_, analysis, run, _) = CreateSampleGovernedAggregate();

        // Transition run to Running
        analysis.StartRun(run.Id, _fixedUtcTime);
        _analysisRepository.Seed(analysis);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: analysis.Revision));

        Assert.Contains("already in Running state", ex.Message);
        Assert.Equal(0, _intelligenceService.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_Cancellation_BeforeStart_PropagatesOperationCanceledExceptionWithoutMutatingRun()
    {
        // Arrange
        var (_, analysis, run, _) = CreateSampleGovernedAggregate();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled token

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: analysis.Revision,
            cancellationToken: cts.Token));

        // Aggregate remains untouched in Requested state
        var reloaded = await _analysisRepository.GetByIdAsync(analysis.Id);
        Assert.Equal(AnalysisRunState.Requested, reloaded!.Runs.First(r => r.Id == run.Id).State);
        Assert.Equal(0, _unitOfWork.CommitCount);
        Assert.Equal(0, _intelligenceService.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_Cancellation_AfterStart_TransitionsRunToFailedAndPropagatesCancellation()
    {
        // Arrange
        var (_, analysis, run, _) = CreateSampleGovernedAggregate();
        using var cts = new CancellationTokenSource();

        _intelligenceService.Handler = (_, token) =>
        {
            // Simulate cancellation while external intelligence is executing
            cts.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult<DocumentIntelligenceResult>(null!);
        };

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: analysis.Revision,
            cancellationToken: cts.Token));

        // CRITICAL INVARIANT: Run is NOT left stranded in Running state!
        var reloaded = await _analysisRepository.GetByIdAsync(analysis.Id);
        var executedRun = reloaded!.Runs.First(r => r.Id == run.Id);
        Assert.Equal(AnalysisRunState.Failed, executedRun.State);
        Assert.Equal("EXECUTION_CANCELLED", executedRun.Failure!.Code);
        Assert.Contains("cancelled by caller", executedRun.Failure.Description);

        // 2 commits: 1 for Start (Running), 1 for Fail (Failed cleanup)
        Assert.Equal(2, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task ExecuteAsync_Cancellation_AfterStart_AllowsSubsequentExecutionWithoutAlreadyRunningError()
    {
        // Arrange
        var (_, analysis, run, _) = CreateSampleGovernedAggregate();
        using var cts = new CancellationTokenSource();

        _intelligenceService.Handler = (_, token) =>
        {
            cts.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult<DocumentIntelligenceResult>(null!);
        };

        // 1. Initial run is cancelled after start
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: analysis.Revision,
            cancellationToken: cts.Token));

        // 2. Subsequent execution of the cancelled run does NOT throw InvalidOperationException ("already in Running state")
        // Instead, idempotency returns the cleanly persisted Failed result!
        var reloaded = await _analysisRepository.GetByIdAsync(analysis.Id);
        var subsequentResult = await _executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: reloaded!.Revision);

        Assert.Equal("Failed", subsequentResult.Status);
        Assert.Equal("EXECUTION_CANCELLED", subsequentResult.FailureCode);
    }

    [Fact]
    public async Task ExecuteAsync_SanitizesLocalPathsInFailureMessages()
    {
        // Arrange
        var (_, analysis, run, _) = CreateSampleGovernedAggregate();
        _intelligenceService.Handler = (_, _) =>
            throw new Exception("Internal error reading file at C:\\Users\\developer\\secret\\test.pdf");

        // Act
        var result = await _executionService.ExecuteAsync(
            analysis.Id.Value,
            run.Id.Value,
            expectedAnalysisRevision: analysis.Revision);

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.DoesNotContain("C:\\Users\\developer", result.FailureDescription);
        Assert.Contains("[path-redacted]", result.FailureDescription);
    }
}
