namespace StateLandGovernance.WorkflowGovernance.Application.Exceptions;

using System;

public sealed class FactCandidateNotFoundException : Exception
{
    public Guid ExtractedFactId { get; }

    public FactCandidateNotFoundException(Guid extractedFactId)
        : base($"Extracted fact candidate '{extractedFactId}' was not found in the target analysis run result.")
    {
        ExtractedFactId = extractedFactId;
    }
}
