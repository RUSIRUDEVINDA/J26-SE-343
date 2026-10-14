namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.Exceptions;

/// <summary>
/// Infrastructure adapter connecting the Application IDocumentIntelligenceService port
/// to the Python FastAPI OCR service (POST /v1/ocr) via typed HttpClient.
/// 
/// Governance Invariants:
/// 1. Zero Semantic Facts: Returns OCR transcript artifacts only. Never fabricates legal entities,
///    candidate facts, or maps OCR optical confidence to legal fact confidence.
/// 2. Mandatory Content Reader: Reads exact document version bytes through IDocumentContentReader.
/// 3. Mandatory Artifact Writer: Persists exact UTF-8 transcript bytes (full document and per-page)
///    through IAnalysisArtifactWriter before returning, verifying SHA-256 receipts and returning
///    actual resolvable storage references.
/// 4. Error Sanitization: Strips local server filesystem paths and stack traces from error messages.
/// 5. Deterministic Keying: Uses deterministic storage keys (analysis-artifacts/{versionId}/...)
///    so retries overwrite/upsert idempotently without orphan references.
/// </summary>
public sealed class FastApiDocumentIntelligenceService : IDocumentIntelligenceService
{
    private readonly HttpClient _httpClient;
    private readonly DocumentIntelligenceOptions _options;
    private readonly ILogger<FastApiDocumentIntelligenceService> _logger;
    private readonly IDocumentContentReader _contentReader;
    private readonly IAnalysisArtifactWriter _artifactWriter;

    public FastApiDocumentIntelligenceService(
        HttpClient httpClient,
        IOptions<DocumentIntelligenceOptions> options,
        ILogger<FastApiDocumentIntelligenceService> logger,
        IDocumentContentReader contentReader,
        IAnalysisArtifactWriter artifactWriter)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contentReader = contentReader ?? throw new ArgumentNullException(nameof(contentReader));
        _artifactWriter = artifactWriter ?? throw new ArgumentNullException(nameof(artifactWriter));

