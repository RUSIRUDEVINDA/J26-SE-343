namespace StateLandGovernance.UnitTests.WorkflowGovernance.DocumentAnalysisTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis.Events;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using Xunit;

public class AnalysisRunLifecycleTests
{
    private readonly DocumentAnalysisId _analysisId = new(Guid.NewGuid());
    private readonly GovernedDocumentId _docId = new(Guid.NewGuid());
    private readonly DocumentVersionId _verId = new(Guid.NewGuid());
    private readonly DocumentChecksum _checksum = new("SHA256", "checksumvalue123");
    private readonly AnalysisRunId _runId = new(Guid.NewGuid());
    private readonly AnalysisModelReference _modelRef = new("Provider", "Model", "v1");
    private readonly List<AnalysisCapabilityCode> _caps = new() { new AnalysisCapabilityCode("Cap1") };
    private readonly DateTime _requestedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
    
    private DocumentAnalysis CreateAggregateWithRun()
    {
        var aggregate = new DocumentAnalysis(_analysisId, _docId, _verId, _checksum, 1, 1, _requestedAt.AddDays(-1));
        aggregate.RequestRun(_runId, _modelRef, _caps, _requestedAt);
        // clear initial events to isolate lifecycle events
        var field = typeof(DocumentAnalysis).GetField("_domainEvents", BindingFlags.NonPublic | BindingFlags.Instance);
        var list = (List<StateLandGovernance.BuildingBlocks.Events.IDomainEvent>)field!.GetValue(aggregate)!;
        list.Clear();
        return aggregate;
    }

    // --- START RUN ---
    [Fact]
    public void StartRun_ValidRequest_TransitionsToRunning_AndRaisesEvent()
    {
        var aggregate = CreateAggregateWithRun();
        var startedAt = _requestedAt.AddMinutes(5);
        var initialRevision = aggregate.Revision;

        aggregate.StartRun(_runId, startedAt);

        var run = aggregate.Runs.Single();
        Assert.Equal(AnalysisRunState.Running, run.State);
        Assert.Equal(startedAt, run.StartedAt);
        Assert.Equal(initialRevision + 1, aggregate.Revision);

        var evt = aggregate.DomainEvents.OfType<AnalysisRunStarted>().Single();
        Assert.Equal(_runId, evt.AnalysisRunId);
        Assert.Equal(startedAt, evt.OccurredOn);
        Assert.Equal(aggregate.Revision, evt.DocumentAnalysisRevision);
        Assert.Equal(run.RunNumber, evt.RunNumber);
        Assert.Equal(_docId, evt.GovernedDocumentId);
        Assert.Equal(_verId, evt.DocumentVersionId);
        Assert.Equal(_checksum.Algorithm, evt.ChecksumAlgorithm);
        Assert.Equal(_checksum.Value, evt.ChecksumValue);
    }

    [Fact]
    public void StartRun_RunNotFound_ThrowsAnalysisRunNotFoundException()
    {
        var aggregate = CreateAggregateWithRun();
        Assert.Throws<AnalysisRunNotFoundException>(() => aggregate.StartRun(new AnalysisRunId(Guid.NewGuid()), _requestedAt.AddMinutes(5)));
    }

    [Fact]
    public void StartRun_AlreadyRunning_ThrowsInvalidAnalysisRunTransitionException()
    {
        var aggregate = CreateAggregateWithRun();
        aggregate.StartRun(_runId, _requestedAt.AddMinutes(5));
        Assert.Throws<InvalidAnalysisRunTransitionException>(() => aggregate.StartRun(_runId, _requestedAt.AddMinutes(10)));
    }

    [Fact]
    public void StartRun_FromTerminalState_ThrowsInvalidAnalysisRunTransitionException()
    {
        var aggregate = CreateAggregateWithRun();
        var failure = new AnalysisRunFailure("ERR01", "Failed");
        aggregate.FailRun(_runId, failure, _requestedAt.AddMinutes(5));
        
        Assert.Throws<InvalidAnalysisRunTransitionException>(() => aggregate.StartRun(_runId, _requestedAt.AddMinutes(10)));
    }

