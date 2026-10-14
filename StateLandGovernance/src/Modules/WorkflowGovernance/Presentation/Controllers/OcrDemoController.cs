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
    /// Overload for callers specifying only languageMode and cancellationToken.
    /// </summary>
    [NonAction]
    public Task<IActionResult> ProcessDemoDocumentAsync(
        IFormFile? file,
        string? languageMode,
        CancellationToken cancellationToken)
        => ProcessDemoDocumentAsync(file, languageMode, executionMode: null, semanticProvider: null, cancellationToken);

    /// <summary>
    /// Overload for callers specifying languageMode, executionMode, and cancellationToken.
    /// </summary>
    [NonAction]
    public Task<IActionResult> ProcessDemoDocumentAsync(
        IFormFile? file,
        string? languageMode,
        string? executionMode,
        CancellationToken cancellationToken)
        => ProcessDemoDocumentAsync(file, languageMode, executionMode, semanticProvider: null, cancellationToken);

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
        [FromForm] string? executionMode = null,
        [FromForm] string? semanticProvider = null,
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
            var isExplicitQwen = string.Equals(semanticProvider, "qwen", StringComparison.OrdinalIgnoreCase)
                || string.Equals(semanticProvider, "ollama", StringComparison.OrdinalIgnoreCase)
                || string.Equals(executionMode, "ocr_qwen", StringComparison.OrdinalIgnoreCase)
                || string.Equals(executionMode, "qwen", StringComparison.OrdinalIgnoreCase);

            var isSemanticMode = string.Equals(executionMode, "analyze", StringComparison.OrdinalIgnoreCase)
                || string.Equals(executionMode, "semantic", StringComparison.OrdinalIgnoreCase)
                || isExplicitQwen;

            var requestedCapabilities = isSemanticMode
                ? new[] { "Ocr", "SemanticExtraction" }
                : new[] { "Ocr" };

            // Default to Gemini for governed semantic mode unless Qwen was explicitly requested
            var effectiveSemanticProvider = isSemanticMode
                ? (isExplicitQwen ? "qwen" : "gemini")
                : null;

            var modelName = isSemanticMode
                ? (isExplicitQwen ? "Tesseract-Qwen" : "Tesseract-Gemini")
                : "Tesseract-5";
            var modelVersion = isSemanticMode
                ? (isExplicitQwen ? "1.0.0" : "1.5.0")
                : "5.4.0";

            var analysisCommand = new RequestDocumentAnalysisCommand(
                docId,
                verId,
                ModelProvider: "FastApi",
                ModelName: modelName,
                ModelVersion: modelVersion,
                RequestedCapabilities: requestedCapabilities);

            var analysisDto = await requestAnalysisHandler.HandleAsync(analysisCommand, cancellationToken);
            var runDto = analysisDto.ActiveRun ?? analysisDto.Runs.Last();

            // 9. Execute DocumentAnalysis orchestration
            var executionResult = await executionService.ExecuteAsync(
                analysisDto.Id,
                runDto.Id,
                expectedAnalysisRevision: analysisDto.Revision,
                languageHint: mode,
                semanticProvider: effectiveSemanticProvider,
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
            var warningsList = new List<string>();
            if (executedRun?.Failure?.Description != null)
            {
                warningsList.Add(executedRun.Failure.Description);
            }

            DocumentClassificationResponse? classification = null;
            var candidateFacts = new List<CandidateFactResponse>();
            var allCandidates = new List<CandidateFactResponse>();
            var fieldInventory = new List<GovernedFieldInventoryItemResponse>();
            ExecutionDurationResponse? durationBreakdown = null;
            int candidateCount = 0;
            int acceptedCount = 0;
            int rejectedCount = 0;
            int conflictCount = 0;
            int ungroundedCount = 0;
            string? modelIdentifier = null;
            string? promptVersion = null;
            string? returnedSemanticProvider = null;

            CandidateFactResponse ParseFactElement(JsonElement factEl)
            {
                var fieldCode = factEl.TryGetProperty("field_code", out var fcEl)
                    ? fcEl.GetString() ?? string.Empty
                    : (factEl.TryGetProperty("fact_code", out var fcEl2) ? fcEl2.GetString() ?? string.Empty : string.Empty);

                var valKind = factEl.TryGetProperty("value_kind", out var vkEl) ? vkEl.GetString() ?? "Text" : "Text";
                var canonVal = factEl.TryGetProperty("canonical_value", out var cvEl) ? cvEl.GetString() ?? string.Empty : string.Empty;
                var rawVal = factEl.TryGetProperty("raw_value", out var rvEl) && rvEl.ValueKind == JsonValueKind.String ? rvEl.GetString() : null;
                var normVal = factEl.TryGetProperty("normalized_value", out var nvEl) && nvEl.ValueKind == JsonValueKind.String ? nvEl.GetString() : null;
                double? fConf = factEl.TryGetProperty("confidence", out var cEl) && cEl.ValueKind == JsonValueKind.Number && cEl.TryGetDouble(out var cdVal) ? cdVal : null;

                bool isGrounded = false;
                if (factEl.TryGetProperty("is_evidence_verified", out var ievEl))
                {
                    isGrounded = ievEl.ValueKind == JsonValueKind.True;
                }
                else if (factEl.TryGetProperty("evidence", out var evCheck) && evCheck.TryGetProperty("is_evidence_verified", out var ievEl2))
                {
                    isGrounded = ievEl2.ValueKind == JsonValueKind.True;
                }

                bool isSemanticValid = !factEl.TryGetProperty("is_semantic_valid", out var isvEl) || isvEl.ValueKind != JsonValueKind.False;
                string valStatus = factEl.TryGetProperty("validation_status", out var vsEl) ? vsEl.GetString() ?? "Valid" : "Valid";
                string candStatus = factEl.TryGetProperty("candidate_status", out var csEl) ? csEl.GetString() ?? "Accepted" : "Accepted";

                var reasons = new List<string>();
                if (factEl.TryGetProperty("validation_reasons", out var vrEl) && vrEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var rItem in vrEl.EnumerateArray())
                    {
                        var rStr = rItem.GetString();
                        if (!string.IsNullOrWhiteSpace(rStr)) reasons.Add(rStr);
                    }
                }

                int rank = factEl.TryGetProperty("rank", out var rkEl) && rkEl.ValueKind == JsonValueKind.Number && rkEl.TryGetInt32(out var rVal) ? rVal : 1;
                bool isPref = factEl.TryGetProperty("is_preferred", out var ipEl) && ipEl.ValueKind == JsonValueKind.True;

                string? warning = factEl.TryGetProperty("validation_warning", out var vwEl) ? vwEl.GetString() : null;
                if (!isGrounded && string.IsNullOrWhiteSpace(warning))
                {
                    warning = "Evidence not verified against OCR transcript";
                }

                int? evPage = null;
                string? evQuote = null;
                if (factEl.TryGetProperty("evidence", out var evEl) && evEl.ValueKind == JsonValueKind.Object)
                {
                    if (evEl.TryGetProperty("page_number", out var pnEl) && pnEl.ValueKind == JsonValueKind.Number && pnEl.TryGetInt32(out var pVal)) evPage = pVal;
                    else if (evEl.TryGetProperty("page", out var pEl2) && pEl2.ValueKind == JsonValueKind.Number && pEl2.TryGetInt32(out var pVal2)) evPage = pVal2;

                    if (evEl.TryGetProperty("quote", out var qEl) && qEl.ValueKind == JsonValueKind.String) evQuote = qEl.GetString();
                    else if (evEl.TryGetProperty("exact_quote", out var qEl2) && qEl2.ValueKind == JsonValueKind.String) evQuote = qEl2.GetString();
                }

                string? srcType = factEl.TryGetProperty("source_type", out var stEl) && stEl.ValueKind == JsonValueKind.String ? stEl.GetString() : "Explicit";
                string? normEnVal = factEl.TryGetProperty("normalized_english_value", out var nevEl) && nevEl.ValueKind == JsonValueKind.String ? nevEl.GetString() : null;

                return new CandidateFactResponse(
                    FactCode: fieldCode,
                    CanonicalValue: canonVal,
                    ValueKind: valKind,
                    Confidence: fConf,
                    EvidencePage: evPage,
                    EvidenceQuote: evQuote,
                    IsGrounded: isGrounded,
                    ValidationWarning: warning,
                    RawValue: rawVal,
                    NormalizedValue: normVal,
                    IsSemanticValid: isSemanticValid,
                    ValidationStatus: valStatus,
                    CandidateStatus: candStatus,
                    ValidationReasons: reasons.Count > 0 ? reasons : null,
                    Rank: rank,
                    IsPreferred: isPref,
                    SourceType: srcType,
                    NormalizedEnglishValue: normEnVal
                );
            }

            var semanticArtifact = artifacts.FirstOrDefault(a => a.ArtifactKind == "SemanticRawResponse");
            if (semanticArtifact != null)
            {
                var semanticBytes = await artifactReader.ReadAsync(semanticArtifact.StorageReference, cancellationToken);
                if (semanticBytes.HasValue)
                {
                    try
                    {
                        using var jsonDoc = JsonDocument.Parse(semanticBytes.Value);
                        var root = jsonDoc.RootElement;

                        if (root.TryGetProperty("classification", out var classEl))
                        {
                            var code = classEl.TryGetProperty("code", out var codeEl)
                                ? codeEl.GetString() ?? "Unknown"
                                : (classEl.TryGetProperty("document_type", out var dtEl) ? dtEl.GetString() ?? "Unknown" : "Unknown");

                            double? conf = classEl.TryGetProperty("confidence", out var confEl) && confEl.ValueKind == JsonValueKind.Number && confEl.TryGetDouble(out var dVal)
                                ? dVal
                                : null;

                            bool hasSemanticFailureWarning = warningsList.Any(w =>
                                w.Contains("Ollama failure", StringComparison.OrdinalIgnoreCase) ||
                                w.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
                                w.Contains("did not complete", StringComparison.OrdinalIgnoreCase));

                            if (string.Equals(code, "Unavailable", StringComparison.OrdinalIgnoreCase) ||
                                (string.Equals(code, "Unknown", StringComparison.OrdinalIgnoreCase) && (conf == null || conf == 0.0) && hasSemanticFailureWarning))
                            {
                                code = "Unavailable";
                                conf = null;
                            }

                            classification = new DocumentClassificationResponse(code, conf);
                        }

                        if (root.TryGetProperty("facts", out var factsEl) && factsEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var factEl in factsEl.EnumerateArray())
                            {
                                candidateFacts.Add(ParseFactElement(factEl));
                            }
                        }

                        if (root.TryGetProperty("all_candidates", out var allCandsEl) && allCandsEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var factEl in allCandsEl.EnumerateArray())
                            {
                                allCandidates.Add(ParseFactElement(factEl));
                            }
                        }
                        else
                        {
                            allCandidates.AddRange(candidateFacts);
                        }

                        if (root.TryGetProperty("field_inventory", out var fiEl) && fiEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var itemEl in fiEl.EnumerateArray())
                            {
                                var fCode = itemEl.TryGetProperty("field_code", out var fc) && fc.ValueKind == JsonValueKind.String ? fc.GetString() ?? "" : "";
                                var dName = itemEl.TryGetProperty("display_name", out var dn) && dn.ValueKind == JsonValueKind.String ? dn.GetString() ?? fCode : fCode;
                                var st = itemEl.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() ?? "Missing" : "Missing";
                                var pv = itemEl.TryGetProperty("preferred_value", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
                                var rv = itemEl.TryGetProperty("raw_value", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
                                int? pn = itemEl.TryGetProperty("page_number", out var pne) && pne.ValueKind == JsonValueKind.Number && pne.TryGetInt32(out var pnv) ? pnv : null;
                                double? cVal = itemEl.TryGetProperty("confidence", out var cfe) && cfe.ValueKind == JsonValueKind.Number && cfe.TryGetDouble(out var cfv) ? cfv : null;
                                bool gr = itemEl.TryGetProperty("is_grounded", out var ge) && ge.ValueKind == JsonValueKind.True;
                                int cc = itemEl.TryGetProperty("candidate_count", out var cce) && cce.ValueKind == JsonValueKind.Number && cce.TryGetInt32(out var ccv) ? ccv : 0;
                                var srcType = itemEl.TryGetProperty("source_type", out var ste) && ste.ValueKind == JsonValueKind.String ? ste.GetString() : null;
                                var normEnVal = itemEl.TryGetProperty("normalized_english_value", out var neve) && neve.ValueKind == JsonValueKind.String ? neve.GetString() : null;

                                fieldInventory.Add(new GovernedFieldInventoryItemResponse(
                                    FieldCode: fCode,
                                    DisplayName: dName,
                                    Status: st,
                                    PreferredValue: pv,
                                    RawValue: rv,
                                    PageNumber: pn,
                                    Confidence: cVal,
                                    IsGrounded: gr,
                                    CandidateCount: cc,
                                    SourceType: srcType,
                                    NormalizedEnglishValue: normEnVal
                                ));
                            }
                        }

                        candidateCount = root.TryGetProperty("candidate_count", out var ccEl) && ccEl.ValueKind == JsonValueKind.Number && ccEl.TryGetInt32(out var ccVal) ? ccVal : allCandidates.Count;
                        acceptedCount = root.TryGetProperty("accepted_count", out var acEl) && acEl.ValueKind == JsonValueKind.Number && acEl.TryGetInt32(out var acVal) ? acVal : allCandidates.Count(c => c.CandidateStatus == "Accepted");
                        rejectedCount = root.TryGetProperty("rejected_count", out var rcEl) && rcEl.ValueKind == JsonValueKind.Number && rcEl.TryGetInt32(out var rcVal) ? rcVal : allCandidates.Count(c => c.CandidateStatus == "Rejected");
                        conflictCount = root.TryGetProperty("conflict_count", out var cfcEl) && cfcEl.ValueKind == JsonValueKind.Number && cfcEl.TryGetInt32(out var cfcVal) ? cfcVal : allCandidates.Count(c => c.CandidateStatus == "Conflict");
                        ungroundedCount = root.TryGetProperty("ungrounded_count", out var ugEl) && ugEl.ValueKind == JsonValueKind.Number && ugEl.TryGetInt32(out var ugVal) ? ugVal : allCandidates.Count(c => c.CandidateStatus == "Ungrounded");
                        modelIdentifier = root.TryGetProperty("model_identifier", out var miEl) && miEl.ValueKind == JsonValueKind.String ? miEl.GetString() : null;
                        promptVersion = root.TryGetProperty("prompt_version", out var pvEl) && pvEl.ValueKind == JsonValueKind.String ? pvEl.GetString() : null;
                        returnedSemanticProvider = root.TryGetProperty("semantic_provider", out var spEl) && spEl.ValueKind == JsonValueKind.String ? spEl.GetString() : null;

                        if (root.TryGetProperty("duration_breakdown", out var durEl))
                        {
                            double? ocrMs = durEl.TryGetProperty("ocr_duration_ms", out var ocrEl) && ocrEl.TryGetDouble(out var oVal) ? oVal : null;
                            double? semMs = durEl.TryGetProperty("semantic_duration_ms", out var semEl) && semEl.TryGetDouble(out var sVal) ? sVal : null;
                            double? totMs = durEl.TryGetProperty("total_duration_ms", out var totEl) && totEl.TryGetDouble(out var tVal) ? tVal : null;
                            durationBreakdown = new ExecutionDurationResponse(ocrMs, semMs, totMs);
                        }

                        if (root.TryGetProperty("warnings", out var wEl) && wEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in wEl.EnumerateArray())
                            {
                                var wStr = item.GetString();
                                if (!string.IsNullOrWhiteSpace(wStr) && !warningsList.Contains(wStr))
                                {
                                    warningsList.Add(wStr);
                                }
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse semantic raw response JSON from {Reference}", semanticArtifact.StorageReference);
                    }
                }
            }

            string? semanticStatus = null;
            if (isSemanticMode)
            {
                bool hasFailureOrTimeout = warningsList.Any(w =>
                    w.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
                    w.Contains("Ollama failure", StringComparison.OrdinalIgnoreCase) ||
                    w.Contains("Gemini failure", StringComparison.OrdinalIgnoreCase) ||
                    w.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                    w.Contains("unreachable", StringComparison.OrdinalIgnoreCase) ||
                    w.Contains("did not complete", StringComparison.OrdinalIgnoreCase));

                if (hasFailureOrTimeout)
                {
                    semanticStatus = candidateFacts.Count > 0 ? "Completed with Warnings" : "Failed / Timed Out";
                }
                else if (classification != null && !string.Equals(classification.DocumentType, "Unavailable", StringComparison.OrdinalIgnoreCase))
                {
                    semanticStatus = "Completed";
                }
                else
                {
                    semanticStatus = "Failed / Timed Out";
                }

                if (string.Equals(semanticStatus, "Failed / Timed Out", StringComparison.OrdinalIgnoreCase))
                {
                    // Distinguish unanalyzed/unavailable fields from genuinely missing fields in the document
                    for (int i = 0; i < fieldInventory.Count; i++)
                    {
                        if (string.Equals(fieldInventory[i].Status, "Missing", StringComparison.OrdinalIgnoreCase))
                        {
                            fieldInventory[i] = fieldInventory[i] with { Status = "Unavailable" };
                        }
                    }
                }
            }

            var governanceNotice = isSemanticMode
                ? "AI-extracted facts are machine-generated candidate facts and are not authoritative until reviewed and verified by an authorized human officer."
                : "OCR text is machine-generated and has not been human verified.";

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
                Warnings: warningsList,
                GovernanceNotice: governanceNotice,
                ExecutionMode: isSemanticMode ? "analyze" : "ocr",
                Classification: classification,
                CandidateFacts: candidateFacts.Count > 0 ? candidateFacts : null,
                DurationBreakdown: durationBreakdown,
                SemanticStatus: semanticStatus,
                AllCandidates: allCandidates.Count > 0 ? allCandidates : null,
                FieldInventory: fieldInventory.Count > 0 ? fieldInventory : null,
                CandidateCount: candidateCount,
                AcceptedCount: acceptedCount,
                RejectedCount: rejectedCount,
                ConflictCount: conflictCount,
                UngroundedCount: ungroundedCount,
                ModelIdentifier: modelIdentifier,
                PromptVersion: promptVersion,
                SemanticProvider: isSemanticMode ? (returnedSemanticProvider ?? effectiveSemanticProvider) : null);

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