        _logger.LogInformation(
            "FastApiDocumentIntelligenceService initialized with BaseUrl: {BaseUrl}, RequestTimeoutSeconds: {Timeout}s, HttpClient.Timeout: {HttpTimeout}s",
            _options.BaseUrl,
            _options.RequestTimeoutSeconds,
            _httpClient.Timeout.TotalSeconds);
    }

    /// <summary>
    /// Gets the timeout configured on the underlying HttpClient.
    /// Exposes effective timeout binding for diagnostics, logging, and integration testing.
    /// </summary>
    public TimeSpan ConfiguredHttpClientTimeout => _httpClient.Timeout;

    public async Task<DocumentIntelligenceResult> AnalyzeDocumentAsync(
        DocumentIntelligenceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var correlationId = request.DocumentVersionId.ToString("D");

        // 1. Retrieve document content bytes through the Application content reader port
        var documentId = new GovernedDocumentId(request.GovernedDocumentId);
        var versionId = new DocumentVersionId(request.DocumentVersionId);

        Stream? contentStream;
        try
        {
            contentStream = await _contentReader.ReadContentAsync(
                documentId,
                versionId,
                request.ContentReference,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Failed to read document content for GovernedDocumentId {DocumentId}",
                correlationId, request.GovernedDocumentId);
            throw;
        }

        if (contentStream is null)
        {
            throw new FileNotFoundException(
                $"Document content stream not found for version {request.DocumentVersionId} at reference '{request.ContentReference}'.");
        }

        byte[] contentBytes;
        await using (contentStream.ConfigureAwait(false))
        {
            using var ms = new MemoryStream();
            var buffer = new byte[81920];
            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                ms.Write(buffer, 0, bytesRead);
                if (ms.Length > _options.MaxUploadBytes)
                {
                    throw new DocumentIntelligencePayloadTooLargeException(
                        $"Document payload size ({ms.Length} bytes) exceeds maximum permitted threshold of {_options.MaxUploadBytes} bytes.");
                }
            }
            contentBytes = ms.ToArray();
        }

        // 2. Authoritative last-mile SHA-256 byte-integrity verification over exact bytes to be uploaded
        var computedHash = Convert.ToHexString(SHA256.HashData(contentBytes)).ToLowerInvariant();
        var expectedHash = (request.ChecksumValue ?? string.Empty).Trim().ToLowerInvariant();

        if (!string.Equals(computedHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "[{CorrelationId}] Source byte integrity failure for version {VersionId}. Expected SHA-256: {Expected}, Computed: {Computed}",
                correlationId, request.DocumentVersionId, expectedHash, computedHash);

            throw new DocumentContentIntegrityException(
                "Source document content SHA-256 checksum does not match registered version checksum.",
                expectedHash,
                computedHash);
        }

        // 3. Prepare multipart/form-data request using the EXACT SAME verified contentBytes
        var languageMode = ResolveLanguageMode(request.LanguageHint, _options.DefaultLanguageMode);
        var sanitizedFileName = SanitizeFileName(request.OriginalFileName, request.DocumentVersionId);

        using var multipartContent = new MultipartFormDataContent();

        var byteArrayContent = new ByteArrayContent(contentBytes);
        var mediaType = string.IsNullOrWhiteSpace(request.MediaType) ? "application/octet-stream" : request.MediaType;
        byteArrayContent.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        multipartContent.Add(byteArrayContent, "file", sanitizedFileName);

        var isSemantic = RequiresSemanticAnalysis(request.RequestedCapabilities);

        // 3. Build target URL with query parameters
        var uriBuilder = new StringBuilder(isSemantic ? "v1/analyze" : "v1/ocr");
        uriBuilder.Append("?language_mode=").Append(Uri.EscapeDataString(languageMode));
        uriBuilder.Append("&preprocess=").Append(_options.Preprocess ? "true" : "false");
        if (isSemantic && !string.IsNullOrWhiteSpace(request.LogicalCategory))
        {
            uriBuilder.Append("&category_hint=").Append(Uri.EscapeDataString(request.LogicalCategory));
        }
        if (isSemantic && !string.IsNullOrWhiteSpace(request.SemanticProvider))
        {
            uriBuilder.Append("&semantic_provider=").Append(Uri.EscapeDataString(request.SemanticProvider));
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, uriBuilder.ToString())
        {
            Content = multipartContent
        };

        httpRequest.Headers.TryAddWithoutValidation("X-Request-ID", correlationId);
        httpRequest.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

        // 4. Send HTTP request to FastAPI document intelligence service
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("[{CorrelationId}] Document intelligence processing cancelled by caller.", correlationId);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Document intelligence processing timed out after {Timeout} seconds.",
                correlationId, _options.RequestTimeoutSeconds);
            throw new DocumentIntelligenceTimeoutException(
                $"Document intelligence service timed out after {_options.RequestTimeoutSeconds} seconds.",
                innerException: ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Network failure communicating with document intelligence service.", correlationId);
            throw new DocumentIntelligenceServiceUnavailableException(
                "Document intelligence service communication failed due to a network or transport error.",
                innerException: ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                await HandleErrorResponseAsync(response, correlationId, cancellationToken);
            }

            if (isSemantic)
            {
                FastApiAnalyzeResponseDto? analyzeResponse;
                try
                {
                    analyzeResponse = await response.Content.ReadFromJsonAsync<FastApiAnalyzeResponseDto>(
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{CorrelationId}] Failed to deserialize analyze response.", correlationId);
                    throw new DocumentIntelligenceServerException(
                        "Failed to deserialize valid response from document intelligence service.",
                        (int)response.StatusCode,
                        innerException: ex);
                }

                if (analyzeResponse is null)
                {
                    throw new DocumentIntelligenceServerException(
                        "Document intelligence service returned empty response payload.",
                        (int)response.StatusCode);
                }

                return await MapAndPersistSemanticArtifactsAsync(analyzeResponse, request, correlationId, cancellationToken);
            }
            else
            {
                FastApiOcrResponseDto? ocrResponse;
                try
                {
                    ocrResponse = await response.Content.ReadFromJsonAsync<FastApiOcrResponseDto>(
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{CorrelationId}] Failed to deserialize OCR response.", correlationId);
                    throw new DocumentIntelligenceServerException(
                        "Failed to deserialize valid response from document intelligence service.",
                        (int)response.StatusCode,
                        innerException: ex);
                }

                if (ocrResponse is null)
                {
                    throw new DocumentIntelligenceServerException(
                        "Document intelligence service returned empty response payload.",
                        (int)response.StatusCode);
                }

                // 5. Persist OCR artifacts through mandatory IAnalysisArtifactWriter and map result
                return await MapAndPersistArtifactsAsync(ocrResponse, request, correlationId, cancellationToken);
            }
        }
    }

    private static string ResolveLanguageMode(string? hint, string fallback)
    {
        if (string.IsNullOrWhiteSpace(hint))
        {
            return fallback;
        }

        var normalized = hint.Trim().ToLowerInvariant();
        return normalized switch
        {
            "eng" or "english" => "eng",
            "sin" or "sinhala" => "sin",
            "sin+eng" or "mixed" or "both" or "bilingual" => "sin+eng",
            _ => fallback
        };
    }

    private static string SanitizeFileName(string? originalFileName, Guid versionId)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return $"{versionId:D}.bin";
        }

        var fileName = Path.GetFileName(originalFileName.Trim());
        var extension = Path.GetExtension(fileName);

        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".bin";
        }

        return $"{versionId:D}{extension}";
    }

    private async Task HandleErrorResponseAsync(
        HttpResponseMessage response,
        string correlationId,
        CancellationToken cancellationToken)
    {
        string? errorCode = null;
        string? detail = null;

        try
        {
            var errorPayload = await response.Content.ReadFromJsonAsync<FastApiErrorResponseDto>(
                cancellationToken: cancellationToken);
            if (errorPayload is not null)
            {
                errorCode = errorPayload.ErrorCode;
                detail = errorPayload.Detail;
            }
        }
        catch
        {
            try
            {
                detail = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch
            {
                detail = null;
            }
        }

        var sanitizedDetail = SanitizeErrorDetail(detail, response.StatusCode);

        _logger.LogWarning("[{CorrelationId}] Document intelligence service returned {StatusCode}: {Detail}",
            correlationId, response.StatusCode, sanitizedDetail);

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest: // 400
                throw new DocumentIntelligenceValidationException(sanitizedDetail, errorCode);

            case (HttpStatusCode)413: // Payload Too Large
                throw new DocumentIntelligencePayloadTooLargeException(sanitizedDetail, errorCode);

            case HttpStatusCode.UnsupportedMediaType: // 415
                throw new DocumentIntelligenceUnsupportedMediaException(sanitizedDetail, errorCode);

            case HttpStatusCode.UnprocessableEntity: // 422
                throw new DocumentIntelligenceLanguageUnavailableException(sanitizedDetail, errorCode);

            case HttpStatusCode.ServiceUnavailable: // 503
                throw new DocumentIntelligenceServiceUnavailableException(sanitizedDetail, errorCode);

            default:
                throw new DocumentIntelligenceServerException(sanitizedDetail, (int)response.StatusCode, errorCode);
        }
    }

    private static string SanitizeErrorDetail(string? detail, HttpStatusCode statusCode)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return $"Document intelligence service returned HTTP {(int)statusCode}.";
        }

        var trimmed = detail.Trim();

        // Strip local server filesystem paths (Windows drive letters or Unix paths)
        trimmed = Regex.Replace(
            trimmed,
            @"[a-zA-Z]:\\[^\s""'<>]+|/(?:home|Users|var|tmp|etc|usr|app)/[^\s""'<>]+",
            "[path-redacted]");

        // Strip raw Python traceback traces
        if (trimmed.Contains("Traceback (most recent call last):", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "An internal error occurred during document intelligence processing.";
        }

        return trimmed;
    }

    /// <summary>
    /// Persists OCR transcript artifacts via IAnalysisArtifactWriter before assembling the
    /// final DocumentIntelligenceResult.
    /// 
    /// Invariants & Provenance:
    /// - Every artifact reference in the returned result is backed by actual bytes written to the writer.
    /// - StorageReference is derived strictly from the writer receipt.
    /// - ChecksumValue is validated against the exact UTF-8 bytes sent to the writer.
    /// - Partial write limitation: There is no distributed/transactional blob storage. If a write fails
    ///   midway, an exception is thrown and NO result is returned. Deterministic keys allow safe retry overwrite.
    /// </summary>
    private async Task<DocumentIntelligenceResult> MapAndPersistArtifactsAsync(
        FastApiOcrResponseDto ocr,
        DocumentIntelligenceRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var artifacts = new List<AnalysisArtifactOutputDto>();
        var warnings = new List<string>();

        // 1. Build composite full-document text artifact
        var fullTextBuilder = new StringBuilder();

        try
        {
            foreach (var page in ocr.Pages.OrderBy(p => p.PageNumber))
            {
                var pageText = string.IsNullOrWhiteSpace(page.CleanText) ? page.Text : page.CleanText;
                if (fullTextBuilder.Length > 0)
                {
                    fullTextBuilder.Append("\n\n");
                }
                fullTextBuilder.Append(pageText);

                // 2. Build per-page transcript artifact preserving strict 1-based page provenance and metadata
                var pageProvenanceDto = new FastApiPageTranscriptArtifactDto(
                    PageNumber: page.PageNumber,
                    LanguageMode: ocr.LanguageMode,
                    RawText: page.RawText,
                    CleanText: pageText
                );
                var pageBytes = JsonSerializer.SerializeToUtf8Bytes(pageProvenanceDto);
                var expectedPageHash = Convert.ToHexString(SHA256.HashData(pageBytes)).ToLowerInvariant();

                // Format proposed relative storage path compliant with Domain AnalysisResultArtifactReference validation:
                // Must not contain '://', ':', leading '/', or '..'
                var proposedPageKey = $"analysis-artifacts/{request.DocumentVersionId:D}/pages/{page.PageNumber}.json";

                var pageReceipt = await _artifactWriter.WriteAsync(
                    proposedPageKey,
                    "application/json",
                    pageBytes,
                    cancellationToken);

                if (!string.Equals(pageReceipt.ChecksumAlgorithm, "SHA-256", StringComparison.OrdinalIgnoreCase))
                {
                    throw new DocumentIntelligenceServerException(
                        $"Artifact writer returned unsupported checksum algorithm '{pageReceipt.ChecksumAlgorithm}'. Expected SHA-256.");
                }

                if (!string.Equals(pageReceipt.ChecksumValue, expectedPageHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new DocumentIntelligenceServerException(
                        $"Artifact writer receipt checksum mismatch for page {page.PageNumber}. Expected {expectedPageHash}, but received {pageReceipt.ChecksumValue}.");
                }

                // StorageReference comes strictly from the writer receipt
                artifacts.Add(new AnalysisArtifactOutputDto(
                    ArtifactId: Guid.NewGuid(),
                    ArtifactKind: "OcrPageTranscript",
                    StorageReference: pageReceipt.StorageReference,
                    ContentType: "application/json",
                    ChecksumAlgorithm: pageReceipt.ChecksumAlgorithm,
                    ChecksumValue: pageReceipt.ChecksumValue
                ));

                if (page.Confidence.HasValue)
                {
                    warnings.Add($"Page {page.PageNumber}: OCR engine confidence score is {page.Confidence.Value:F1}% " +
                                 "(unverified machine observation metric, not legal fact confidence).");
                }
            }

            var fullText = fullTextBuilder.ToString();
            var fullBytes = Encoding.UTF8.GetBytes(fullText);
            var expectedFullHash = Convert.ToHexString(SHA256.HashData(fullBytes)).ToLowerInvariant();
            var proposedDocKey = $"analysis-artifacts/{request.DocumentVersionId:D}/document-transcript.txt";

            var docReceipt = await _artifactWriter.WriteAsync(
                proposedDocKey,
                "text/plain",
                fullBytes,
                cancellationToken);

            if (!string.Equals(docReceipt.ChecksumAlgorithm, "SHA-256", StringComparison.OrdinalIgnoreCase))
            {
                throw new DocumentIntelligenceServerException(
                    $"Artifact writer returned unsupported checksum algorithm '{docReceipt.ChecksumAlgorithm}'. Expected SHA-256.");
            }

            if (!string.Equals(docReceipt.ChecksumValue, expectedFullHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new DocumentIntelligenceServerException(
                    $"Artifact writer receipt checksum mismatch for document transcript. Expected {expectedFullHash}, but received {docReceipt.ChecksumValue}.");
            }

            artifacts.Insert(0, new AnalysisArtifactOutputDto(
                ArtifactId: Guid.NewGuid(),
                ArtifactKind: "OcrDocumentTranscript",
                StorageReference: docReceipt.StorageReference,
                ContentType: "text/plain",
                ChecksumAlgorithm: docReceipt.ChecksumAlgorithm,
                ChecksumValue: docReceipt.ChecksumValue
            ));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DocumentIntelligenceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Failed to persist analysis artifacts for version {VersionId}",
                correlationId, request.DocumentVersionId);
            throw new DocumentIntelligenceServerException(
                "Failed to persist document analysis artifact to storage.",
                innerException: ex);
        }

        // 3. CRITICAL SEMANTIC INVARIANT:
        // OCR performs text recognition ONLY. It does NOT extract semantic facts.
        // Candidates list must be strictly EMPTY to prevent machine optical observations from
        // accidentally becoming authoritative case facts without human or Qwen extraction.
        var candidates = Array.Empty<ExtractedCandidateFactDto>();

        return new DocumentIntelligenceResult(
            Provider: "FastApi-TesseractOCR",
            ModelName: "Tesseract-5",
            ModelVersion: "5.4.0",
            Outcome: "Succeeded",
            Artifacts: artifacts,
            Candidates: candidates,
            Warnings: warnings.Count > 0 ? warnings : null
        );
    }

    private static bool RequiresSemanticAnalysis(IReadOnlyList<string>? requestedCapabilities)
    {
        if (requestedCapabilities == null || requestedCapabilities.Count == 0)
        {
            return false;
        }

        return requestedCapabilities.Any(c =>
            string.Equals(c, "SemanticExtraction", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c, "Classification", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c, "Analyze", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeValueKind(string? valueKind)
    {
        if (string.IsNullOrWhiteSpace(valueKind))
        {
            return "Text";
        }

        var trimmed = valueKind.Trim();
        if (string.Equals(trimmed, "String", StringComparison.OrdinalIgnoreCase))
        {
            return "Text";
        }

        return trimmed;
    }

    private static string NormalizeCanonicalValue(string? rawValue, string normalizedValueKind)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        var trimmed = rawValue.Trim();

        // Domain invariant: AnalysisFactValue strictly forbids control characters (e.g. \r, \n, \t).
        // Replace all control characters with spaces and collapse consecutive whitespace.
        var sanitizedBuilder = new StringBuilder(trimmed.Length);
        bool prevSpace = false;
        foreach (char c in trimmed)
        {
            if (char.IsControl(c) || char.IsWhiteSpace(c))
            {
                if (!prevSpace)
                {
                    sanitizedBuilder.Append(' ');
                    prevSpace = true;
                }
            }
            else
            {
                sanitizedBuilder.Append(c);
                prevSpace = false;
            }
        }
        trimmed = sanitizedBuilder.ToString().Trim();

        switch (normalizedValueKind)
        {
            case "Decimal":
                if (decimal.TryParse(trimmed, System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var d))
                {
                    return d.ToString("G29", System.Globalization.CultureInfo.InvariantCulture);
                }
                return trimmed;

            case "Integer":
                if (long.TryParse(trimmed, System.Globalization.NumberStyles.None | System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var l))
                {
                    return l.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                return trimmed;

            case "Boolean":
                return string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false";

            default:
                return trimmed;
        }
    }

    /// <summary>
    /// Persists OCR transcripts, raw semantic JSON response, and maps candidate facts
    /// into DocumentIntelligenceResult.
    ///
    /// Governance Invariants:
    /// 1. Machine output is candidate-only. ExtractedCandidateFact items are non-authoritative.
    /// 2. Artifact references are backed by actual UTF-8 bytes written to IAnalysisArtifactWriter.
    /// 3. Fact confidence is clamped to [0.0, 1.0].
    /// 4. Evidence quotes link to persisted page or document transcript artifacts.
    /// </summary>
    private async Task<DocumentIntelligenceResult> MapAndPersistSemanticArtifactsAsync(
        FastApiAnalyzeResponseDto analyze,
        DocumentIntelligenceRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var artifacts = new List<AnalysisArtifactOutputDto>();
        var warnings = new List<string>();
        var pageArtifactMap = new Dictionary<int, Guid>();

        if (analyze.Warnings != null)
        {
            warnings.AddRange(analyze.Warnings);
        }

        // Add non-authoritative governance notice
        if (analyze.Classification.Code == "Unavailable" || !analyze.Classification.Confidence.HasValue)
        {
            warnings.Add("Semantic inference did not complete. Machine classification is unavailable.");
        }
        else
        {
            warnings.Add($"Document classified as '{analyze.Classification.Code}' with machine confidence {analyze.Classification.Confidence.Value:P0}. " +
                         "Machine output is candidate-only, not authoritative.");
        }

        // 1. Build composite full-document text artifact & page artifacts
        var fullTextBuilder = new StringBuilder();

        try
        {
            foreach (var page in analyze.Pages.OrderBy(p => p.PageNumber))
            {
                var pageText = string.IsNullOrWhiteSpace(page.CleanText) ? page.Text : page.CleanText;
                if (fullTextBuilder.Length > 0)
                {
                    fullTextBuilder.Append("\n\n");
                }
                fullTextBuilder.Append(pageText);

                // Per-page transcript artifact preserving strict 1-based page provenance
                var pageProvenanceDto = new FastApiPageTranscriptArtifactDto(
                    PageNumber: page.PageNumber,
                    LanguageMode: analyze.LanguageMode,
                    RawText: page.RawText,
                    CleanText: pageText
                );
                var pageBytes = JsonSerializer.SerializeToUtf8Bytes(pageProvenanceDto);
                var expectedPageHash = Convert.ToHexString(SHA256.HashData(pageBytes)).ToLowerInvariant();

                var proposedPageKey = $"analysis-artifacts/{request.DocumentVersionId:D}/pages/{page.PageNumber}.json";

                var pageReceipt = await _artifactWriter.WriteAsync(
                    proposedPageKey,
                    "application/json",
                    pageBytes,
                    cancellationToken);

                if (!string.Equals(pageReceipt.ChecksumAlgorithm, "SHA-256", StringComparison.OrdinalIgnoreCase))
                {
                    throw new DocumentIntelligenceServerException(
                        $"Artifact writer returned unsupported checksum algorithm '{pageReceipt.ChecksumAlgorithm}'. Expected SHA-256.");
                }

                if (!string.Equals(pageReceipt.ChecksumValue, expectedPageHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new DocumentIntelligenceServerException(
                        $"Artifact writer receipt checksum mismatch for page {page.PageNumber}. Expected {expectedPageHash}, but received {pageReceipt.ChecksumValue}.");
                }

                var pageArtifactId = Guid.NewGuid();
                pageArtifactMap[page.PageNumber] = pageArtifactId;

                artifacts.Add(new AnalysisArtifactOutputDto(
                    ArtifactId: pageArtifactId,
                    ArtifactKind: "OcrPageTranscript",
                    StorageReference: pageReceipt.StorageReference,
                    ContentType: "application/json",
                    ChecksumAlgorithm: pageReceipt.ChecksumAlgorithm,
                    ChecksumValue: pageReceipt.ChecksumValue
                ));

                if (page.Confidence.HasValue)
                {
                    warnings.Add($"Page {page.PageNumber}: OCR engine confidence score is {page.Confidence.Value:F1}% " +
                                 "(unverified machine observation metric, not legal fact confidence).");
                }
            }

            var fullText = fullTextBuilder.ToString();
            var fullBytes = Encoding.UTF8.GetBytes(fullText);
            var expectedFullHash = Convert.ToHexString(SHA256.HashData(fullBytes)).ToLowerInvariant();
            var proposedDocKey = $"analysis-artifacts/{request.DocumentVersionId:D}/document-transcript.txt";

            var docReceipt = await _artifactWriter.WriteAsync(
                proposedDocKey,
                "text/plain",
                fullBytes,
                cancellationToken);

            if (!string.Equals(docReceipt.ChecksumAlgorithm, "SHA-256", StringComparison.OrdinalIgnoreCase))
            {
                throw new DocumentIntelligenceServerException(
                    $"Artifact writer returned unsupported checksum algorithm '{docReceipt.ChecksumAlgorithm}'. Expected SHA-256.");
            }

            if (!string.Equals(docReceipt.ChecksumValue, expectedFullHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new DocumentIntelligenceServerException(
                    $"Artifact writer receipt checksum mismatch for document transcript. Expected {expectedFullHash}, but received {docReceipt.ChecksumValue}.");
            }

            var docArtifactId = Guid.NewGuid();
            artifacts.Insert(0, new AnalysisArtifactOutputDto(
                ArtifactId: docArtifactId,
                ArtifactKind: "OcrDocumentTranscript",
                StorageReference: docReceipt.StorageReference,
                ContentType: "text/plain",
                ChecksumAlgorithm: docReceipt.ChecksumAlgorithm,
                ChecksumValue: docReceipt.ChecksumValue
            ));

            // Persist raw semantic JSON response artifact
            var semanticBytes = JsonSerializer.SerializeToUtf8Bytes(analyze);
            var expectedSemanticHash = Convert.ToHexString(SHA256.HashData(semanticBytes)).ToLowerInvariant();
            var proposedSemanticKey = $"analysis-artifacts/{request.DocumentVersionId:D}/semantic-extraction.json";

            var semanticReceipt = await _artifactWriter.WriteAsync(
                proposedSemanticKey,
                "application/json",
                semanticBytes,
                cancellationToken);

            artifacts.Add(new AnalysisArtifactOutputDto(
                ArtifactId: Guid.NewGuid(),
                ArtifactKind: "SemanticRawResponse",
                StorageReference: semanticReceipt.StorageReference,
                ContentType: "application/json",
                ChecksumAlgorithm: semanticReceipt.ChecksumAlgorithm,
                ChecksumValue: semanticReceipt.ChecksumValue
            ));

            // Map candidate facts
            var candidateFacts = new List<ExtractedCandidateFactDto>();
            foreach (var fact in analyze.Facts)
            {
                Guid? evidenceArtifactId = null;
                int? pageNumber = null;
                string? excerpt = null;

                if (fact.Evidence != null && !string.IsNullOrWhiteSpace(fact.Evidence.Quote))
                {
                    pageNumber = fact.Evidence.PageNumber;
                    excerpt = fact.Evidence.Quote.Trim();
                    if (excerpt.Length > 500)
                    {
                        excerpt = excerpt.Substring(0, 500);
                    }

                    if (pageArtifactMap.TryGetValue(fact.Evidence.PageNumber, out var matchedPageId))
                    {
                        evidenceArtifactId = matchedPageId;
                    }
                    else
                    {
                        evidenceArtifactId = docArtifactId;
                    }
                }

                var normalizedValueKind = NormalizeValueKind(fact.ValueKind);
                var canonicalValue = NormalizeCanonicalValue(fact.CanonicalValue, normalizedValueKind);

                candidateFacts.Add(new ExtractedCandidateFactDto(
                    FactId: Guid.NewGuid(),
                    FactCode: fact.FieldCode,
                    ValueKind: normalizedValueKind,
                    CanonicalValue: canonicalValue,
                    ConfidenceScore: (decimal)Math.Clamp(fact.Confidence, 0.0, 1.0),
                    EvidenceArtifactId: evidenceArtifactId,
                    PageNumber: pageNumber,
                    Excerpt: excerpt
                ));

                if (!fact.IsEvidenceVerified)
                {
                    warnings.Add($"Fact '{fact.FieldCode}': Evidence quote was not verified against OCR text.");
                }
            }

            var resolvedProvider = !string.IsNullOrWhiteSpace(analyze.SemanticProvider)
                ? (analyze.SemanticProvider.Equals("gemini", StringComparison.OrdinalIgnoreCase) ? "FastApi-GeminiSemantic"
                   : analyze.SemanticProvider.Equals("qwen", StringComparison.OrdinalIgnoreCase) ? "FastApi-QwenSemantic"
                   : $"FastApi-{analyze.SemanticProvider}")
                : (analyze.ModelIdentifier?.Contains("gemini", StringComparison.OrdinalIgnoreCase) == true
                    ? "FastApi-GeminiSemantic"
                    : (request.SemanticProvider?.Contains("gemini", StringComparison.OrdinalIgnoreCase) == true
                        ? "FastApi-GeminiSemantic"
                        : "FastApi-QwenSemantic"));

            return new DocumentIntelligenceResult(
                Provider: resolvedProvider,
                ModelName: analyze.ModelIdentifier ?? (resolvedProvider.Contains("Gemini") ? "gemini-flash-latest" : "qwen3:1.7b"),
                ModelVersion: analyze.PromptVersion ?? "1.0.0",
                Outcome: "Succeeded",
                Artifacts: artifacts,
                Candidates: candidateFacts,
                Warnings: warnings.Count > 0 ? warnings : null
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DocumentIntelligenceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Failed to persist analysis artifacts for version {VersionId}",
                correlationId, request.DocumentVersionId);
            throw new DocumentIntelligenceServerException(
                "Failed to persist document analysis artifact to storage.",
                innerException: ex);
        }
    }
}