    [Fact]
    public void StartRun_StartedAtEqualRequestedAt_IsAccepted()
    {
        var aggregate = CreateAggregateWithRun();
        aggregate.StartRun(_runId, _requestedAt);
        Assert.Equal(AnalysisRunState.Running, aggregate.Runs.Single().State);
    }

    [Fact]
    public void StartRun_StartedAtNotUtc_ThrowsInvalidAnalysisRunTransitionException()
    {
        var aggregate = CreateAggregateWithRun();
        Assert.Throws<InvalidAnalysisRunTransitionException>(() => aggregate.StartRun(_runId, new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Local)));
    }

    [Fact]
    public void StartRun_StartedAtBeforeRequestedAt_ThrowsInvalidAnalysisRunTransitionException()
    {
        var aggregate = CreateAggregateWithRun();
        Assert.Throws<InvalidAnalysisRunTransitionException>(() => aggregate.StartRun(_runId, _requestedAt.AddMinutes(-5)));
    }

    [Fact]
    public void StartRun_Failure_LeavesStateRevisionAndEventsUnchanged()
    {
        var aggregate = CreateAggregateWithRun();
        var initialRevision = aggregate.Revision;

        Assert.Throws<InvalidAnalysisRunTransitionException>(() => aggregate.StartRun(_runId, _requestedAt.AddMinutes(-5)));

        Assert.Equal(AnalysisRunState.Requested, aggregate.Runs.Single().State);
        Assert.Equal(initialRevision, aggregate.Revision);
        Assert.Empty(aggregate.DomainEvents);
    }

    // --- FAIL RUN ---
    [Fact]
    public void FailRun_FromRequested_TransitionsToFailed_AndRaisesEvent()
    {
        var aggregate = CreateAggregateWithRun();
        var failedAt = _requestedAt.AddMinutes(5);
        var failure = new AnalysisRunFailure("ERR1", "Desc1");
        var initialRevision = aggregate.Revision;

        aggregate.FailRun(_runId, failure, failedAt);

        var run = aggregate.Runs.Single();
        Assert.Equal(AnalysisRunState.Failed, run.State);
        Assert.Equal(failedAt, run.FailedAt);
        Assert.Equal(failure, run.Failure);
        Assert.Equal(initialRevision + 1, aggregate.Revision);

        var evt = aggregate.DomainEvents.OfType<AnalysisRunFailed>().Single();
        Assert.Equal(_runId, evt.AnalysisRunId);
        Assert.Equal(failedAt, evt.OccurredOn);
        Assert.Equal("ERR1", evt.FailureCode);
        Assert.Equal("Desc1", evt.FailureDescription);
        Assert.Equal(aggregate.Revision, evt.DocumentAnalysisRevision);
    }

    [Fact]
    public void FailRun_FromRunning_TransitionsToFailed()
    {
        var aggregate = CreateAggregateWithRun();
        aggregate.StartRun(_runId, _requestedAt.AddMinutes(5));
        
        var failure = new AnalysisRunFailure("ERR2", "Desc2");
        aggregate.FailRun(_runId, failure, _requestedAt.AddMinutes(10));

        var run = aggregate.Runs.Single();
        Assert.Equal(AnalysisRunState.Failed, run.State);
        Assert.Equal(_requestedAt.AddMinutes(10), run.FailedAt);
        Assert.Equal(failure, run.Failure);
    }

    [Fact]
    public void FailRun_FailedAtEqualRequestedAt_AcceptedFromRequested()
    {
        var aggregate = CreateAggregateWithRun();
        var failure = new AnalysisRunFailure("ERR", "Desc");
        aggregate.FailRun(_runId, failure, _requestedAt);
        Assert.Equal(AnalysisRunState.Failed, aggregate.Runs.Single().State);
    }

    [Fact]
    public void FailRun_FailedAtEqualStartedAt_AcceptedFromRunning()
    {
        var aggregate = CreateAggregateWithRun();
        var startedAt = _requestedAt.AddMinutes(5);
        aggregate.StartRun(_runId, startedAt);
        
        var failure = new AnalysisRunFailure("ERR", "Desc");
        aggregate.FailRun(_runId, failure, startedAt);
        Assert.Equal(AnalysisRunState.Failed, aggregate.Runs.Single().State);
    }

    [Fact]
    public void FailRun_NullFailure_ThrowsInvalidAnalysisRunTransitionException()
    {
        var aggregate = CreateAggregateWithRun();
        Assert.Throws<InvalidAnalysisRunTransitionException>(() => aggregate.FailRun(_runId, null!, _requestedAt.AddMinutes(5)));
    }

    [Fact]
    public void FailRun_AlreadyFailed_ThrowsInvalidAnalysisRunTransitionException()
    {
        var aggregate = CreateAggregateWithRun();
        var failure = new AnalysisRunFailure("ERR", "Desc");
        aggregate.FailRun(_runId, failure, _requestedAt.AddMinutes(5));

        Assert.Throws<InvalidAnalysisRunTransitionException>(() => aggregate.FailRun(_runId, failure, _requestedAt.AddMinutes(10)));
    }

    [Fact]
    public void FailRun_Superseded_ThrowsInvalidAnalysisRunTransitionException()
    {
        var aggregate = CreateAggregateWithRun();
        aggregate.SupersedeRun(_runId, "Reason", _requestedAt.AddMinutes(5));

        Assert.Throws<InvalidAnalysisRunTransitionException>(() => aggregate.FailRun(_runId, new AnalysisRunFailure("ERR", "Desc"), _requestedAt.AddMinutes(10)));
    }

    [Fact]
    public void FailRun_BeforeRequestedAt_ThrowsInvalidAnalysisRunTransitionException()
    {
        var aggregate = CreateAggregateWithRun();
        Assert.Throws<InvalidAnalysisRunTransitionException>(() => aggregate.FailRun(_runId, new AnalysisRunFailure("ERR", "Desc"), _requestedAt.AddMinutes(-5)));
    }

    [Fact]
    public void FailRun_BeforeStartedAt_ThrowsInvalidAnalysisRunTransitionException()
    {
        var aggregate = CreateAggregateWithRun();
        var startedAt = _requestedAt.AddMinutes(10);
        aggregate.StartRun(_runId, startedAt);

        Assert.Throws<InvalidAnalysisRunTransitionException>(() => aggregate.FailRun(_runId, new AnalysisRunFailure("ERR", "Desc"), _requestedAt.AddMinutes(5)));
    }

    // --- SUPERSEDE RUN ---
    [Fact]
    public void SupersedeRun_FromRequested_TransitionsToSuperseded_AndRaisesEvent()
    {
        var aggregate = CreateAggregateWithRun();
        var supersededAt = _requestedAt.AddMinutes(5);
        var initialRevision = aggregate.Revision;

        aggregate.SupersedeRun(_runId, "  Reason trimmed  ", supersededAt);

        var run = aggregate.Runs.Single();
        Assert.Equal(AnalysisRunState.Superseded, run.State);
        Assert.Equal(supersededAt, run.SupersededAt);
        Assert.Equal("Reason trimmed", run.SupersessionReason);
        Assert.Equal(initialRevision + 1, aggregate.Revision);

        var evt = aggregate.DomainEvents.OfType<AnalysisRunSuperseded>().Single();
        Assert.Equal(_runId, evt.AnalysisRunId);
        Assert.Equal(supersededAt, evt.OccurredOn);
        Assert.Equal("Reason trimmed", evt.SupersessionReason);
    }

    [Fact]
    public void SupersedeRun_FromRunning_TransitionsToSuperseded()
    {
        var aggregate = CreateAggregateWithRun();
        aggregate.StartRun(_runId, _requestedAt.AddMinutes(5));
        
        aggregate.SupersedeRun(_runId, "Reason", _requestedAt.AddMinutes(10));

        var run = aggregate.Runs.Single();
        Assert.Equal(AnalysisRunState.Superseded, run.State);
        Assert.Equal(_requestedAt.AddMinutes(10), run.SupersededAt);
    }

    [Fact]
    public void SupersedeRun_BlankReason_ThrowsInvalidAnalysisRunTransitionException()
    {
        var aggregate = CreateAggregateWithRun();
        Assert.Throws<InvalidAnalysisRunTransitionException>(() => aggregate.SupersedeRun(_runId, "   ", _requestedAt.AddMinutes(5)));
    }

    [Fact]
    public void SupersedeRun_BeforeStartedAt_ThrowsInvalidAnalysisRunTransitionException()
    {
        var aggregate = CreateAggregateWithRun();
        aggregate.StartRun(_runId, _requestedAt.AddMinutes(10));
        Assert.Throws<InvalidAnalysisRunTransitionException>(() => aggregate.SupersedeRun(_runId, "Reason", _requestedAt.AddMinutes(5)));
    }

    [Fact]
    public void SupersedeRun_AlreadyFailed_ThrowsInvalidAnalysisRunTransitionException()
    {
        var aggregate = CreateAggregateWithRun();
        aggregate.FailRun(_runId, new AnalysisRunFailure("ERR", "Desc"), _requestedAt.AddMinutes(5));
        Assert.Throws<InvalidAnalysisRunTransitionException>(() => aggregate.SupersedeRun(_runId, "Reason", _requestedAt.AddMinutes(10)));
    }

    // --- STRUCTURAL ---
    [Fact]
    public void AnalysisRunFailure_CannotBypassValidationWithInit()
    {
        var props = typeof(AnalysisRunFailure).GetProperties();
        foreach (var p in props)
        {
            Assert.False(p.CanWrite);
            var setMethod = p.GetSetMethod(nonPublic: true);
            if (setMethod != null)
            {
                var isInitOnly = setMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));
                Assert.False(isInitOnly);
            }
        }
    }

    [Fact]
    public void AnalysisRunFailure_Equality()
    {
        var f1 = new AnalysisRunFailure("A", "B");
        var f2 = new AnalysisRunFailure("A", "B");
        var f3 = new AnalysisRunFailure("X", "B");

        Assert.Equal(f1, f2);
        Assert.NotEqual(f1, f3);
        Assert.Equal(f1.GetHashCode(), f2.GetHashCode());
    }

    [Fact]
    public void CompleteRun_HasExactSignature_AndDoesNotAcceptBindingArguments()
    {
        var method = typeof(DocumentAnalysis).GetMethod("CompleteRun");
        Assert.NotNull(method);

        var parameters = method.GetParameters();
        
        Assert.DoesNotContain(parameters, p => p.ParameterType == typeof(GovernedDocumentId));
        Assert.DoesNotContain(parameters, p => p.ParameterType == typeof(DocumentVersionId));
        Assert.DoesNotContain(parameters, p => p.ParameterType == typeof(DocumentChecksum));
        Assert.DoesNotContain(parameters, p => p.ParameterType == typeof(AnalysisModelReference));

        Assert.Equal(6, parameters.Length);
        Assert.Equal(typeof(AnalysisRunId), parameters[0].ParameterType);
        Assert.Equal(typeof(AnalysisRunResultId), parameters[1].ParameterType);
        Assert.Equal(typeof(AnalysisResultOutcome), parameters[2].ParameterType);
        Assert.Equal(typeof(IReadOnlyCollection<AnalysisResultArtifactReference>), parameters[3].ParameterType);
        Assert.Equal(typeof(IReadOnlyCollection<ExtractedFactInput>), parameters[4].ParameterType);
        Assert.Equal(typeof(DateTime), parameters[5].ParameterType);
    }

    [Fact]
    public void AnalysisRunState_ContainsExactExpectedValues()
    {
        var names = Enum.GetNames(typeof(AnalysisRunState));
        Assert.Equal(5, names.Length);
        Assert.Contains("Requested", names);
        Assert.Contains("Running", names);
        Assert.Contains("Completed", names);
        Assert.Contains("Failed", names);
        Assert.Contains("Superseded", names);
    }

    [Fact]
    public void AnalysisRun_InternalTransitionMethods_AreNotPublic()
    {
        var methods = typeof(AnalysisRun).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.DoesNotContain(methods, m => m.Name.StartsWith("Mark"));
    }

    [Fact]
    public void AnalysisRunFailure_OversizedDescription_Throws()
    {
        var oversized = new string('A', 501);
        Assert.Throws<InvalidAnalysisRunTransitionException>(() => new AnalysisRunFailure("ERR", oversized));
    }
}
