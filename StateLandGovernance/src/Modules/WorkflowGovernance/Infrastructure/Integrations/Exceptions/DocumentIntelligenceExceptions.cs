namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.Exceptions;

using System;

/// <summary>
/// Base technical exception for external document intelligence service communication failures.
/// Does not leak raw internal file paths or server-side credentials.
/// </summary>
public class DocumentIntelligenceException : Exception
{
    public int? StatusCode { get; }
    public string? ErrorCode { get; }

    public DocumentIntelligenceException(string message, int? statusCode = null, string? errorCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Thrown when document content is corrupted, empty, or fails schema validation (HTTP 400).
/// </summary>
public sealed class DocumentIntelligenceValidationException : DocumentIntelligenceException
{
    public DocumentIntelligenceValidationException(string message, string? errorCode = null, Exception? innerException = null)
        : base(message, 400, errorCode, innerException)
    {
    }
}

/// <summary>
/// Thrown when uploaded payload exceeds the server-side size limit (HTTP 413).
/// </summary>
public sealed class DocumentIntelligencePayloadTooLargeException : DocumentIntelligenceException
{
    public DocumentIntelligencePayloadTooLargeException(string message, string? errorCode = null, Exception? innerException = null)
        : base(message, 413, errorCode, innerException)
    {
    }
}

/// <summary>
/// Thrown when document media type or magic byte signature is not supported (HTTP 415).
/// </summary>
public sealed class DocumentIntelligenceUnsupportedMediaException : DocumentIntelligenceException
{
    public DocumentIntelligenceUnsupportedMediaException(string message, string? errorCode = null, Exception? innerException = null)
        : base(message, 415, errorCode, innerException)
    {
    }
}

/// <summary>
/// Thrown when requested language mode is invalid or required language pack is missing (HTTP 422).
/// Automatic fallback is disabled to prevent silent transcription corruption.
/// </summary>
public sealed class DocumentIntelligenceLanguageUnavailableException : DocumentIntelligenceException
{
    public DocumentIntelligenceLanguageUnavailableException(string message, string? errorCode = null, Exception? innerException = null)
        : base(message, 422, errorCode, innerException)
    {
    }
}

/// <summary>
/// Thrown when Tesseract or the document intelligence runtime is degraded/unavailable on the host (HTTP 503).
/// </summary>
public sealed class DocumentIntelligenceServiceUnavailableException : DocumentIntelligenceException
{
    public DocumentIntelligenceServiceUnavailableException(string message, string? errorCode = null, Exception? innerException = null)
        : base(message, 503, errorCode, innerException)
    {
    }
}

/// <summary>
/// Thrown when an unexpected internal failure occurs within the document intelligence service (HTTP 500 / 5xx).
/// </summary>
public sealed class DocumentIntelligenceServerException : DocumentIntelligenceException
{
    public DocumentIntelligenceServerException(string message, int statusCode = 500, string? errorCode = null, Exception? innerException = null)
        : base(message, statusCode, errorCode, innerException)
    {
    }
}

/// <summary>
/// Thrown when an HTTP request to the document intelligence service times out.
/// </summary>
public sealed class DocumentIntelligenceTimeoutException : DocumentIntelligenceException
{
    public DocumentIntelligenceTimeoutException(string message, Exception? innerException = null)
        : base(message, null, "REQUEST_TIMEOUT", innerException)
    {
    }
}
