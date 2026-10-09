namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Storage;

using System;

/// <summary>
/// Configuration options for local filesystem development and demonstration storage.
/// </summary>
public sealed class LocalFileStorageOptions
{
    public const string SectionName = "WorkflowGovernance:LocalStorage";

    /// <summary>
    /// Root directory path for storing local demo document bytes and analysis artifacts.
    /// Defaults to a repository-local runtime path.
    /// </summary>
    public string RootPath { get; set; } = ".local-data/workflow-governance";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RootPath))
        {
            throw new ArgumentException("LocalStorage RootPath must not be null or whitespace.", nameof(RootPath));
        }
    }
}
