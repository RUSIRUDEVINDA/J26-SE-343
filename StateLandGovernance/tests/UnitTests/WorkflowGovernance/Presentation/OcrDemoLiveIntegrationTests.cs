namespace StateLandGovernance.UnitTests.WorkflowGovernance.Presentation;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StateLandGovernance.UnitTests.WorkflowGovernance.Infrastructure;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Validators;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Handoff;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Services;
using StateLandGovernance.WorkflowGovernance.Presentation.Configuration;
using StateLandGovernance.WorkflowGovernance.Presentation.Controllers;
using StateLandGovernance.WorkflowGovernance.Presentation.DTOs;
using Xunit;

public sealed class OcrDemoLiveIntegrationTests
{
    private const string LiveServiceBaseUrl = "http://127.0.0.1:8009";
    private const string Proposal1Path = @"C:\Users\Ovindi\Downloads\Proposal1.pdf";

    private sealed class InMemoryDocumentContentStore : IDocumentContentWriter, IDocumentContentReader
    {
        private readonly ConcurrentDictionary<(Guid DocumentId, Guid VersionId, string ContentReference), byte[]> _storage = new();

        public async Task<DocumentContentReceipt> WriteAsync(
            Stream contentStream,
            string fileName,
            string mediaType,
            Guid? documentId,
            Guid? versionId,
            CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            await contentStream.CopyToAsync(ms, cancellationToken);
            var bytes = ms.ToArray();
            var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var docGuid = documentId ?? Guid.NewGuid();
            var verGuid = versionId ?? Guid.NewGuid();
            var contentRef = $"store://gov-docs/{docGuid}/{verGuid}/{fileName}";
            _storage[(docGuid, verGuid, contentRef)] = bytes;

            return new DocumentContentReceipt(
                ContentReference: contentRef,
                ChecksumAlgorithm: "SHA-256",
                ChecksumValue: checksum,
                OriginalFileName: fileName,
                MediaType: mediaType,
                FileSizeInBytes: bytes.Length);
        }

        public Task<Stream?> ReadContentAsync(
            GovernedDocumentId documentId,
            DocumentVersionId versionId,
            string contentReference,
            CancellationToken cancellationToken = default)
        {
            if (_storage.TryGetValue((documentId.Value, versionId.Value, contentReference), out var bytes))
            {
                return Task.FromResult<Stream?>(new MemoryStream(bytes));
            }
            return Task.FromResult<Stream?>(null);
        }
    }

    private sealed class InMemoryArtifactStore : IAnalysisArtifactWriter, IAnalysisArtifactReader
    {
        private readonly ConcurrentDictionary<string, byte[]> _storage = new(StringComparer.Ordinal);

        public Task<AnalysisArtifactWriteReceipt> WriteAsync(
            string proposedKey,
            string contentType,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken = default)
        {
            var bytes = content.ToArray();
            var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            _storage[proposedKey] = bytes;

            return Task.FromResult(new AnalysisArtifactWriteReceipt(
                proposedKey,
                "SHA-256",
                checksum,
                bytes.LongLength));
        }

        public Task<ReadOnlyMemory<byte>?> ReadAsync(
            string storageReference,
            CancellationToken cancellationToken = default)
        {
            if (_storage.TryGetValue(storageReference, out var bytes))
            {
                return Task.FromResult<ReadOnlyMemory<byte>?>(new ReadOnlyMemory<byte>(bytes));
            }
            return Task.FromResult<ReadOnlyMemory<byte>?>(null);
        }
    }

