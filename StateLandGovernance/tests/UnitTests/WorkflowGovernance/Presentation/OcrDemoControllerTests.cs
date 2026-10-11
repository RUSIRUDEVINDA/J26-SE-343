namespace StateLandGovernance.UnitTests.WorkflowGovernance.Presentation;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;
using StateLandGovernance.WorkflowGovernance.Presentation;
using StateLandGovernance.WorkflowGovernance.Presentation.Configuration;
using StateLandGovernance.WorkflowGovernance.Presentation.Controllers;
using StateLandGovernance.WorkflowGovernance.Presentation.DTOs;
using Xunit;

public sealed class OcrDemoControllerTests
{
    private readonly InMemoryLeaseCaseRepository _leaseCaseRepo = new();
    private readonly InMemoryGovernedDocumentRepository _documentRepo = new();
    private readonly InMemoryDocumentAnalysisRepository _analysisRepo = new();
    private readonly InMemoryWorkflowGovernanceUnitOfWork _unitOfWork = new();
    private readonly FakeDocumentContentWriter _contentWriter = new();
    private readonly FakeExecutionService _executionService = new();
    private readonly FakeAnalysisArtifactReader _artifactReader = new();
    private readonly IOptions<OcrDemoOptions> _options = Options.Create(new OcrDemoOptions
    {
        Enabled = true,
        MaxUploadBytes = 1024 * 1024 // 1 MB for testing
    });

    private OcrDemoController CreateController()
    {
        var timeProvider = TimeProvider.System;
        var registerCaseHandler = new RegisterLeaseCaseCommandHandler(
            _leaseCaseRepo, _unitOfWork, new StateLandGovernance.WorkflowGovernance.Application.Validators.RegisterLeaseCaseCommandValidator(), timeProvider);
        var registerDocHandler = new RegisterGovernedDocumentCommandHandler(
            _documentRepo, _leaseCaseRepo, _unitOfWork, new StateLandGovernance.WorkflowGovernance.Application.Validators.RegisterGovernedDocumentCommandValidator(), timeProvider);
        var requestAnalysisHandler = new RequestDocumentAnalysisCommandHandler(
            _analysisRepo, _documentRepo, _unitOfWork, new StateLandGovernance.WorkflowGovernance.Application.Validators.RequestDocumentAnalysisCommandValidator(), timeProvider);

        return new OcrDemoController(
            _contentWriter,
            _executionService,
            _analysisRepo,
            _artifactReader,
            registerCaseHandler,
            registerDocHandler,
            requestAnalysisHandler,
            _options,
            timeProvider,
            NullLogger<OcrDemoController>.Instance);
    }

