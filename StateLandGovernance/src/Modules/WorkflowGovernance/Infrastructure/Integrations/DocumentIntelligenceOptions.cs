namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;

using System;

/// <summary>
/// Configuration options for connecting to the external Python Document Intelligence FastAPI service.
/// </summary>
public sealed class DocumentIntelligenceOptions
{
    public const string SectionName = "WorkflowGovernance:DocumentIntelligence";

    /// <summary>
    /// Base URL of the FastAPI Document Intelligence service (e.g. http://127.0.0.1:8000).
    /// Must be an absolute HTTP or HTTPS URL.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// HTTP request timeout in seconds. Default is 30 seconds to accommodate multi-page documents.
    /// Must be greater than 0.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Default language mode for OCR extraction: 'eng', 'sin', or 'sin+eng'.
    /// Default is 'sin+eng' for bilingual state-land documents.
    /// </summary>
    public string DefaultLanguageMode { get; set; } = "sin+eng";

    /// <summary>
    /// Whether to apply deterministic OpenCV contrast/denoise/deskew preprocessing.
    /// </summary>
    public bool Preprocess { get; set; } = true;

    /// <summary>
    /// Maximum allowed upload bytes for document analysis. Default is 25 MB (26,214,400 bytes).
    /// Must be greater than 0. Kept aligned with external FastAPI OCR upload limits.
    /// </summary>
    public long MaxUploadBytes { get; set; } = 25 * 1024 * 1024;

    /// <summary>
    /// Validates configuration values and throws ArgumentException if invalid.
    /// Prevents silent fallback or misconfiguration.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            throw new ArgumentException(
                "DocumentIntelligence:BaseUrl is required and must not be blank.",
                nameof(BaseUrl));
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                $"DocumentIntelligence:BaseUrl '{BaseUrl}' is not a valid absolute HTTP or HTTPS URL.",
                nameof(BaseUrl));
        }

        if (RequestTimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RequestTimeoutSeconds),
                RequestTimeoutSeconds,
                "DocumentIntelligence:RequestTimeoutSeconds must be greater than 0.");
        }

        if (MaxUploadBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxUploadBytes),
                MaxUploadBytes,
                "DocumentIntelligence:MaxUploadBytes must be greater than 0.");
        }

        if (DefaultLanguageMode is not ("eng" or "sin" or "sin+eng"))
        {
            throw new ArgumentException(
                $"DocumentIntelligence:DefaultLanguageMode '{DefaultLanguageMode}' is invalid. Must be 'eng', 'sin', or 'sin+eng'.",
                nameof(DefaultLanguageMode));
        }
    }
}
