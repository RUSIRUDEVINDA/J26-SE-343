namespace StateLandGovernance.WorkflowGovernance.Application.Exceptions;

using System;

public sealed class DocumentAnalysisNotFoundException : Exception
{
    public Guid? DocumentAnalysisId { get; }
    public Guid? DocumentVersionId { get; }

    public DocumentAnalysisNotFoundException(Guid documentAnalysisId)
        : base($"DocumentAnalysis '{documentAnalysisId}' was not found.")
    {
        DocumentAnalysisId = documentAnalysisId;
    }

    public DocumentAnalysisNotFoundException(string message, Guid? documentVersionId = null)
        : base(message)
    {
        DocumentVersionId = documentVersionId;
    }

    public static DocumentAnalysisNotFoundException ForVersion(Guid documentVersionId)
        => new DocumentAnalysisNotFoundException($"DocumentAnalysis for DocumentVersion '{documentVersionId}' was not found.", documentVersionId);
}