    private static IFormFile CreateFormFile(string fileName, string contentType, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    [Fact]
    public async Task Demo_EmptyFile_Returns400BadRequest()
    {
        var controller = CreateController();
        var emptyFile = CreateFormFile("sample.png", "image/png", Array.Empty<byte>());

        var result = await controller.ProcessDemoDocumentAsync(emptyFile, "eng", CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(badRequestResult.Value);
        Assert.Equal("Missing Upload File", problem.Title);
    }

    [Fact]
    public async Task Demo_OversizedFile_Returns413PayloadTooLarge()
    {
        var controller = CreateController();
        var bigBytes = new byte[1024 * 1024 + 10]; // Exceeds 1 MB limit
        var bigFile = CreateFormFile("sample.png", "image/png", bigBytes);

        var result = await controller.ProcessDemoDocumentAsync(bigFile, "eng", CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(413, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Payload Too Large", problem.Title);
    }

    [Theory]
    [InlineData("sample.txt", "text/plain")]
    [InlineData("sample.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("sample.exe", "application/octet-stream")]
    public async Task Demo_UnsupportedMediaType_Returns415UnsupportedMediaType(string fileName, string contentType)
    {
        var controller = CreateController();
        var file = CreateFormFile(fileName, contentType, new byte[] { 1, 2, 3 });

        var result = await controller.ProcessDemoDocumentAsync(file, "eng", CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(415, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Unsupported Media Type", problem.Title);
    }

    [Theory]
    [InlineData("fra")]
    [InlineData("deu")]
    [InlineData("tamil")]
    [InlineData("invalid_mode")]
    public async Task Demo_InvalidLanguageMode_Returns422UnprocessableEntity(string languageMode)
    {
        var controller = CreateController();
        var file = CreateFormFile("sample.png", "image/png", new byte[] { 1, 2, 3 });

        var result = await controller.ProcessDemoDocumentAsync(file, languageMode, CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(422, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Invalid Language Mode", problem.Title);
    }

    [Fact]
    public async Task Demo_ValidEnglishRequest_Returns200WithTranscriptsAndNotice()
    {
        // Arrange
        var controller = CreateController();
        var fileBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG header
        var file = CreateFormFile("lease_deed.png", "image/png", fileBytes);

        _executionService.OnExecute = async (analysisId, runId, revision, lang) =>
        {
            var analysis = await _analysisRepo.GetByIdAsync(new DocumentAnalysisId(analysisId));
            var run = analysis!.Runs.First(r => r.Id.Value == runId);

            var docArtifactRef = new AnalysisResultArtifactReference(
                new AnalysisResultArtifactId(Guid.NewGuid()),
                "OcrDocumentTranscript",
                "artifacts/test/doc-transcript.txt",
                "text/plain",
                new AnalysisArtifactChecksum("SHA-256", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"));

            var pageArtifactRef = new AnalysisResultArtifactReference(
                new AnalysisResultArtifactId(Guid.NewGuid()),
                "OcrPageTranscript",
                "artifacts/test/page-1.json",
                "application/json",
                new AnalysisArtifactChecksum("SHA-256", "a3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"));

            analysis.StartRun(new AnalysisRunId(runId), DateTime.UtcNow);
            analysis.CompleteRun(
                new AnalysisRunId(runId),
                new AnalysisRunResultId(Guid.NewGuid()),
                AnalysisResultOutcome.OutputsProduced,
                new[] { docArtifactRef, pageArtifactRef },
                Array.Empty<ExtractedFactInput>(),
                DateTime.UtcNow);

            _artifactReader.Storage[docArtifactRef.StorageReference] = Encoding.UTF8.GetBytes("STATE LEASE AGREEMENT");

            var pageJson = JsonSerializer.Serialize(new
            {
                page_number = 1,
                raw_text = "STATE LEASE AGREEMENT",
                clean_text = "STATE LEASE AGREEMENT",
                confidence = 94.5
            });
            _artifactReader.Storage[pageArtifactRef.StorageReference] = Encoding.UTF8.GetBytes(pageJson);

            return new DocumentAnalysisExecutionResult(analysisId, runId, "Completed", 2);
        };

        // Act
        var result = await controller.ProcessDemoDocumentAsync(file, "eng", CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var response = Assert.IsType<OcrDemoResponse>(okResult.Value);
        Assert.Equal("Completed", response.Status);
        Assert.Equal("STATE LEASE AGREEMENT", response.FullText);
        Assert.Equal("OCR text is machine-generated and has not been human verified.", response.GovernanceNotice);
        Assert.Single(response.Pages);
        Assert.Equal(1, response.Pages[0].PageNumber);
        Assert.Equal("STATE LEASE AGREEMENT", response.Pages[0].RawText);
    }

    [Theory]
    [InlineData("sin")]
    [InlineData("sin+eng")]
    public async Task Demo_ValidSinhalaRequest_Returns200WithTranscripts(string languageMode)
    {
        // Arrange
        var controller = CreateController();
        var fileBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }; // PDF header
        var file = CreateFormFile("sin_deed.pdf", "application/pdf", fileBytes);

        _executionService.OnExecute = async (analysisId, runId, revision, lang) =>
        {
            var analysis = await _analysisRepo.GetByIdAsync(new DocumentAnalysisId(analysisId));
            var run = analysis!.Runs.First(r => r.Id.Value == runId);

            var docArtifactRef = new AnalysisResultArtifactReference(
                new AnalysisResultArtifactId(Guid.NewGuid()),
                "OcrDocumentTranscript",
                "artifacts/test/sin-doc-transcript.txt",
                "text/plain",
                new AnalysisArtifactChecksum("SHA-256", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"));

            analysis.StartRun(new AnalysisRunId(runId), DateTime.UtcNow);
            analysis.CompleteRun(
                new AnalysisRunId(runId),
                new AnalysisRunResultId(Guid.NewGuid()),
                AnalysisResultOutcome.OutputsProduced,
                new[] { docArtifactRef },
                Array.Empty<ExtractedFactInput>(),
                DateTime.UtcNow);

            _artifactReader.Storage[docArtifactRef.StorageReference] = Encoding.UTF8.GetBytes("ශ්‍රී ලංකා රජයේ බදු ගිවිසුම");

            return new DocumentAnalysisExecutionResult(analysisId, runId, "Completed", 1);
        };

        // Act
        var result = await controller.ProcessDemoDocumentAsync(file, languageMode, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var response = Assert.IsType<OcrDemoResponse>(okResult.Value);
        Assert.Equal("Completed", response.Status);
        Assert.Equal("ශ්‍රී ලංකා රජයේ බදු ගිවිසුම", response.FullText);
        Assert.Equal("OCR text is machine-generated and has not been human verified.", response.GovernanceNotice);
    }

    [Fact]
    public async Task Demo_ExecutionFailure_IntegrityChecksumMismatch_Returns422()
    {
        // Arrange
        var controller = CreateController();
        var file = CreateFormFile("corrupt.pdf", "application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 });

        _executionService.ResultToReturn = new DocumentAnalysisExecutionResult(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Failed",
            0,
            FailureCode: "CHECKSUM_MISMATCH",
            FailureDescription: "The document content integrity check failed.");

        // Act
        var result = await controller.ProcessDemoDocumentAsync(file, "sin", CancellationToken.None);

        // Assert
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(422, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Integrity Verification Failed", problem.Title);
    }

    [Fact]
    public async Task Demo_ExecutionFailure_ServiceUnavailable_Returns503()
    {
        // Arrange
        var controller = CreateController();
        var file = CreateFormFile("sample.pdf", "application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 });

        _executionService.ResultToReturn = new DocumentAnalysisExecutionResult(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Failed",
            0,
            FailureCode: "SERVICE_UNAVAILABLE",
            FailureDescription: "OCR service unreachable.");

        // Act
        var result = await controller.ProcessDemoDocumentAsync(file, "eng", CancellationToken.None);

        // Assert
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(503, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("OCR Service Unavailable", problem.Title);
    }

    [Fact]
    public async Task Demo_ExecutionFailure_GenericError_Returns500()
    {
        // Arrange
        var controller = CreateController();
        var file = CreateFormFile("corrupt.pdf", "application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 });

        _executionService.ResultToReturn = new DocumentAnalysisExecutionResult(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Failed",
            0,
            FailureCode: "TesseractInternalError",
            FailureDescription: "Internal engine crash.");

        // Act
        var result = await controller.ProcessDemoDocumentAsync(file, "sin", CancellationToken.None);

        // Assert
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Analysis Execution Failed", problem.Title);
    }

    [Fact]
    public async Task Demo_WhenDemoDisabled_ReturnsNotFound404()
    {
        // Arrange
        var disabledOptions = Options.Create(new OcrDemoOptions
        {
            Enabled = false,
            MaxUploadBytes = 1024 * 1024
        });

        var timeProvider = TimeProvider.System;
        var registerCaseHandler = new RegisterLeaseCaseCommandHandler(
            _leaseCaseRepo, _unitOfWork, new StateLandGovernance.WorkflowGovernance.Application.Validators.RegisterLeaseCaseCommandValidator(), timeProvider);
        var registerDocHandler = new RegisterGovernedDocumentCommandHandler(
            _documentRepo, _leaseCaseRepo, _unitOfWork, new StateLandGovernance.WorkflowGovernance.Application.Validators.RegisterGovernedDocumentCommandValidator(), timeProvider);
        var requestAnalysisHandler = new RequestDocumentAnalysisCommandHandler(
            _analysisRepo, _documentRepo, _unitOfWork, new StateLandGovernance.WorkflowGovernance.Application.Validators.RequestDocumentAnalysisCommandValidator(), timeProvider);

        var controller = new OcrDemoController(
            _contentWriter,
            _executionService,
            _analysisRepo,
            _artifactReader,
            registerCaseHandler,
            registerDocHandler,
            requestAnalysisHandler,
            disabledOptions,
            timeProvider,
            NullLogger<OcrDemoController>.Instance);

        var file = CreateFormFile("sample.png", "image/png", new byte[] { 1, 2, 3 });

        // Act
        var result = await controller.ProcessDemoDocumentAsync(file, "eng", CancellationToken.None);

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        Assert.Equal("OCR Demo Disabled", problem.Title);
    }

    [Fact]
    public async Task Demo_WhenDemoDisabled_WithoutOcrOrStorageDependencies_ActivatesAndReturnsNotFound404()
    {
        // Arrange: Container has ONLY presentation and NO storage or OCR dependencies registered
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddWorkflowGovernancePresentation(configureOptions: opt =>
        {
            opt.Enabled = false;
        });

        using var provider = services.BuildServiceProvider();

        // ActivatorUtilities uses [ActivatorUtilitiesConstructor]
        var controller = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<OcrDemoController>(provider);

        var file = CreateFormFile("sample.png", "image/png", new byte[] { 1, 2, 3 });

        // Act
        var result = await controller.ProcessDemoDocumentAsync(file, "eng", CancellationToken.None);

        // Assert: 404 returned safely without attempting to resolve or requiring missing storage/OCR services
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        Assert.Equal("OCR Demo Disabled", problem.Title);
    }

    #region Fakes

    private sealed class FakeDocumentContentWriter : IDocumentContentWriter
    {
        public Task<DocumentContentReceipt> WriteAsync(
            Stream content,
            string originalFileName,
            string mediaType,
            Guid? documentId = null,
            Guid? versionId = null,
            CancellationToken cancellationToken = default)
        {
            var docId = documentId ?? Guid.NewGuid();
            var verId = versionId ?? Guid.NewGuid();
            return Task.FromResult(DocumentContentReceipt.CreateSha256(
                contentReference: $"documents/{docId:D}/{verId:D}/source.bin",
                sha256Hex: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                originalFileName: originalFileName,
                mediaType: mediaType,
                fileSizeInBytes: 100));
        }
    }

    private sealed class FakeExecutionService : IDocumentAnalysisExecutionService
    {
        public DocumentAnalysisExecutionResult? ResultToReturn { get; set; }
        public Func<Guid, Guid, int, string?, Task<DocumentAnalysisExecutionResult>>? OnExecute { get; set; }

        public Task<DocumentAnalysisExecutionResult> ExecuteAsync(
            Guid documentAnalysisId,
            Guid analysisRunId,
            int expectedAnalysisRevision,
            string? languageHint = null,
            CancellationToken cancellationToken = default)
        {
            if (OnExecute != null)
            {
                return OnExecute(documentAnalysisId, analysisRunId, expectedAnalysisRevision, languageHint);
            }

            return Task.FromResult(ResultToReturn ?? new DocumentAnalysisExecutionResult(
                documentAnalysisId,
                analysisRunId,
                "Completed",
                ArtifactCount: 2));
        }
    }

    private sealed class FakeAnalysisArtifactReader : IAnalysisArtifactReader
    {
        public Dictionary<string, byte[]> Storage { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<ReadOnlyMemory<byte>?> ReadAsync(string storageReference, CancellationToken cancellationToken = default)
        {
            if (Storage.TryGetValue(storageReference, out var bytes))
            {
                return Task.FromResult<ReadOnlyMemory<byte>?>(new ReadOnlyMemory<byte>(bytes));
            }
            return Task.FromResult<ReadOnlyMemory<byte>?>(null);
        }
    }

    #endregion
}
