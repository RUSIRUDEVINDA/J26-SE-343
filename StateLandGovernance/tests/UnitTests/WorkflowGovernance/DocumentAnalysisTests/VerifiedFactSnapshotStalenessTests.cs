namespace StateLandGovernance.UnitTests.WorkflowGovernance.DocumentAnalysisTests;

using System;
using System.Linq;
using Xunit;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

public class VerifiedFactSnapshotStalenessTests
{
    private readonly DocumentAnalysis _analysis;
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly DateTime _utcNow = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly AnalysisRunId _runId = new AnalysisRunId(Guid.NewGuid());
    private readonly AnalysisRunResultId _resultId = new AnalysisRunResultId(Guid.NewGuid());
    private readonly VerifiedAuthoritySnapshot _authority;

    public VerifiedFactSnapshotStalenessTests()
    {
        _analysis = new DocumentAnalysis(
            new DocumentAnalysisId(Guid.NewGuid()),
            new GovernedDocumentId(Guid.NewGuid()),
            new DocumentVersionId(Guid.NewGuid()),
            new DocumentChecksum("SHA256", "hash"),
            1, 1, _utcNow.AddDays(-10));

        _analysis.RequestRun(_runId, new AnalysisModelReference("p", "m", "v"), new[] { new AnalysisCapabilityCode("cap") }, _utcNow.AddDays(-9));
        _analysis.StartRun(_runId, _utcNow.AddDays(-8));
        _analysis.CompleteRun(_runId, _resultId, AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow.AddDays(-7));

        _authority = new VerifiedAuthoritySnapshot(_actorId, new[] { "FactSnapshotPublisher" }, new AuthorityScope(AuthorityScopeKind.GovernedDocument, _analysis.GovernedDocumentId.Value.ToString("D")), _utcNow.AddDays(-20), _utcNow.AddDays(-10), _utcNow.AddDays(20));
    }

    [Fact]
    public void PublishSnapshot_NewerRunningRun_DoesNotStale()
    {
        var runId2 = new AnalysisRunId(Guid.NewGuid());
        _analysis.RequestRun(runId2, new AnalysisModelReference("p", "m", "v"), new[] { new AnalysisCapabilityCode("cap") }, _utcNow.AddDays(-6));
        _analysis.StartRun(runId2, _utcNow.AddDays(-5));
        
        _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, _authority);
        Assert.Single(_analysis.VerifiedFactSnapshots);
    }

    [Fact]
    public void PublishSnapshot_NewerFailedRun_DoesNotStale()
    {
        var runId2 = new AnalysisRunId(Guid.NewGuid());
        _analysis.RequestRun(runId2, new AnalysisModelReference("p", "m", "v"), new[] { new AnalysisCapabilityCode("cap") }, _utcNow.AddDays(-6));
        _analysis.StartRun(runId2, _utcNow.AddDays(-5));
        _analysis.FailRun(runId2, new AnalysisRunFailure("err", "error"), _utcNow.AddDays(-4));

        _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, _authority);
        Assert.Single(_analysis.VerifiedFactSnapshots);
    }

    [Fact]
    public void PublishSnapshot_NewerSupersededRun_DoesNotStale()
    {
        var runId2 = new AnalysisRunId(Guid.NewGuid());
        _analysis.RequestRun(runId2, new AnalysisModelReference("p", "m", "v"), new[] { new AnalysisCapabilityCode("cap") }, _utcNow.AddDays(-6));
        _analysis.SupersedeRun(runId2, "super", _utcNow.AddDays(-5));

        _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, _authority);
        Assert.Single(_analysis.VerifiedFactSnapshots);
    }

    [Fact]
    public void PublishSnapshot_EqualCompletionTimestamps_StillUseRunNumber()
    {
        var runId2 = new AnalysisRunId(Guid.NewGuid());
        _analysis.RequestRun(runId2, new AnalysisModelReference("p", "m", "v"), new[] { new AnalysisCapabilityCode("cap") }, _utcNow.AddDays(-9));
        _analysis.StartRun(runId2, _utcNow.AddDays(-8));
        _analysis.CompleteRun(runId2, new AnalysisRunResultId(Guid.NewGuid()), AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow.AddDays(-7)); // SAME TIMESTAMP

        Assert.Throws<StaleAnalysisResultException>(() => _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, _authority));
    }

    [Fact]
    public void PublishSnapshot_NewerCompletedRun_Stales()
    {
        var runId2 = new AnalysisRunId(Guid.NewGuid());
        _analysis.RequestRun(runId2, new AnalysisModelReference("p", "m", "v"), new[] { new AnalysisCapabilityCode("cap") }, _utcNow.AddDays(-6));
        _analysis.StartRun(runId2, _utcNow.AddDays(-5));
        _analysis.CompleteRun(runId2, new AnalysisRunResultId(Guid.NewGuid()), AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow.AddDays(-4));

        Assert.Throws<StaleAnalysisResultException>(() => _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, _authority));
    }

    [Fact]
    public void PublishSnapshot_NewerRequestedRun_DoesNotStale()
    {
        var runId2 = new AnalysisRunId(Guid.NewGuid());
        _analysis.RequestRun(runId2, new AnalysisModelReference("p", "m", "v"), new[] { new AnalysisCapabilityCode("cap") }, _utcNow.AddDays(-6));

        _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, _authority);
        Assert.Single(_analysis.VerifiedFactSnapshots);
    }

    [Fact]
    public void StaleResultAtomicity_LeavesStateUnchanged()
    {
        var runId2 = new AnalysisRunId(Guid.NewGuid());
        _analysis.RequestRun(runId2, new AnalysisModelReference("p", "m", "v"), new[] { new AnalysisCapabilityCode("cap") }, _utcNow.AddDays(-6));
        _analysis.StartRun(runId2, _utcNow.AddDays(-5));
        _analysis.CompleteRun(runId2, new AnalysisRunResultId(Guid.NewGuid()), AnalysisResultOutcome.NoFindings, Array.Empty<AnalysisResultArtifactReference>(), Array.Empty<ExtractedFactInput>(), _utcNow.AddDays(-4));

        var rev = _analysis.Revision;
        var evtsCount = _analysis.DomainEvents.Count;

        Assert.Throws<StaleAnalysisResultException>(() => _analysis.PublishVerifiedFactSnapshot(new VerifiedFactSnapshotId(Guid.NewGuid()), _resultId, _actorId, _utcNow, _authority));

        Assert.Empty(_analysis.VerifiedFactSnapshots);
        Assert.Equal(rev, _analysis.Revision);
        Assert.Equal(evtsCount, _analysis.DomainEvents.Count);
    }
}
