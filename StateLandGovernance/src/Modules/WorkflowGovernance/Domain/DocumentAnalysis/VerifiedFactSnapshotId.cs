namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct VerifiedFactSnapshotId
{
    public Guid Value { get; }

    public VerifiedFactSnapshotId(Guid value)
    {
        if (value == Guid.Empty) throw new InvalidVerifiedFactSnapshotException("Snapshot ID cannot be empty.");
        Value = value;
    }
}
