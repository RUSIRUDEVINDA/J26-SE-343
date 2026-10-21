namespace StateLandGovernance.WorkflowGovernance.Presentation.Configuration;

using System;

/// <summary>
/// Configuration options for the OCR demo endpoint.
/// Reuses the DocumentIntelligence configuration section to keep upload limits consistent.
/// </summary>
public sealed class OcrDemoOptions
{
    public const string SectionName = "WorkflowGovernance:OcrDemo";

    /// <summary>
    /// Controls whether the temporary OCR demonstration endpoint is enabled.
    /// Defaults to false. Must only be enabled in Development/Testing environments.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Maximum allowed upload file size in bytes. Defaults to 25 MB.
    /// </summary>
    public long MaxUploadBytes { get; set; } = 25 * 1024 * 1024;

    public void Validate()
    {
        if (MaxUploadBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxUploadBytes), "MaxUploadBytes must be positive.");
        }
    }
}
