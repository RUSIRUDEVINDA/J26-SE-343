namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class AnalysisRun
{
    public AnalysisRunId Id { get; }
    public int RunNumber { get; }
    public DocumentVersionId DocumentVersionId { get; }
    public DocumentChecksum DocumentChecksum { get; }
    public AnalysisModelReference ModelReference { get; }
    
    private readonly List<AnalysisCapabilityCode> _requestedCapabilities;
    public IReadOnlyCollection<AnalysisCapabilityCode> RequestedCapabilities => _requestedCapabilities.AsReadOnly();
    
    public DateTime RequestedAt { get; }
    public AnalysisRunState State { get; private set; }

    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public AnalysisRunResult? Result { get; private set; }
    public DateTime? FailedAt { get; private set; }
    public DateTime? SupersededAt { get; private set; }
    public AnalysisRunFailure? Failure { get; private set; }
    public string? SupersessionReason { get; private set; }
    
    internal AnalysisRun(
        AnalysisRunId id,
        int runNumber,
        DocumentVersionId documentVersionId,
        DocumentChecksum documentChecksum,
        AnalysisModelReference modelReference,
        IReadOnlyCollection<AnalysisCapabilityCode> requestedCapabilities,
        DateTime requestedAt)
    {
        Id = id;
        RunNumber = runNumber;
        DocumentVersionId = documentVersionId;
        DocumentChecksum = documentChecksum;
        ModelReference = modelReference;
        _requestedCapabilities = new List<AnalysisCapabilityCode>(requestedCapabilities);
        RequestedAt = requestedAt;
        State = AnalysisRunState.Requested;
    }

    internal void MarkStarted(DateTime startedAt)
    {
        if (State != AnalysisRunState.Requested) throw new InvalidAnalysisRunTransitionException($"Cannot start a run from {State} state.");
        if (startedAt.Kind != DateTimeKind.Utc) throw new InvalidAnalysisRunTransitionException("StartedAt must be UTC.");
        if (startedAt < RequestedAt) throw new InvalidAnalysisRunTransitionException("StartedAt cannot be before RequestedAt.");

        StartedAt = startedAt;
        State = AnalysisRunState.Running;
    }

    internal void MarkCompleted(AnalysisRunResult result, DateTime completedAt)
    {
        if (State != AnalysisRunState.Running) throw new InvalidAnalysisRunTransitionException($"Cannot complete a run from {State} state.");
        if (result == null) throw new InvalidAnalysisRunTransitionException("Result cannot be null.");
        if (completedAt.Kind != DateTimeKind.Utc) throw new InvalidAnalysisRunTransitionException("CompletedAt must be UTC.");
        if (StartedAt.HasValue && completedAt < StartedAt.Value) throw new InvalidAnalysisRunTransitionException("CompletedAt cannot be before StartedAt.");

        Result = result;
        CompletedAt = completedAt;
        State = AnalysisRunState.Completed;
    }

    internal void MarkFailed(AnalysisRunFailure failure, DateTime failedAt)
    {
        if (State == AnalysisRunState.Failed || State == AnalysisRunState.Superseded || State == AnalysisRunState.Completed)
            throw new InvalidAnalysisRunTransitionException($"Cannot fail a run from {State} state.");
        
        if (failure == null) throw new InvalidAnalysisRunTransitionException("Failure cannot be null.");
        if (failedAt.Kind != DateTimeKind.Utc) throw new InvalidAnalysisRunTransitionException("FailedAt must be UTC.");
        if (failedAt < RequestedAt) throw new InvalidAnalysisRunTransitionException("FailedAt cannot be before RequestedAt.");
        if (StartedAt.HasValue && failedAt < StartedAt.Value) throw new InvalidAnalysisRunTransitionException("FailedAt cannot be before StartedAt.");

        Failure = failure;
        FailedAt = failedAt;
        State = AnalysisRunState.Failed;
    }

    internal void MarkSuperseded(string supersessionReason, DateTime supersededAt)
    {
        if (State == AnalysisRunState.Failed || State == AnalysisRunState.Superseded || State == AnalysisRunState.Completed)
            throw new InvalidAnalysisRunTransitionException($"Cannot supersede a run from {State} state.");
        
        if (string.IsNullOrWhiteSpace(supersessionReason)) throw new InvalidAnalysisRunTransitionException("Supersession reason cannot be blank.");
        
        var trimmedReason = supersessionReason.Trim();
        if (trimmedReason.Length > 500) throw new InvalidAnalysisRunTransitionException("Supersession reason exceeds maximum length of 500.");
        
        foreach (char c in trimmedReason)
        {
            if (char.IsControl(c)) throw new InvalidAnalysisRunTransitionException("Supersession reason contains control characters.");
        }

        if (supersededAt.Kind != DateTimeKind.Utc) throw new InvalidAnalysisRunTransitionException("SupersededAt must be UTC.");
        if (supersededAt < RequestedAt) throw new InvalidAnalysisRunTransitionException("SupersededAt cannot be before RequestedAt.");
        if (StartedAt.HasValue && supersededAt < StartedAt.Value) throw new InvalidAnalysisRunTransitionException("SupersededAt cannot be before StartedAt.");

        SupersessionReason = trimmedReason;
        SupersededAt = supersededAt;
        State = AnalysisRunState.Superseded;
    }
}
