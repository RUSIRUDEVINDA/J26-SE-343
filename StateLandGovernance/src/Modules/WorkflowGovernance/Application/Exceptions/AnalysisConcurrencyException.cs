namespace StateLandGovernance.WorkflowGovernance.Application.Exceptions;

using System;

public sealed class AnalysisConcurrencyException : Exception
{
    public Guid DocumentAnalysisId { get; }
    public int ExpectedRevision { get; }
    public int ActualRevision { get; }

    public AnalysisConcurrencyException(Guid documentAnalysisId, int expectedRevision, int actualRevision)
        : base($"DocumentAnalysis '{documentAnalysisId}' revision mismatch. Expected {expectedRevision} but found {actualRevision}.")
    {
        DocumentAnalysisId = documentAnalysisId;
        ExpectedRevision = expectedRevision;
        ActualRevision = actualRevision;
    }
}
