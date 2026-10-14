namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct DocumentAnalysisId
{
    public Guid Value { get; }
    
    public DocumentAnalysisId(Guid value)
    {
        if (value == Guid.Empty) throw new InvalidDocumentAnalysisException("DocumentAnalysisId cannot be empty.");
        Value = value;
    }
    
    public override string ToString() => Value.ToString();
}
