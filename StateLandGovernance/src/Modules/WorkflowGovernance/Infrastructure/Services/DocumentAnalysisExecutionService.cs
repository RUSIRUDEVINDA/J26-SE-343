namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Services;

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.Exceptions;

/// <summary>
/// Infrastructure coordinator for governed document analysis execution.
/// Enforces exact version binding, lifecycle progression via Application command handlers,
/// single-read last-mile SHA-256 byte-integrity verification via the intelligence adapter,
/// and machine-generated artifact persistence.
/// 
/// Invariants:
/// 1. Exact Version Binding: Operates strictly on the GovernedDocumentId and DocumentVersionId bound to the run.
/// 2. Last-Mile Byte Integrity: The underlying intelligence service buffers source bytes once, verifies SHA-256,
///    and sends those exact verified bytes in the HTTP upload (preventing TOCTOU and duplicate reads).
/// 3. Zero Semantic Facts: Result candidates are strictly empty; OCR observations are never promoted to case truth.
/// 4. Transaction Boundaries: No database transaction is held open during external HTTP OCR or artifact writing.
/// 5. Concurrency & Revisions: Uses authoritative revisions returned by command handlers.
/// 6. Cancellation Cleanup: When cancelled after Start has been committed, cleans up run to Failed state
///    with code EXECUTION_CANCELLED using a bounded context to prevent orphaned Running runs.
/// </summary>
public sealed class DocumentAnalysisExecutionService : IDocumentAnalysisExecutionService
{
    private readonly IDocumentAnalysisRepository _analysisRepository;
    private readonly IGovernedDocumentRepository _documentRepository;
    private readonly IDocumentIntelligenceService _intelligenceService;
    private readonly ICommandHandler<StartDocumentAnalysisRunCommand, DocumentAnalysisDto> _startRunHandler;
    private readonly ICommandHandler<CompleteDocumentAnalysisCommand, DocumentAnalysisDto> _completeRunHandler;
    private readonly ICommandHandler<FailDocumentAnalysisCommand, DocumentAnalysisDto> _failRunHandler;
    private readonly DocumentIntelligenceOptions _options;
    private readonly ILogger<DocumentAnalysisExecutionService> _logger;

