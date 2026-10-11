namespace StateLandGovernance.WorkflowGovernance.Presentation.Controllers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Presentation.Configuration;
using StateLandGovernance.WorkflowGovernance.Presentation.DTOs;

/// <summary>
/// Minimal Presentation endpoint for the temporary OCR vertical-slice demonstration.
/// Exposes browser upload for English and Sinhala documents through the complete governed intake pipeline.
/// </summary>
[ApiController]
[Route("api/workflow-governance/ocr")]
[ApiExplorerSettings(GroupName = "workflow-governance")]
public sealed class OcrDemoController : ControllerBase
{
    private static readonly HashSet<string> AllowedModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "eng",
        "sin",
        "sin+eng"
    };

    private static readonly Dictionary<string, string> AllowedMediaTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".pdf", "application/pdf" },
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" }
    };

    private readonly IServiceProvider? _serviceProvider;
    private readonly IDocumentContentWriter? _contentWriter;
    private readonly IDocumentAnalysisExecutionService? _executionService;
    private readonly IDocumentAnalysisRepository? _analysisRepository;
    private readonly IAnalysisArtifactReader? _artifactReader;
    private readonly RegisterLeaseCaseCommandHandler? _registerCaseHandler;
    private readonly RegisterGovernedDocumentCommandHandler? _registerDocumentHandler;
    private readonly RequestDocumentAnalysisCommandHandler? _requestAnalysisHandler;
    private readonly OcrDemoOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OcrDemoController> _logger;

    /// <summary>
    /// Runtime activation constructor. Resolves dependencies on-demand to ensure the controller
    /// can be safely instantiated and return a 404 response when the OCR demo is disabled,
    /// without failing host DI verification.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public OcrDemoController(
        IOptions<OcrDemoOptions> options,
        TimeProvider timeProvider,
        ILogger<OcrDemoController> logger,
        IServiceProvider serviceProvider)
    {
        _options = options?.Value ?? new OcrDemoOptions();
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Explicit constructor for unit testing and direct test instantiation.
    /// </summary>
    public OcrDemoController(
        IDocumentContentWriter contentWriter,
        IDocumentAnalysisExecutionService executionService,
        IDocumentAnalysisRepository analysisRepository,
        IAnalysisArtifactReader artifactReader,
        RegisterLeaseCaseCommandHandler registerCaseHandler,
        RegisterGovernedDocumentCommandHandler registerDocumentHandler,
        RequestDocumentAnalysisCommandHandler requestAnalysisHandler,
        IOptions<OcrDemoOptions> options,
        TimeProvider timeProvider,
        ILogger<OcrDemoController> logger)
    {
        _contentWriter = contentWriter ?? throw new ArgumentNullException(nameof(contentWriter));
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _artifactReader = artifactReader ?? throw new ArgumentNullException(nameof(artifactReader));
        _registerCaseHandler = registerCaseHandler ?? throw new ArgumentNullException(nameof(registerCaseHandler));
        _registerDocumentHandler = registerDocumentHandler ?? throw new ArgumentNullException(nameof(registerDocumentHandler));
        _requestAnalysisHandler = requestAnalysisHandler ?? throw new ArgumentNullException(nameof(requestAnalysisHandler));
        _options = options?.Value ?? new OcrDemoOptions();
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Uploads an English, Sinhala, or bilingual document, registers it through the governed workflow pipeline,
    /// executes machine OCR analysis, and returns persisted recognition text for temporary UI display.
    /// </summary>
    [HttpPost("demo")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(OcrDemoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ProcessDemoDocumentAsync(
        IFormFile? file,
        [FromForm] string? languageMode = null,
        CancellationToken cancellationToken = default)
    {
        // 0. Verify demo endpoint is enabled
        if (!_options.Enabled)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "OCR Demo Disabled",
                Detail = "The temporary OCR demo endpoint is disabled in this environment."
            });
        }

        // 1. Validate file presence
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Missing Upload File",
                Detail = "A valid document file must be uploaded and cannot be empty."
            });
        }

        // 2. Validate file size against configured limit
        if (file.Length > _options.MaxUploadBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new ProblemDetails
            {
                Status = StatusCodes.Status413PayloadTooLarge,
                Title = "Payload Too Large",
                Detail = $"File size ({file.Length} bytes) exceeds the maximum allowed limit of {_options.MaxUploadBytes} bytes."
            });
        }

        // 3. Validate media type and extension allow-list
        var extension = Path.GetExtension(file.FileName) ?? string.Empty;
        if (!AllowedMediaTypesByExtension.TryGetValue(extension, out var mediaType))
        {
            return StatusCode(StatusCodes.Status415UnsupportedMediaType, new ProblemDetails
            {
                Status = StatusCodes.Status415UnsupportedMediaType,
                Title = "Unsupported Media Type",
                Detail = $"File extension '{extension}' is not supported. Supported extensions: .pdf, .png, .jpg, .jpeg."
            });
        }

        // 4. Validate language mode
        var mode = string.IsNullOrWhiteSpace(languageMode) ? "sin+eng" : languageMode.Trim().ToLowerInvariant();
        if (!AllowedModes.Contains(mode))
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Invalid Language Mode",
                Detail = $"Language mode '{languageMode}' is not supported. Supported modes: 'eng', 'sin', 'sin+eng'."
            });
        }

        var contentWriter = _contentWriter ?? _serviceProvider?.GetService<IDocumentContentWriter>();
        var executionService = _executionService ?? _serviceProvider?.GetService<IDocumentAnalysisExecutionService>();
        var analysisRepo = _analysisRepository ?? _serviceProvider?.GetService<IDocumentAnalysisRepository>();
        var artifactReader = _artifactReader ?? _serviceProvider?.GetService<IAnalysisArtifactReader>();
        var registerCaseHandler = _registerCaseHandler ?? _serviceProvider?.GetService<RegisterLeaseCaseCommandHandler>();
        var registerDocHandler = _registerDocumentHandler ?? _serviceProvider?.GetService<RegisterGovernedDocumentCommandHandler>();
        var requestAnalysisHandler = _requestAnalysisHandler ?? _serviceProvider?.GetService<RequestDocumentAnalysisCommandHandler>();

        if (contentWriter is null || executionService is null || analysisRepo is null || artifactReader is null
            || registerCaseHandler is null || registerDocHandler is null || requestAnalysisHandler is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "OCR Demo Dependencies Unavailable",
                Detail = "Required dependencies for OCR demo processing are not registered in this host."
            });
        }

        try
        {
            // 5. Write raw bytes to storage and obtain authoritative SHA-256 receipt
            var docId = Guid.NewGuid();
            var verId = Guid.NewGuid();

            DocumentContentReceipt contentReceipt;
            await using (var stream = file.OpenReadStream())
            {
                contentReceipt = await contentWriter.WriteAsync(
                    stream,
                    file.FileName,
                    mediaType,
                    docId,
                    verId,
                    cancellationToken);
            }

            // 6. Establish minimal demo lease case context
            var actorId = Guid.NewGuid();
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var authorityContext = new VerifiedAuthorityContext(
                ActorId: actorId,
                Capabilities: new[] { "LeaseInitiator", "DocumentSubmitter" },
                ScopeKind: "Global",
                ScopeTargetIdentifier: null,
                ValidFrom: now.AddHours(-1),
                VerificationTime: now,
                ValidUntil: now.AddHours(24));

            var applicationRef = $"OCR-DEMO-{Guid.NewGuid():N}";
            var caseCommand = new RegisterLeaseCaseCommand(
                applicationRef,
                actorId,
                authorityContext);

            var caseDto = await registerCaseHandler.HandleAsync(caseCommand, cancellationToken);

            // 7. Register GovernedDocument and initial DocumentVersion
            var docCommand = new RegisterGovernedDocumentCommand(
                caseDto.Id,
                "Deed",
                contentReceipt,
                actorId,
                authorityContext,
                GovernedDocumentId: docId,
                InitialVersionId: verId);

            var docDto = await registerDocHandler.HandleAsync(docCommand, cancellationToken);

            // 8. Request DocumentAnalysis run
            var analysisCommand = new RequestDocumentAnalysisCommand(
                docId,
                verId,
                ModelProvider: "FastApi",
                ModelName: "Tesseract-5",
                ModelVersion: "5.4.0",
                RequestedCapabilities: new[] { "Ocr" });

            var analysisDto = await requestAnalysisHandler.HandleAsync(analysisCommand, cancellationToken);
            var runDto = analysisDto.ActiveRun ?? analysisDto.Runs.Last();

            // 9. Execute DocumentAnalysis orchestration
            var executionResult = await executionService.ExecuteAsync(
                analysisDto.Id,
                runDto.Id,
                expectedAnalysisRevision: analysisDto.Revision,
                languageHint: mode,
                cancellationToken: cancellationToken);

            // 10. Check execution outcome
            if (!string.Equals(executionResult.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("OCR demo analysis execution failed with code {Code}: {Description}",
                    executionResult.FailureCode, executionResult.FailureDescription);

                return MapExecutionFailure(executionResult);
            }

            // 11. Read persisted OCR transcript artifacts
            var reloadedAnalysis = await analysisRepo.GetByIdAsync(
                new DocumentAnalysisId(analysisDto.Id),
                cancellationToken);

            var executedRun = reloadedAnalysis?.Runs.FirstOrDefault(r => r.Id.Value == runDto.Id);
            var artifacts = executedRun?.Result?.Artifacts ?? (IReadOnlyCollection<AnalysisResultArtifactReference>)Array.Empty<AnalysisResultArtifactReference>();

            var fullText = string.Empty;
            var docArtifact = artifacts.FirstOrDefault(a => a.ArtifactKind == "OcrDocumentTranscript");
            if (docArtifact != null)
            {
                var docBytes = await artifactReader.ReadAsync(docArtifact.StorageReference, cancellationToken);
                if (docBytes.HasValue)
                {
                    fullText = Encoding.UTF8.GetString(docBytes.Value.Span);
                }
            }

            var pages = new List<OcrDemoPageResponse>();
            var pageArtifacts = artifacts
                .Where(a => a.ArtifactKind == "OcrPageTranscript")
                .ToList();

            foreach (var pageArtifact in pageArtifacts)
            {
                var pageBytes = await artifactReader.ReadAsync(pageArtifact.StorageReference, cancellationToken);
                if (pageBytes.HasValue)
                {
                    try
                    {
                        var pageProvenance = JsonSerializer.Deserialize<StoredPageProvenanceDto>(pageBytes.Value.Span);
                        if (pageProvenance != null)
                        {
                            pages.Add(new OcrDemoPageResponse(
                                PageNumber: pageProvenance.PageNumber,
                                RawText: pageProvenance.RawText ?? string.Empty,
                                CleanText: pageProvenance.CleanText ?? string.Empty,
                                Confidence: null));
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse page provenance JSON from {Reference}", pageArtifact.StorageReference);
                    }
                }
            }

            pages.Sort((a, b) => a.PageNumber.CompareTo(b.PageNumber));

            var pageCount = pages.Count > 0 ? pages.Count : 1;
            var warnings = executedRun?.Failure?.Description != null
                ? new[] { executedRun.Failure.Description }
                : Array.Empty<string>();

            var response = new OcrDemoResponse(
                CaseId: caseDto.Id,
                GovernedDocumentId: docDto.Id,
                DocumentVersionId: verId,
                DocumentAnalysisId: analysisDto.Id,
                AnalysisRunId: runDto.Id,
                Status: "Completed",
                LanguageMode: mode,
                FileName: file.FileName,
                MediaType: mediaType,
                PageCount: pageCount,
                FullText: fullText,
                Pages: pages,
                Warnings: warnings,
                GovernanceNotice: "OCR text is machine-generated and has not been human verified.");

            return Ok(response);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("OCR demo processing was cancelled by client.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in OCR demo endpoint for file {FileName}", file.FileName);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Document Analysis Failed",
                Detail = "An unexpected error occurred while processing the document. Details have been logged safely."
            });
        }
    }

    private IActionResult MapExecutionFailure(DocumentAnalysisExecutionResult result)
    {
        var code = result.FailureCode ?? "UNKNOWN_ERROR";
        var description = result.FailureDescription ?? "Document analysis execution failed.";

        return code switch
        {
            "SERVICE_UNAVAILABLE" => StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "OCR Service Unavailable",
                Detail = "The document intelligence service is currently unavailable. Please ensure the OCR service is running."
            }),

            "TIMEOUT" => StatusCode(StatusCodes.Status504GatewayTimeout, new ProblemDetails
            {
                Status = StatusCodes.Status504GatewayTimeout,
                Title = "OCR Service Timeout",
                Detail = "The document intelligence request timed out."
            }),

            "CHECKSUM_MISMATCH" => UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Integrity Verification Failed",
                Detail = "The document content integrity check failed. The uploaded bytes did not match the expected digest."
            }),

            "PAYLOAD_TOO_LARGE" => StatusCode(StatusCodes.Status413PayloadTooLarge, new ProblemDetails
            {
                Status = StatusCodes.Status413PayloadTooLarge,
                Title = "Payload Too Large",
                Detail = description
            }),

            "UNSUPPORTED_MEDIA" => StatusCode(StatusCodes.Status415UnsupportedMediaType, new ProblemDetails
            {
                Status = StatusCodes.Status415UnsupportedMediaType,
                Title = "Unsupported Media Type",
                Detail = description
            }),

            "UNSUPPORTED_LANGUAGE" => UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Unsupported Language",
                Detail = description
            }),

            _ => StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Analysis Execution Failed",
                Detail = description
            })
        };
    }

    private sealed record StoredPageProvenanceDto(
        [property: JsonPropertyName("page_number")] int PageNumber,
        [property: JsonPropertyName("language_mode")] string? LanguageMode,
        [property: JsonPropertyName("raw_text")] string? RawText,
        [property: JsonPropertyName("clean_text")] string? CleanText
    );
}