    private sealed class InMemoryLeaseCaseRepo : ILeaseCaseRepository
    {
        private readonly ConcurrentDictionary<Guid, LeaseCase> _cases = new();
        public Task<LeaseCase?> GetByIdAsync(LeaseCaseId id, CancellationToken cancellationToken = default)
        {
            _cases.TryGetValue(id.Value, out var c);
            return Task.FromResult(c);
        }
        public Task<LeaseCase?> GetByApplicationReferenceAsync(string applicationReference, CancellationToken cancellationToken = default)
        {
            var c = _cases.Values.FirstOrDefault(x => x.ApplicationReference == applicationReference);
            return Task.FromResult(c);
        }
        public Task AddAsync(LeaseCase leaseCase, CancellationToken cancellationToken = default)
        {
            _cases[leaseCase.Id.Value] = leaseCase;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryGovernedDocRepo : IGovernedDocumentRepository
    {
        private readonly ConcurrentDictionary<Guid, GovernedDocument> _docs = new();
        public Task<GovernedDocument?> GetByIdAsync(GovernedDocumentId id, CancellationToken cancellationToken = default)
        {
            _docs.TryGetValue(id.Value, out var d);
            return Task.FromResult(d);
        }
        public Task<IReadOnlyList<GovernedDocument>> GetByLeaseCaseIdAsync(LeaseCaseId leaseCaseId, CancellationToken cancellationToken = default)
        {
            var list = _docs.Values.Where(d => d.LeaseCaseId == leaseCaseId).ToList();
            return Task.FromResult<IReadOnlyList<GovernedDocument>>(list);
        }
        public Task AddAsync(GovernedDocument document, CancellationToken cancellationToken = default)
        {
            _docs[document.Id.Value] = document;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryDocumentAnalysisRepo : IDocumentAnalysisRepository
    {
        private readonly ConcurrentDictionary<Guid, DocumentAnalysis> _analyses = new();
        public Task<DocumentAnalysis?> GetByIdAsync(DocumentAnalysisId id, CancellationToken cancellationToken = default)
        {
            _analyses.TryGetValue(id.Value, out var a);
            return Task.FromResult(a);
        }
        public Task<DocumentAnalysis?> GetByDocumentVersionIdAsync(DocumentVersionId documentVersionId, CancellationToken cancellationToken = default)
        {
            var a = _analyses.Values.FirstOrDefault(x => x.DocumentVersionId == documentVersionId);
            return Task.FromResult(a);
        }
        public Task AddAsync(DocumentAnalysis documentAnalysis, CancellationToken cancellationToken = default)
        {
            _analyses[documentAnalysis.Id.Value] = documentAnalysis;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpUnitOfWork : IWorkflowGovernanceUnitOfWork
    {
        public Task<int> CommitAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class StubFailingIntelligenceService : IDocumentIntelligenceService
    {
        private readonly IAnalysisArtifactWriter _artifactWriter;
        private readonly IReadOnlyList<string> _warnings;

        public StubFailingIntelligenceService(
            IAnalysisArtifactWriter artifactWriter,
            IReadOnlyList<string>? warnings = null)
        {
            _artifactWriter = artifactWriter;
            _warnings = warnings ?? new[] { "Upstream Gemini extraction timed out after 60s" };
        }

        public async Task<DocumentIntelligenceResult> AnalyzeDocumentAsync(
            DocumentIntelligenceRequest request,
            CancellationToken cancellationToken = default)
        {
            var canonicalFields = new[]
            {
                ("applicant_name", "Applicant Name"),
                ("applicant_address", "Applicant Address"),
                ("applicant_nic_passport", "Applicant NIC / Passport"),
                ("land_location", "Land Location"),
                ("district", "District"),
                ("divisional_secretariat_division", "Divisional Secretariat Division"),
                ("grama_niladhari_division", "Grama Niladhari Division"),
                ("land_extent", "Land Extent"),
                ("land_extent_unit", "Land Extent Unit"),
                ("proposed_land_use", "Proposed Land Use"),
                ("project_title", "Project Title"),
                ("investment_amount", "Investment Amount"),
                ("investment_currency", "Investment Currency"),
                ("expected_employment", "Expected Employment"),
                ("project_description", "Project Description"),
                ("supporting_document_references", "Supporting Document References"),
                ("legal_statutory_basis", "Legal Statutory Basis")
            };

            var inventoryItems = canonicalFields.Select(f => new
            {
                field_code = f.Item1,
                display_name = f.Item2,
                status = "Unavailable",
                preferred_value = (string?)null,
                raw_value = (string?)null,
                page_number = (int?)null,
                confidence = (double?)null,
                is_grounded = false,
                candidate_count = 0,
                source_type = (string?)null,
                normalized_english_value = (string?)null
            }).ToArray();

            var semanticPayload = new
            {
                document_type = "Unavailable",
                classification = new { code = "Unavailable", confidence = (double?)null },
                facts = Array.Empty<object>(),
                all_candidates = Array.Empty<object>(),
                field_inventory = inventoryItems,
                warnings = _warnings,
                candidate_count = 0,
                accepted_count = 0,
                rejected_count = 0,
                conflict_count = 0,
                ungrounded_count = 0,
                model_identifier = "gemini-flash-latest",
                prompt_version = "v4.0"
            };

            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(semanticPayload);
            var receipt = await _artifactWriter.WriteAsync(
                $"analysis-artifacts/{request.DocumentVersionId:D}/semantic-extraction.json",
                "application/json",
                jsonBytes,
                cancellationToken);

            var artifact = new AnalysisArtifactOutputDto(
                ArtifactId: Guid.NewGuid(),
                ArtifactKind: "SemanticRawResponse",
                StorageReference: receipt.StorageReference,
                ContentType: "application/json",
                ChecksumAlgorithm: receipt.ChecksumAlgorithm,
                ChecksumValue: receipt.ChecksumValue);

            return new DocumentIntelligenceResult(
                Provider: "gemini",
                ModelName: "gemini-flash-latest",
                ModelVersion: "gemini-3.8-flash",
                Outcome: "NoFindings",
                Artifacts: new[] { artifact },
                Candidates: Array.Empty<ExtractedCandidateFactDto>(),
                Warnings: _warnings);
        }
    }

    private sealed class TestServicePipeline
    {
        public InMemoryDocumentContentStore ContentStore { get; } = new();
        public InMemoryArtifactStore ArtifactStore { get; } = new();
        public InMemoryLeaseCaseRepo LeaseCaseRepo { get; } = new();
        public InMemoryGovernedDocRepo DocRepo { get; } = new();
        public InMemoryDocumentAnalysisRepo AnalysisRepo { get; } = new();
        public NoOpUnitOfWork UnitOfWork { get; } = new();
        public IDocumentIntelligenceService IntelligenceService { get; }
        public DocumentAnalysisExecutionService ExecutionService { get; }
        public RegisterLeaseCaseCommandHandler RegisterCaseHandler { get; }
        public RegisterGovernedDocumentCommandHandler RegisterDocHandler { get; }
        public RequestDocumentAnalysisCommandHandler RequestAnalysisHandler { get; }
        public IOptions<OcrDemoOptions> DemoOptions { get; }
        public TimeProvider TimeProvider { get; }

        public TestServicePipeline(
            string serviceBaseUrl = LiveServiceBaseUrl,
            IDocumentIntelligenceService? intelligenceServiceOverride = null,
            InMemoryArtifactStore? artifactStore = null)
        {
            TimeProvider = TimeProvider.System;
            if (artifactStore != null)
            {
                ArtifactStore = artifactStore;
            }

            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(serviceBaseUrl),
                Timeout = TimeSpan.FromMinutes(5)
            };

            var diOptions = Options.Create(new DocumentIntelligenceOptions
            {
                BaseUrl = serviceBaseUrl,
                DefaultLanguageMode = "sin",
                RequestTimeoutSeconds = 300,
                MaxUploadBytes = 50 * 1024 * 1024
            });

            IntelligenceService = intelligenceServiceOverride ?? new FastApiDocumentIntelligenceService(
                httpClient,
                diOptions,
                NullLogger<FastApiDocumentIntelligenceService>.Instance,
                ContentStore,
                ArtifactStore);

            var startHandler = new StartDocumentAnalysisRunCommandHandler(
                AnalysisRepo, UnitOfWork, new StartDocumentAnalysisRunCommandValidator(), TimeProvider);
            var completeHandler = new CompleteDocumentAnalysisCommandHandler(
                AnalysisRepo, UnitOfWork, new CompleteDocumentAnalysisCommandValidator(), TimeProvider);
            var failHandler = new FailDocumentAnalysisCommandHandler(
                AnalysisRepo, UnitOfWork, new FailDocumentAnalysisCommandValidator(), TimeProvider);

            ExecutionService = new DocumentAnalysisExecutionService(
                AnalysisRepo,
                DocRepo,
                IntelligenceService,
                startHandler,
                completeHandler,
                failHandler,
                diOptions,
                NullLogger<DocumentAnalysisExecutionService>.Instance);

            RegisterCaseHandler = new RegisterLeaseCaseCommandHandler(
                LeaseCaseRepo, UnitOfWork, new RegisterLeaseCaseCommandValidator(), TimeProvider);
            RegisterDocHandler = new RegisterGovernedDocumentCommandHandler(
                DocRepo, LeaseCaseRepo, UnitOfWork, new RegisterGovernedDocumentCommandValidator(), TimeProvider);
            RequestAnalysisHandler = new RequestDocumentAnalysisCommandHandler(
                AnalysisRepo, DocRepo, UnitOfWork, new RequestDocumentAnalysisCommandValidator(), TimeProvider);

            DemoOptions = Options.Create(new OcrDemoOptions
            {
                Enabled = true,
                MaxUploadBytes = 50 * 1024 * 1024
            });
        }
    }

    private static async Task<WebApplication> CreateTestHostAsync(TestServicePipeline pipeline)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.Services.AddControllers()
            .AddApplicationPart(typeof(OcrDemoController).Assembly);

        builder.Services.AddSingleton<IDocumentContentWriter>(pipeline.ContentStore);
        builder.Services.AddSingleton<IDocumentContentReader>(pipeline.ContentStore);
        builder.Services.AddSingleton<IAnalysisArtifactWriter>(pipeline.ArtifactStore);
        builder.Services.AddSingleton<IAnalysisArtifactReader>(pipeline.ArtifactStore);
        builder.Services.AddSingleton<ILeaseCaseRepository>(pipeline.LeaseCaseRepo);
        builder.Services.AddSingleton<IGovernedDocumentRepository>(pipeline.DocRepo);
        builder.Services.AddSingleton<IDocumentAnalysisRepository>(pipeline.AnalysisRepo);
        builder.Services.AddSingleton<IWorkflowGovernanceUnitOfWork>(pipeline.UnitOfWork);
        builder.Services.AddSingleton<IDocumentAnalysisExecutionService>(pipeline.ExecutionService);
        builder.Services.AddSingleton(pipeline.RegisterCaseHandler);
        builder.Services.AddSingleton(pipeline.RegisterDocHandler);
        builder.Services.AddSingleton(pipeline.RequestAnalysisHandler);
        builder.Services.AddSingleton(pipeline.DemoOptions);
        builder.Services.AddSingleton(pipeline.TimeProvider);

        var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    [LiveIntegrationFact]
    [Trait("Category", "LiveIntegration")]
    public async Task LiveIntegration_HttpEndpoint_ExercisesFullRoutingAndMiddlewarePipeline()
    {
        // 1. Verify health of live FastAPI service on port 8009
        using var checkClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var health = await checkClient.GetAsync($"{LiveServiceBaseUrl}/health");
        Assert.True(health.IsSuccessStatusCode, $"Live FastAPI service at {LiveServiceBaseUrl} must be reachable and healthy.");

        // 2. Verify source Proposal1.pdf exists
        Assert.True(File.Exists(Proposal1Path), $"Source document {Proposal1Path} must exist.");
        var pdfBytes = await File.ReadAllBytesAsync(Proposal1Path);
        Assert.True(pdfBytes.Length > 0, "Proposal1.pdf must not be empty.");

        // 3. Start genuine ASP.NET Core Kestrel WebApplication on dynamic loopback port
        var pipeline = new TestServicePipeline();
        var app = await CreateTestHostAsync(pipeline);
        try
        {
            var server = app.Services.GetRequiredService<IServer>();
            var addressesFeature = server.Features.Get<IServerAddressesFeature>();
            var serverAddress = addressesFeature?.Addresses.FirstOrDefault();
            Assert.False(string.IsNullOrWhiteSpace(serverAddress), "Test host must have bound to a loopback address.");

            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri(serverAddress),
                Timeout = TimeSpan.FromMinutes(5)
            };

            // 4. Construct real HTTP multipart/form-data request
            using var formData = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(pdfBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            formData.Add(fileContent, "file", "Proposal1.pdf");
            formData.Add(new StringContent("sin"), "languageMode");
            formData.Add(new StringContent("analyze"), "executionMode");

            // 5. Send POST to /api/workflow-governance/ocr/demo over HTTP network socket
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var httpResponse = await httpClient.PostAsync("/api/workflow-governance/ocr/demo", formData, cts.Token);

            Assert.Equal(System.Net.HttpStatusCode.OK, httpResponse.StatusCode);
            Assert.Equal("application/json", httpResponse.Content.Headers.ContentType?.MediaType);

            var jsonString = await httpResponse.Content.ReadAsStringAsync(cts.Token);
            var response = JsonSerializer.Deserialize<OcrDemoResponse>(jsonString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(response);
            Assert.NotNull(response.FieldInventory);
            Assert.Equal(17, response.FieldInventory.Count);

            var outDir = Path.Combine(AppContext.BaseDirectory, "demo_live_output");
            Directory.CreateDirectory(outDir);
            var jsonPath = Path.Combine(outDir, "proposal1_dotnet_http_endpoint_response.json");
            await File.WriteAllTextAsync(jsonPath, jsonString);

            var inventory = response.FieldInventory.ToDictionary(f => f.FieldCode, f => f);
            Assert.True(inventory.ContainsKey("applicant_name"));
            Assert.True(inventory.ContainsKey("land_location"));
            Assert.True(inventory.ContainsKey("district"));

            if (string.Equals(response.SemanticStatus, "Failed / Timed Out", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Equal("Unavailable", inventory["applicant_name"].Status);
                Assert.NotEmpty(response.Warnings);
            }
            else
            {
                Assert.Equal("Extracted", inventory["applicant_name"].Status);
            }
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [LiveIntegrationFact]
    [Trait("Category", "LiveIntegration")]
    public async Task LiveIntegration_SuccessPath_WhenGeminiAvailable_PopulatesGroundedCandidatesAnd17FieldInventory()
    {
        // 1. Verify health of live FastAPI service on port 8009
        using var checkClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var health = await checkClient.GetAsync($"{LiveServiceBaseUrl}/health");
        Assert.True(health.IsSuccessStatusCode, $"Live FastAPI service at {LiveServiceBaseUrl} must be reachable and healthy.");

        // 2. Verify source Proposal1.pdf exists
        Assert.True(File.Exists(Proposal1Path), $"Source document {Proposal1Path} must exist.");
        var pdfBytes = await File.ReadAllBytesAsync(Proposal1Path);
        Assert.True(pdfBytes.Length > 0, "Proposal1.pdf must not be empty.");

        // 3. Assemble application pipeline
        var pipeline = new TestServicePipeline();
        var controller = new OcrDemoController(
            pipeline.ContentStore,
            pipeline.ExecutionService,
            pipeline.AnalysisRepo,
            pipeline.ArtifactStore,
            pipeline.RegisterCaseHandler,
            pipeline.RegisterDocHandler,
            pipeline.RequestAnalysisHandler,
            pipeline.DemoOptions,
            pipeline.TimeProvider,
            NullLogger<OcrDemoController>.Instance);

        using var stream = new MemoryStream(pdfBytes);
        var formFile = new FormFile(stream, 0, pdfBytes.Length, "file", "Proposal1.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        // 4. Act: execute full extraction
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var actionResult = await controller.ProcessDemoDocumentAsync(
            formFile,
            languageMode: "sin",
            executionMode: "analyze",
            cancellationToken: cts.Token);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(200, okResult.StatusCode);

        var response = Assert.IsType<OcrDemoResponse>(okResult.Value);
        Assert.NotNull(response);

        // Strict assertion: success path REQUIRES semantic completion (a failed/timed-out run must NOT pass)
        Assert.Equal("Completed", response.SemanticStatus);
        Assert.NotNull(response.ModelIdentifier);
        Assert.Contains("gemini", response.ModelIdentifier, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(response.FieldInventory);
        Assert.Equal(17, response.FieldInventory.Count);

        var inventory = response.FieldInventory.ToDictionary(f => f.FieldCode, f => f);

        // Expected present fields MUST be Extracted with populated grounded candidates
        Assert.Equal("Extracted", inventory["applicant_name"].Status);
        Assert.False(string.IsNullOrWhiteSpace(inventory["applicant_name"].PreferredValue));
        Assert.True(inventory["applicant_name"].Confidence > 0);

        Assert.Equal("Extracted", inventory["land_location"].Status);
        Assert.Equal("Extracted", inventory["district"].Status);
        Assert.Equal("Extracted", inventory["divisional_secretariat_division"].Status);
        Assert.Equal("Extracted", inventory["land_extent"].Status);
        Assert.Equal("Extracted", inventory["land_extent_unit"].Status);
        Assert.Equal("Extracted", inventory["proposed_land_use"].Status);
        Assert.Equal("Extracted", inventory["project_title"].Status);
        Assert.Equal("Extracted", inventory["project_description"].Status);
        Assert.Equal("Extracted", inventory["expected_employment"].Status);

        // Genuine missing fields MUST be Missing
        Assert.Equal("Missing", inventory["requested_lease_period"].Status);
        Assert.Equal("Missing", inventory["legal_statutory_basis"].Status);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task FailurePath_WhenUpstreamServiceUnreachable_ProducesClear503AndBlocksProgression()
    {
        // 1. Configure pipeline pointing to an unreachable port
        var pipeline = new TestServicePipeline("http://127.0.0.1:59999");
        var controller = new OcrDemoController(
            pipeline.ContentStore,
            pipeline.ExecutionService,
            pipeline.AnalysisRepo,
            pipeline.ArtifactStore,
            pipeline.RegisterCaseHandler,
            pipeline.RegisterDocHandler,
            pipeline.RequestAnalysisHandler,
            pipeline.DemoOptions,
            pipeline.TimeProvider,
            NullLogger<OcrDemoController>.Instance);

        var dummyBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x35 }; // %PDF-1.5
        using var stream = new MemoryStream(dummyBytes);
        var formFile = new FormFile(stream, 0, dummyBytes.Length, "file", "test.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var actionResult = await controller.ProcessDemoDocumentAsync(
            formFile,
            languageMode: "sin",
            executionMode: "analyze",
            cancellationToken: CancellationToken.None);

        // 2. Asserts HTTP 503 Service Unavailable ProblemDetails (does NOT throw unhandled, does not progress case)
        var objResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objResult.Value);
        Assert.Contains("unavailable", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task FailurePath_WhenSemanticExtractionFails_MarksAllFieldsUnavailableNotMissing()
    {
        // 1. Configure pipeline with stub intelligence service simulating upstream semantic failure / timeout
        var artifactStore = new InMemoryArtifactStore();
        var stubService = new StubFailingIntelligenceService(artifactStore, new[] { "Upstream Gemini extraction timed out after 60s" });
        var pipeline = new TestServicePipeline(intelligenceServiceOverride: stubService, artifactStore: artifactStore);

        var controller = new OcrDemoController(
            pipeline.ContentStore,
            pipeline.ExecutionService,
            pipeline.AnalysisRepo,
            pipeline.ArtifactStore,
            pipeline.RegisterCaseHandler,
            pipeline.RegisterDocHandler,
            pipeline.RequestAnalysisHandler,
            pipeline.DemoOptions,
            pipeline.TimeProvider,
            NullLogger<OcrDemoController>.Instance);

        var dummyBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x35 }; // %PDF-1.5
        using var stream = new MemoryStream(dummyBytes);
        var formFile = new FormFile(stream, 0, dummyBytes.Length, "file", "test.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var actionResult = await controller.ProcessDemoDocumentAsync(
            formFile,
            languageMode: "sin",
            executionMode: "analyze",
            cancellationToken: CancellationToken.None);

        // 2. Asserts HTTP 200 with fail-closed governance invariants
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var response = Assert.IsType<OcrDemoResponse>(okResult.Value);
        Assert.NotNull(response);
        Assert.Equal("Failed / Timed Out", response.SemanticStatus);
        Assert.NotEmpty(response.Warnings);
        Assert.NotNull(response.FieldInventory);
        Assert.Equal(17, response.FieldInventory.Count);

        // Distinguish unanalyzed/unavailable from genuinely missing:
        // When provider fails, ALL 17 inventory fields must be 'Unavailable' - NOT 'Missing'
        foreach (var field in response.FieldInventory)
        {
            Assert.Equal("Unavailable", field.Status);
        }

        // Case status must remain initial Draft state (never promoted)
        var caseEntity = await pipeline.LeaseCaseRepo.GetByIdAsync(new LeaseCaseId(response.CaseId));
        Assert.NotNull(caseEntity);
        Assert.Equal(LeaseCaseStatus.Draft, caseEntity.Status);
    }
}