    public DocumentAnalysisExecutionService(
        IDocumentAnalysisRepository analysisRepository,
        IGovernedDocumentRepository documentRepository,
        IDocumentIntelligenceService intelligenceService,
        ICommandHandler<StartDocumentAnalysisRunCommand, DocumentAnalysisDto> startRunHandler,
        ICommandHandler<CompleteDocumentAnalysisCommand, DocumentAnalysisDto> completeRunHandler,
        ICommandHandler<FailDocumentAnalysisCommand, DocumentAnalysisDto> failRunHandler,
        IOptions<DocumentIntelligenceOptions> options,
        ILogger<DocumentAnalysisExecutionService> logger)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _intelligenceService = intelligenceService ?? throw new ArgumentNullException(nameof(intelligenceService));
        _startRunHandler = startRunHandler ?? throw new ArgumentNullException(nameof(startRunHandler));
        _completeRunHandler = completeRunHandler ?? throw new ArgumentNullException(nameof(completeRunHandler));
        _failRunHandler = failRunHandler ?? throw new ArgumentNullException(nameof(failRunHandler));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DocumentAnalysisExecutionResult> ExecuteAsync(
        Guid documentAnalysisId,
        Guid analysisRunId,
        int expectedAnalysisRevision,
        string? languageHint = null,
        CancellationToken cancellationToken = default)
    {
        if (documentAnalysisId == Guid.Empty) throw new ArgumentException("DocumentAnalysisId cannot be empty.", nameof(documentAnalysisId));
        if (analysisRunId == Guid.Empty) throw new ArgumentException("AnalysisRunId cannot be empty.", nameof(analysisRunId));
        if (expectedAnalysisRevision <= 0) throw new ArgumentOutOfRangeException(nameof(expectedAnalysisRevision), "Expected revision must be positive.");

        // Check cancellation before any mutation occurs
        cancellationToken.ThrowIfCancellationRequested();

        // Validate language hint if specified
        string? normalizedLanguage = null;
        if (!string.IsNullOrWhiteSpace(languageHint))
        {
            var trimmed = languageHint.Trim().ToLowerInvariant();
            if (trimmed is not ("eng" or "sin" or "sin+eng"))
            {
                throw new ArgumentException($"Language mode '{languageHint}' is not supported. Supported modes are eng, sin, sin+eng.", nameof(languageHint));
            }
            normalizedLanguage = trimmed;
        }

        // 1. Fetch DocumentAnalysis aggregate
        var analysis = await _analysisRepository.GetByIdAsync(
            new DocumentAnalysisId(documentAnalysisId),
            cancellationToken);

        if (analysis == null)
        {
            throw new DocumentAnalysisNotFoundException(documentAnalysisId);
        }

        // 2. Initial optimistic concurrency check
        if (analysis.Revision != expectedAnalysisRevision)
        {
            throw new AnalysisConcurrencyException(documentAnalysisId, expectedAnalysisRevision, analysis.Revision);
        }

        // 3. Locate target AnalysisRun
        var run = analysis.Runs.FirstOrDefault(r => r.Id.Value == analysisRunId);
        if (run == null)
        {
            throw new AnalysisRunNotFoundException($"AnalysisRun with ID {analysisRunId} was not found on aggregate {documentAnalysisId}.");
        }

        // 4. Idempotency checks on current run state
        switch (run.State)
        {
            case AnalysisRunState.Completed:
                _logger.LogInformation("AnalysisRun {RunId} is already Completed. Returning existing result.", analysisRunId);
                return new DocumentAnalysisExecutionResult(
                    DocumentAnalysisId: documentAnalysisId,
                    AnalysisRunId: analysisRunId,
                    Status: "Completed",
                    ArtifactCount: run.Result?.Artifacts.Count ?? 0);

            case AnalysisRunState.Failed:
                _logger.LogInformation("AnalysisRun {RunId} has already Failed.", analysisRunId);
                return new DocumentAnalysisExecutionResult(
                    DocumentAnalysisId: documentAnalysisId,
                    AnalysisRunId: analysisRunId,
                    Status: "Failed",
                    ArtifactCount: 0,
                    FailureCode: run.Failure?.Code,
                    FailureDescription: run.Failure?.Description);

            case AnalysisRunState.Superseded:
                _logger.LogInformation("AnalysisRun {RunId} is Superseded.", analysisRunId);
                return new DocumentAnalysisExecutionResult(
                    DocumentAnalysisId: documentAnalysisId,
                    AnalysisRunId: analysisRunId,
                    Status: "Superseded",
                    ArtifactCount: 0);

            case AnalysisRunState.Running:
                throw new InvalidOperationException($"AnalysisRun {analysisRunId} is already in Running state and cannot be claimed concurrently.");

            case AnalysisRunState.Requested:
                break;

            default:
                throw new InvalidOperationException($"AnalysisRun {analysisRunId} is in unhandled state {run.State}.");
        }

        // 5. Capability validation - OCR vertical slice strictly supports OCR capabilities only
        var hasUnsupportedCapabilities = run.RequestedCapabilities.Any(c =>
            !string.Equals(c.Value, "Ocr", StringComparison.OrdinalIgnoreCase));

        // 6. Claim/Start the analysis run (Transitions Requested -> Running, increments revision by 1, 1 DB Commit)
        var startCommand = new StartDocumentAnalysisRunCommand(
            DocumentAnalysisId: documentAnalysisId,
            AnalysisRunId: analysisRunId,
            ExpectedRevision: expectedAnalysisRevision);

        var startedDto = await _startRunHandler.HandleAsync(startCommand, cancellationToken);
        var currentRevision = startedDto.Revision;

        // 7. Post-claim pre-flight checks (fail run cleanly if configuration/binding is invalid)
        if (hasUnsupportedCapabilities)
        {
            return await FailRunAsync(
                documentAnalysisId,
                analysisRunId,
                currentRevision,
                "UNSUPPORTED_CAPABILITY",
                "Requested capabilities include unsupported semantic models. OCR vertical slice supports 'Ocr' capability only.",
                CancellationToken.None);
        }

        // 8. Execute external analysis pipeline within guarded try/catch
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Verify GovernedDocument and exact version exist
            var governedDoc = await _documentRepository.GetByIdAsync(analysis.GovernedDocumentId, cancellationToken);
            if (governedDoc == null)
            {
                return await FailRunAsync(
                    documentAnalysisId,
                    analysisRunId,
                    currentRevision,
                    "DOCUMENT_NOT_FOUND",
                    $"GovernedDocument with ID {analysis.GovernedDocumentId.Value} does not exist.",
                    cancellationToken);
            }

            var version = governedDoc.Versions.FirstOrDefault(v => v.Id.Value == run.DocumentVersionId.Value);
            if (version == null)
            {
                return await FailRunAsync(
                    documentAnalysisId,
                    analysisRunId,
                    currentRevision,
                    "VERSION_NOT_FOUND",
                    $"DocumentVersion {run.DocumentVersionId.Value} not found on GovernedDocument {analysis.GovernedDocumentId.Value}.",
                    cancellationToken);
            }

            // Verify checksum algorithm matches expected SHA-256
            if (!string.Equals(version.Checksum.Algorithm, "SHA-256", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(run.DocumentChecksum.Algorithm, "SHA-256", StringComparison.OrdinalIgnoreCase))
            {
                return await FailRunAsync(
                    documentAnalysisId,
                    analysisRunId,
                    currentRevision,
                    "UNSUPPORTED_CHECKSUM_ALGORITHM",
                    "Governed document checksum algorithm must be SHA-256.",
                    cancellationToken);
            }

            // Early checksum match guard: version checksum must match requested run checksum
            if (!string.Equals(version.Checksum.Value.Trim(), run.DocumentChecksum.Value.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return await FailRunAsync(
                    documentAnalysisId,
                    analysisRunId,
                    currentRevision,
                    "CHECKSUM_MISMATCH",
                    "Source document version checksum does not match requested analysis run checksum.",
                    cancellationToken);
            }

            // Verify file size does not exceed configured maximum limit
            if (version.FileSizeInBytes > _options.MaxUploadBytes)
            {
                return await FailRunAsync(
                    documentAnalysisId,
                    analysisRunId,
                    currentRevision,
                    "FILE_TOO_LARGE",
                    $"Document file size ({version.FileSizeInBytes} bytes) exceeds maximum limit of {_options.MaxUploadBytes} bytes.",
                    cancellationToken);
            }

            // Construct DocumentIntelligenceRequest from authoritative governed data
            var ocrRequest = new DocumentIntelligenceRequest(
                GovernedDocumentId: analysis.GovernedDocumentId.Value,
                DocumentVersionId: run.DocumentVersionId.Value,
                ContentReference: version.ContentReference.Value,
                ChecksumAlgorithm: version.Checksum.Algorithm,
                ChecksumValue: version.Checksum.Value,
                OriginalFileName: version.OriginalFileName,
                MediaType: version.MediaType,
                LogicalCategory: governedDoc.LogicalCategory,
                RequestedCapabilities: run.RequestedCapabilities.Select(c => c.Value).ToList(),
                LanguageHint: normalizedLanguage
            );

            // Execute external document intelligence:
            // The adapter performs a single source byte read, verifies last-mile SHA-256 over exact bytes,
            // uploads those same exact bytes to FastAPI, and writes persisted OCR artifacts.
            var ocrResult = await _intelligenceService.AnalyzeDocumentAsync(ocrRequest, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // CRITICAL INVARIANT: Zero semantic facts. OCR transcript artifacts only.
            var candidateFacts = Array.Empty<ExtractedCandidateFactDto>();

            // Complete analysis run through Application command handler (1 DB Commit)
            var outcome = ocrResult.Artifacts.Count > 0 ? "OutputsProduced" : "NoFindings";

            var completeCommand = new CompleteDocumentAnalysisCommand(
                DocumentAnalysisId: documentAnalysisId,
                AnalysisRunId: analysisRunId,
                ExpectedRevision: currentRevision,
                Outcome: outcome,
                Artifacts: ocrResult.Artifacts,
                ExtractedCandidateFacts: candidateFacts,
                ResultId: Guid.NewGuid()
            );

            var completedDto = await _completeRunHandler.HandleAsync(completeCommand, cancellationToken);

            return new DocumentAnalysisExecutionResult(
                DocumentAnalysisId: documentAnalysisId,
                AnalysisRunId: analysisRunId,
                Status: "Completed",
                ArtifactCount: ocrResult.Artifacts.Count
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Document analysis execution for {RunId} was cancelled after run was started. Cleaning up run state to Failed.", analysisRunId);

            // Best-effort transition to Failed using a separate bounded non-caller cancellation context (5 seconds)
            using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await FailRunAsync(
                    documentAnalysisId,
                    analysisRunId,
                    currentRevision,
                    "EXECUTION_CANCELLED",
                    "Document analysis execution was cancelled by caller.",
                    cleanupCts.Token);
            }
            catch (AnalysisConcurrencyException)
            {
                // Surface concurrency conflict clearly as required by prompt
                throw;
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "Failed to persist cancellation failure state for run {RunId}", analysisRunId);
            }

            // Propagate cancellation
            throw;
        }
        catch (AnalysisConcurrencyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Technical failure during document analysis execution for run {RunId}", analysisRunId);
            var (failureCode, safeDescription) = MapExceptionToFailure(ex);
            return await FailRunAsync(documentAnalysisId, analysisRunId, currentRevision, failureCode, safeDescription, CancellationToken.None);
        }
    }

    private async Task<DocumentAnalysisExecutionResult> FailRunAsync(
        Guid documentAnalysisId,
        Guid analysisRunId,
        int expectedRevision,
        string failureCode,
        string safeDescription,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var failCommand = new FailDocumentAnalysisCommand(
                DocumentAnalysisId: documentAnalysisId,
                AnalysisRunId: analysisRunId,
                ExpectedRevision: expectedRevision,
                FailureCode: failureCode,
                SafeDescription: SanitizeText(safeDescription)
            );

            await _failRunHandler.HandleAsync(failCommand, cancellationToken);

            return new DocumentAnalysisExecutionResult(
                DocumentAnalysisId: documentAnalysisId,
                AnalysisRunId: analysisRunId,
                Status: "Failed",
                ArtifactCount: 0,
                FailureCode: failureCode,
                FailureDescription: safeDescription
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record analysis run failure for {RunId}", analysisRunId);
            throw;
        }
    }

    private static (string FailureCode, string SafeDescription) MapExceptionToFailure(Exception ex)
    {
        if (ex is AggregateException agg && agg.InnerExceptions.Count == 1)
        {
            ex = agg.InnerExceptions[0];
        }

        return ex switch
        {
            DocumentContentIntegrityException =>
                ("CHECKSUM_MISMATCH", "Source document content SHA-256 checksum does not match registered version checksum."),

            DocumentIntelligenceTimeoutException =>
                ("OCR_TIMEOUT", "Document intelligence processing timed out."),

            DocumentIntelligenceServiceUnavailableException =>
                ("SERVICE_UNAVAILABLE", "Document intelligence service is currently unavailable."),

            DocumentIntelligenceValidationException =>
                ("VALIDATION_FAILED", "Document intelligence validation failed."),

            DocumentIntelligencePayloadTooLargeException =>
                ("PAYLOAD_TOO_LARGE", "Document payload exceeds maximum permitted upload threshold."),

            DocumentIntelligenceUnsupportedMediaException =>
                ("UNSUPPORTED_MEDIA", "Document media type is not supported for OCR analysis."),

            DocumentIntelligenceLanguageUnavailableException =>
                ("LANGUAGE_UNAVAILABLE", "Requested OCR language pack is not available on server."),

            DocumentIntelligenceServerException =>
                ("OCR_SERVER_ERROR", "An internal error occurred during document intelligence processing."),

            FileNotFoundException =>
                ("CONTENT_NOT_FOUND", "Document content stream could not be found in storage."),

            _ =>
                ("TECHNICAL_FAILURE", SanitizeText(ex.Message))
        };
    }

    private static string SanitizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "An internal processing error occurred.";
        }

        var trimmed = text.Trim();

        // Redact file paths
        trimmed = Regex.Replace(
            trimmed,
            @"[a-zA-Z]:\\[^\s""'<>]+|/(?:home|Users|var|tmp|etc|usr|app)/[^\s""'<>]+",
            "[path-redacted]");

        // Redact stack traces
        if (trimmed.Contains("Traceback", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("at System.", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "An internal processing error occurred.";
        }

        return trimmed;
    }
}
