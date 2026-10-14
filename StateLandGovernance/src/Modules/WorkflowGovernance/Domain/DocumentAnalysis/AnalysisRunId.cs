namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct AnalysisRunId
{
    public Guid Value { get; }

    public AnalysisRunId(Guid value)
    {
        if (value == Guid.Empty) throw new InvalidAnalysisRunException("AnalysisRunId cannot be empty.");
        Value = value;
    }

    public override string ToString() => Value.ToString();
}
