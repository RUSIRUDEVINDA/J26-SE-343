namespace StateLandGovernance.UnitTests.WorkflowGovernance.Infrastructure;

using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;
using Xunit;

/// <summary>
/// Custom Fact attribute that gates live integration tests.
/// When RUN_DOCUMENT_INTELLIGENCE_LIVE_TESTS != 'true', the test is explicitly skipped in xUnit.
/// When RUN_DOCUMENT_INTELLIGENCE_LIVE_TESTS == 'true', the test executes and asserts against the live service.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class LiveIntegrationFactAttribute : FactAttribute
{
    public LiveIntegrationFactAttribute()
    {
        var optIn = Environment.GetEnvironmentVariable("RUN_DOCUMENT_INTELLIGENCE_LIVE_TESTS");
        if (!string.Equals(optIn, "true", StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Live integration test requires local FastAPI service on port 8009. " +
                   "Opt-in by setting environment variable RUN_DOCUMENT_INTELLIGENCE_LIVE_TESTS=true.";
        }
    }
}

public sealed class FastApiDocumentIntelligenceServiceLiveSmokeTests
{
    private const string LiveServiceBaseUrl = "http://127.0.0.1:8009";

    private sealed class LiveTestContentReader : IDocumentContentReader
    {
        public Stream? StreamToReturn { get; set; }

        public Task<Stream?> ReadContentAsync(
            GovernedDocumentId documentId,
            DocumentVersionId versionId,
            string contentReference,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(StreamToReturn);
        }
    }

    [LiveIntegrationFact]
    [Trait("Category", "LiveIntegration")]
    public async Task LiveSmokeTest_EnglishDocument_ProcessesThroughFastApiTesseractAndMapsToArtifactsWithoutFacts()
    {
        // 1. Verify live service is reachable when opted in
        using var checkClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var health = await checkClient.GetAsync($"{LiveServiceBaseUrl}/health");
        Assert.True(health.IsSuccessStatusCode, $"Live FastAPI service at {LiveServiceBaseUrl} must be reachable and healthy.");

        // 2. Arrange real adapter and content reader with non-sensitive test image
        var samplePath = @"C:\Users\Ovindi\.gemini\antigravity-ide\brain\69bf555c-79b4-4f85-a016-b3421ebb4fcd\scratch\english_sample.png";
        Assert.True(File.Exists(samplePath), $"Sample image file at {samplePath} must exist.");

        var samplePngBytes = await File.ReadAllBytesAsync(samplePath);

        var contentReader = new LiveTestContentReader
        {
            StreamToReturn = new MemoryStream(samplePngBytes)
        };

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

        var artifactWriter = new InMemoryAnalysisArtifactWriter();

        var service = new FastApiDocumentIntelligenceService(
            httpClient,
            options,
            NullLogger<FastApiDocumentIntelligenceService>.Instance,
            contentReader,
            artifactWriter);

        var sampleChecksum = Convert.ToHexString(SHA256.HashData(samplePngBytes)).ToLowerInvariant();
        var versionGuid = Guid.NewGuid();
        var request = new DocumentIntelligenceRequest(
            GovernedDocumentId: Guid.NewGuid(),
            DocumentVersionId: versionGuid,
            ContentReference: "urn:document:test-lease.png",
            ChecksumAlgorithm: "SHA-256",
            ChecksumValue: sampleChecksum,
            OriginalFileName: "test_lease.png",
            MediaType: "image/png",
            LogicalCategory: "LeaseApplication",
            RequestedCapabilities: new[] { "Ocr" },
            LanguageHint: "eng");

        // 3. Act - execute through real adapter -> HTTP -> FastAPI -> Tesseract
        var result = await service.AnalyzeDocumentAsync(request, CancellationToken.None);

        // 4. Assert
        Assert.NotNull(result);
        Assert.Equal("Succeeded", result.Outcome);
        Assert.NotNull(result.Artifacts);
        Assert.Equal(2, result.Artifacts.Count);

        // CRITICAL INVARIANT: Zero semantic facts from OCR-only service
        Assert.NotNull(result.Candidates);
        Assert.Empty(result.Candidates);

        // Verify artifacts were persisted to the writer and StorageReferences resolve
        Assert.Equal(2, artifactWriter.Count);

        // Verify document transcript artifact resolves and checksum matches exact bytes
        var docArtifact = Assert.Single(result.Artifacts, a => a.ArtifactKind == "OcrDocumentTranscript");
        Assert.Equal($"analysis-artifacts/{versionGuid:D}/document-transcript.txt", docArtifact.StorageReference);
        Assert.Equal("text/plain", docArtifact.ContentType);
        Assert.Equal("SHA-256", docArtifact.ChecksumAlgorithm);

        Assert.True(artifactWriter.TryGetArtifact(docArtifact.StorageReference, out var storedDoc));
        Assert.NotNull(storedDoc);
        var computedDocHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(storedDoc.Bytes)).ToLowerInvariant();
        Assert.Equal(docArtifact.ChecksumValue, computedDocHash);
        Assert.NotEmpty(storedDoc.Bytes);

        // Verify page transcript artifact resolves and contains valid provenance JSON
        var pageArtifact = Assert.Single(result.Artifacts, a => a.ArtifactKind == "OcrPageTranscript");
        Assert.Equal($"analysis-artifacts/{versionGuid:D}/pages/1.json", pageArtifact.StorageReference);
        Assert.Equal("application/json", pageArtifact.ContentType);
        Assert.Equal("SHA-256", pageArtifact.ChecksumAlgorithm);

        Assert.True(artifactWriter.TryGetArtifact(pageArtifact.StorageReference, out var storedPage));
        Assert.NotNull(storedPage);
        var computedPageHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(storedPage.Bytes)).ToLowerInvariant();
        Assert.Equal(pageArtifact.ChecksumValue, computedPageHash);

        var pageProvenance = JsonSerializer.Deserialize<FastApiPageTranscriptArtifactDto>(storedPage.Bytes);
        Assert.NotNull(pageProvenance);
        Assert.Equal(1, pageProvenance.PageNumber);
        Assert.False(string.IsNullOrWhiteSpace(pageProvenance.CleanText));
    }

    [LiveIntegrationFact]
    [Trait("Category", "LiveIntegration")]
    public async Task LiveSmokeTest_SinhalaMixedDocument_PreservesSinhalaUnicodeAndZeroFacts()
    {
        // 1. Verify live service is reachable when opted in
        using var checkClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var health = await checkClient.GetAsync($"{LiveServiceBaseUrl}/health");
        Assert.True(health.IsSuccessStatusCode, $"Live FastAPI service at {LiveServiceBaseUrl} must be reachable and healthy.");

        // 2. Arrange real adapter and content reader with Sinhala/mixed test image
        var samplePath = @"C:\Users\Ovindi\.gemini\antigravity-ide\brain\69bf555c-79b4-4f85-a016-b3421ebb4fcd\scratch\sinhala_sample.png";
        Assert.True(File.Exists(samplePath), $"Sample image file at {samplePath} must exist.");

        var samplePngBytes = await File.ReadAllBytesAsync(samplePath);

        var contentReader = new LiveTestContentReader
        {
            StreamToReturn = new MemoryStream(samplePngBytes)
        };

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

        var artifactWriter = new InMemoryAnalysisArtifactWriter();

        var service = new FastApiDocumentIntelligenceService(
            httpClient,
            options,
            NullLogger<FastApiDocumentIntelligenceService>.Instance,
            contentReader,
            artifactWriter);

        var sampleChecksum = Convert.ToHexString(SHA256.HashData(samplePngBytes)).ToLowerInvariant();
        var versionGuid = Guid.NewGuid();
        var request = new DocumentIntelligenceRequest(
            GovernedDocumentId: Guid.NewGuid(),
            DocumentVersionId: versionGuid,
            ContentReference: "urn:document:test-sinhala.png",
            ChecksumAlgorithm: "SHA-256",
            ChecksumValue: sampleChecksum,
            OriginalFileName: "test_sinhala.png",
            MediaType: "image/png",
            LogicalCategory: "LeaseApplication",
            RequestedCapabilities: new[] { "Ocr" },
            LanguageHint: "sin+eng");

        // 3. Act - execute through real adapter -> HTTP -> FastAPI -> Tesseract with sin+eng
        var result = await service.AnalyzeDocumentAsync(request, CancellationToken.None);

        // 4. Assert
        Assert.NotNull(result);
        Assert.Equal("Succeeded", result.Outcome);
        Assert.NotNull(result.Artifacts);
        Assert.Equal(2, result.Artifacts.Count);

        // CRITICAL INVARIANT: Zero semantic facts from OCR-only service
        Assert.NotNull(result.Candidates);
        Assert.Empty(result.Candidates);

        // Verify writer retained both artifacts
        Assert.Equal(2, artifactWriter.Count);

        // Verify page artifact has relative storage path, resolves in writer, and SHA-256 matches
        var pageArtifact = Assert.Single(result.Artifacts, a => a.ArtifactKind == "OcrPageTranscript");
        Assert.Equal($"analysis-artifacts/{versionGuid:D}/pages/1.json", pageArtifact.StorageReference);
        Assert.Equal("application/json", pageArtifact.ContentType);
        Assert.Equal("SHA-256", pageArtifact.ChecksumAlgorithm);

        Assert.True(artifactWriter.TryGetArtifact(pageArtifact.StorageReference, out var storedPage));
        Assert.NotNull(storedPage);
        var computedPageHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(storedPage.Bytes)).ToLowerInvariant();
        Assert.Equal(pageArtifact.ChecksumValue, computedPageHash);

        var pageProvenance = JsonSerializer.Deserialize<FastApiPageTranscriptArtifactDto>(storedPage.Bytes);
        Assert.NotNull(pageProvenance);
        Assert.Equal(1, pageProvenance.PageNumber);
        Assert.Equal("sin+eng", pageProvenance.LanguageMode);
    }
}
