namespace StateLandGovernance.UnitTests.WorkflowGovernance.Infrastructure;

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Validators;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Services;
using Xunit;

public sealed class DocumentAnalysisExecutionLiveSmokeTests
{
    private const string LiveServiceBaseUrl = "http://127.0.0.1:8009";

    private sealed class InMemoryUnitOfWork : IWorkflowGovernanceUnitOfWork
    {
        public int CommitCount { get; private set; }

        public Task<int> CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class InMemoryDocumentAnalysisRepository : IDocumentAnalysisRepository
    {
        private readonly System.Collections.Generic.Dictionary<Guid, DocumentAnalysis> _analyses = new();

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
        private readonly System.Collections.Generic.Dictionary<Guid, GovernedDocument> _documents = new();

        public void Seed(GovernedDocument document) => _documents[document.Id.Value] = document;

        public Task<GovernedDocument?> GetByIdAsync(GovernedDocumentId id, CancellationToken cancellationToken = default)
        {
            _documents.TryGetValue(id.Value, out var doc);
            return Task.FromResult(doc);
        }

        public Task<System.Collections.Generic.IReadOnlyList<GovernedDocument>> GetByLeaseCaseIdAsync(LeaseCaseId leaseCaseId, CancellationToken cancellationToken = default)
        {
            var docs = _documents.Values.Where(d => d.LeaseCaseId == leaseCaseId).ToList();
            return Task.FromResult<System.Collections.Generic.IReadOnlyList<GovernedDocument>>(docs);
        }

        public Task AddAsync(GovernedDocument document, CancellationToken cancellationToken = default)
        {
            Seed(document);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FakeTimeProvider(DateTime utcNow) => _utcNow = new DateTimeOffset(utcNow, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    [LiveIntegrationFact]
    [Trait("Category", "LiveIntegration")]
    public async Task LiveOrchestrationSmokeTest_EnglishDocument_CompletesRunAndPreservesArtifactsWithoutFacts()
    {
        // 1. Verify live service is reachable when opted in
        using var checkClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var health = await checkClient.GetAsync($"{LiveServiceBaseUrl}/health");
        Assert.True(health.IsSuccessStatusCode, $"Live FastAPI service at {LiveServiceBaseUrl} must be reachable.");

        var samplePath = @"C:\Users\Ovindi\.gemini\antigravity-ide\brain\69bf555c-79b4-4f85-a016-b3421ebb4fcd\scratch\english_sample.png";
        Assert.True(File.Exists(samplePath), $"Sample image file at {samplePath} must exist.");

        var pngBytes = await File.ReadAllBytesAsync(samplePath);
        var sha256Hex = Convert.ToHexString(SHA256.HashData(pngBytes)).ToLowerInvariant();

        // 2. Setup repos, writers, readers
        var fixedTime = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new FakeTimeProvider(fixedTime);
        var analysisRepo = new InMemoryDocumentAnalysisRepository();
        var docRepo = new InMemoryGovernedDocumentRepository();
        var contentReader = new TestDocumentContentReader();
        var artifactWriter = new InMemoryAnalysisArtifactWriter();
        var unitOfWork = new InMemoryUnitOfWork();

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(LiveServiceBaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        var options = Options.Create(new DocumentIntelligenceOptions
        {
            BaseUrl = LiveServiceBaseUrl,
            DefaultLanguageMode = "sin+eng",
            RequestTimeoutSeconds = 30
        });

        var intelligenceService = new FastApiDocumentIntelligenceService(
            httpClient,
            options,
            NullLogger<FastApiDocumentIntelligenceService>.Instance,
            contentReader,
            artifactWriter);

        var startHandler = new StartDocumentAnalysisRunCommandHandler(
            analysisRepo,
            unitOfWork,
            new StartDocumentAnalysisRunCommandValidator(),
            timeProvider);

        var completeHandler = new CompleteDocumentAnalysisCommandHandler(
            analysisRepo,
            unitOfWork,
            new CompleteDocumentAnalysisCommandValidator(),
            timeProvider);

        var failHandler = new FailDocumentAnalysisCommandHandler(
            analysisRepo,
            unitOfWork,
            new FailDocumentAnalysisCommandValidator(),
            timeProvider);

        var executionService = new DocumentAnalysisExecutionService(
            analysisRepo,
            docRepo,
            intelligenceService,
            startHandler,
            completeHandler,
            failHandler,
            options,
            NullLogger<DocumentAnalysisExecutionService>.Instance);

        // 3. Create governed document & analysis aggregate
        var caseId = Guid.NewGuid();
        var docId = new GovernedDocumentId(Guid.NewGuid());
        var versionId = new DocumentVersionId(Guid.NewGuid());
        var contentRef = "store://gov-docs/sample-english.png";
        var actorId = Guid.NewGuid();

        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "DocumentSubmitter" },
            new AuthorityScope(AuthorityScopeKind.Global, null),
            fixedTime.AddHours(-1),
            fixedTime,
            fixedTime.AddHours(1));

        var governedDoc = new GovernedDocument(
            docId,
            new LeaseCaseId(caseId),
            "Deed",
            versionId,
            new DocumentChecksum("SHA-256", sha256Hex),
            new DocumentContentReference(contentRef),
            "sample_english.png",
            "image/png",
            pngBytes.LongLength,
            actorId,
            fixedTime,
            authority);

        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var analysis = new DocumentAnalysis(
            analysisId,
            docId,
            versionId,
            new DocumentChecksum("SHA-256", sha256Hex),
            documentVersionNumber: 1,
            sourceDocumentRevision: 1,
            fixedTime);

        var runId = new AnalysisRunId(Guid.NewGuid());
        var modelRef = new AnalysisModelReference("FastApi", "Tesseract-5", "5.4.0");
        var capabilities = new[] { new AnalysisCapabilityCode("Ocr") };

        analysis.RequestRun(runId, modelRef, capabilities, fixedTime);

        docRepo.Seed(governedDoc);
        analysisRepo.Seed(analysis);
        contentReader.Store(docId.Value, versionId.Value, contentRef, pngBytes);

        // 4. Act: Execute orchestrated analysis
        var result = await executionService.ExecuteAsync(
            analysisId.Value,
            runId.Value,
            expectedAnalysisRevision: analysis.Revision,
            languageHint: "eng");

        // 5. Assert: Completed lifecycle and preserved artifacts
        Assert.NotNull(result);
        Assert.Equal("Completed", result.Status);
        Assert.True(result.ArtifactCount >= 2);
        Assert.Null(result.FailureCode);

        // Verify aggregate state
        var reloaded = await analysisRepo.GetByIdAsync(analysisId);
        Assert.NotNull(reloaded);
        Assert.Equal(4, reloaded.Revision); // Created (1) -> Requested (2) -> Started (3) -> Completed (4)

        var executedRun = reloaded.Runs.First(r => r.Id == runId);
        Assert.Equal(AnalysisRunState.Completed, executedRun.State);
        Assert.NotNull(executedRun.Result);
        Assert.Equal(AnalysisResultOutcome.OutputsProduced, executedRun.Result.Outcome);
        Assert.Empty(executedRun.Result.ExtractedFacts); // CRITICAL: 0 semantic facts
        Assert.True(executedRun.Result.Artifacts.Count >= 2);

        // Verify artifact retrieval from writer
        var docArtifact = executedRun.Result.Artifacts.First(a => a.ArtifactKind == "OcrDocumentTranscript");
        Assert.True(artifactWriter.TryGetArtifact(docArtifact.StorageReference, out var storedTranscript));
        Assert.NotNull(storedTranscript);
        var transcriptText = Encoding.UTF8.GetString(storedTranscript.Bytes);
        Assert.False(string.IsNullOrWhiteSpace(transcriptText));
    }

    [LiveIntegrationFact]
    [Trait("Category", "LiveIntegration")]
    public async Task LiveOrchestrationSmokeTest_SinhalaMixedDocument_SurvivesUtf8AndProducesZeroFacts()
    {
        // 1. Verify live service is reachable when opted in
        using var checkClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var health = await checkClient.GetAsync($"{LiveServiceBaseUrl}/health");
        Assert.True(health.IsSuccessStatusCode, $"Live FastAPI service at {LiveServiceBaseUrl} must be reachable.");

        var samplePath = @"C:\Users\Ovindi\.gemini\antigravity-ide\brain\69bf555c-79b4-4f85-a016-b3421ebb4fcd\scratch\sinhala_sample.png";
        Assert.True(File.Exists(samplePath), $"Sample image file at {samplePath} must exist.");

        var pngBytes = await File.ReadAllBytesAsync(samplePath);
        var sha256Hex = Convert.ToHexString(SHA256.HashData(pngBytes)).ToLowerInvariant();

        // 2. Setup repos, writers, readers
        var fixedTime = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new FakeTimeProvider(fixedTime);
        var analysisRepo = new InMemoryDocumentAnalysisRepository();
        var docRepo = new InMemoryGovernedDocumentRepository();
        var contentReader = new TestDocumentContentReader();
        var artifactWriter = new InMemoryAnalysisArtifactWriter();
        var unitOfWork = new InMemoryUnitOfWork();

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(LiveServiceBaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        var options = Options.Create(new DocumentIntelligenceOptions
        {
            BaseUrl = LiveServiceBaseUrl,
            DefaultLanguageMode = "sin+eng",
            RequestTimeoutSeconds = 30
        });

        var intelligenceService = new FastApiDocumentIntelligenceService(
            httpClient,
            options,
            NullLogger<FastApiDocumentIntelligenceService>.Instance,
            contentReader,
            artifactWriter);

        var startHandler = new StartDocumentAnalysisRunCommandHandler(
            analysisRepo,
            unitOfWork,
            new StartDocumentAnalysisRunCommandValidator(),
            timeProvider);

        var completeHandler = new CompleteDocumentAnalysisCommandHandler(
            analysisRepo,
            unitOfWork,
            new CompleteDocumentAnalysisCommandValidator(),
            timeProvider);

        var failHandler = new FailDocumentAnalysisCommandHandler(
            analysisRepo,
            unitOfWork,
            new FailDocumentAnalysisCommandValidator(),
            timeProvider);

        var executionService = new DocumentAnalysisExecutionService(
            analysisRepo,
            docRepo,
            intelligenceService,
            startHandler,
            completeHandler,
            failHandler,
            options,
            NullLogger<DocumentAnalysisExecutionService>.Instance);

        // 3. Create governed document & analysis aggregate
        var caseId = Guid.NewGuid();
        var docId = new GovernedDocumentId(Guid.NewGuid());
        var versionId = new DocumentVersionId(Guid.NewGuid());
        var contentRef = "store://gov-docs/sample-sinhala.png";
        var actorId = Guid.NewGuid();

        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "DocumentSubmitter" },
            new AuthorityScope(AuthorityScopeKind.Global, null),
            fixedTime.AddHours(-1),
            fixedTime,
            fixedTime.AddHours(1));

        var governedDoc = new GovernedDocument(
            docId,
            new LeaseCaseId(caseId),
            "Deed",
            versionId,
            new DocumentChecksum("SHA-256", sha256Hex),
            new DocumentContentReference(contentRef),
            "sample_sinhala.png",
            "image/png",
            pngBytes.LongLength,
            actorId,
            fixedTime,
            authority);

        var analysisId = new DocumentAnalysisId(Guid.NewGuid());
        var analysis = new DocumentAnalysis(
            analysisId,
            docId,
            versionId,
            new DocumentChecksum("SHA-256", sha256Hex),
            documentVersionNumber: 1,
            sourceDocumentRevision: 1,
            fixedTime);

        var runId = new AnalysisRunId(Guid.NewGuid());
        var modelRef = new AnalysisModelReference("FastApi", "Tesseract-5", "5.4.0");
        var capabilities = new[] { new AnalysisCapabilityCode("Ocr") };

        analysis.RequestRun(runId, modelRef, capabilities, fixedTime);

        docRepo.Seed(governedDoc);
        analysisRepo.Seed(analysis);
        contentReader.Store(docId.Value, versionId.Value, contentRef, pngBytes);

        // 4. Act: Execute orchestrated analysis with sin+eng
        var result = await executionService.ExecuteAsync(
            analysisId.Value,
            runId.Value,
            expectedAnalysisRevision: analysis.Revision,
            languageHint: "sin+eng");

        // 5. Assert: Completed lifecycle and preserved Sinhala Unicode
        Assert.NotNull(result);
        Assert.Equal("Completed", result.Status);
        Assert.True(result.ArtifactCount >= 2);
        Assert.Null(result.FailureCode);

        var reloaded = await analysisRepo.GetByIdAsync(analysisId);
        Assert.NotNull(reloaded);

        var executedRun = reloaded.Runs.First(r => r.Id == runId);
        Assert.Equal(AnalysisRunState.Completed, executedRun.State);
        Assert.NotNull(executedRun.Result);
        Assert.Equal(AnalysisResultOutcome.OutputsProduced, executedRun.Result.Outcome);
        Assert.Empty(executedRun.Result.ExtractedFacts); // CRITICAL: 0 semantic facts

        // Verify artifact retrieval from writer and Sinhala Unicode presence
        var docArtifact = executedRun.Result.Artifacts.First(a => a.ArtifactKind == "OcrDocumentTranscript");
        Assert.True(artifactWriter.TryGetArtifact(docArtifact.StorageReference, out var storedTranscript));
        Assert.NotNull(storedTranscript);
        var transcriptText = Encoding.UTF8.GetString(storedTranscript.Bytes);
        Assert.True(transcriptText.Any(c => c >= '\u0D80' && c <= '\u0DFF'), "OCR transcript must contain Sinhala Unicode characters.");
    }
}
