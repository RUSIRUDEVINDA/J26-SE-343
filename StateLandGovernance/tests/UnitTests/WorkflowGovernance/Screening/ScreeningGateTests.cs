namespace StateLandGovernance.UnitTests.WorkflowGovernance.Screening;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.Screening;
using Xunit;

public class ScreeningGateTests
{
    private static LeaseCase CreateLeaseCase(Guid? currentSnapshotId = null)
    {
        var actorId = Guid.NewGuid();
        var leaseCaseId = new LeaseCaseId(Guid.NewGuid());
        var actionTime = DateTime.UtcNow;
        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, leaseCaseId.Value.ToString());
        var authority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "LeaseInitiator" },
            scope,
            actionTime.AddMinutes(-5),
            actionTime.AddMinutes(-2),
            actionTime.AddMinutes(10));

        var leaseCase = new LeaseCase(leaseCaseId, "APP-123", actorId, actionTime, authority);
        if (currentSnapshotId.HasValue)
        {
            leaseCase.SetCurrentVerifiedFactSnapshot(currentSnapshotId.Value);
        }

        return leaseCase;
    }

    [Fact]
    public void WorkflowProgression_Fails_When_Screening_Is_Missing()
    {
        var leaseCase = CreateLeaseCase();

        Assert.Null(leaseCase.LatestScreening);
        var ex = Assert.Throws<MissingScreeningException>(() => leaseCase.ProgressWorkflow());
        Assert.Contains("missing", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkflowProgression_Fails_When_Screening_Is_Stale()
    {
        var snapshotId = Guid.NewGuid();
        var currentSnapshotId = Guid.NewGuid();
        var leaseCase = CreateLeaseCase(currentSnapshotId);

        var screening = new ScreeningResult(
            Guid.NewGuid(),
            leaseCase.Id,
            snapshotId,
            ScreeningOutcome.Cleared,
            "Cleared on old snapshot",
            DateTime.UtcNow);

        leaseCase.RecordScreeningResult(screening);

        Assert.NotNull(leaseCase.LatestScreening);
        Assert.True(leaseCase.LatestScreening.IsStale(leaseCase.CurrentVerifiedFactSnapshotId));
        var ex = Assert.Throws<StaleScreeningException>(() => leaseCase.ProgressWorkflow());
        Assert.Contains("stale", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkflowProgression_Fails_When_Screening_Is_Blocked()
    {
        var snapshotId = Guid.NewGuid();
        var leaseCase = CreateLeaseCase(snapshotId);

        var screening = new ScreeningResult(
            Guid.NewGuid(),
            leaseCase.Id,
            snapshotId,
            ScreeningOutcome.Blocked,
            "Adverse environmental or legal finding detected",
            DateTime.UtcNow);

        leaseCase.RecordScreeningResult(screening);

        Assert.NotNull(leaseCase.LatestScreening);
        Assert.Equal(ScreeningOutcome.Blocked, leaseCase.LatestScreening.Outcome);
        var ex = Assert.Throws<BlockedScreeningException>(() => leaseCase.ProgressWorkflow());
        Assert.Contains("blocked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkflowProgression_Succeeds_When_Screening_Is_Cleared_And_Current()
    {
        var snapshotId = Guid.NewGuid();
        var leaseCase = CreateLeaseCase(snapshotId);

        var screening = new ScreeningResult(
            Guid.NewGuid(),
            leaseCase.Id,
            snapshotId,
            ScreeningOutcome.Cleared,
            "All clearance criteria met",
            DateTime.UtcNow);

        leaseCase.RecordScreeningResult(screening);

        Assert.NotNull(leaseCase.LatestScreening);
        Assert.False(leaseCase.LatestScreening.IsStale(leaseCase.CurrentVerifiedFactSnapshotId));
        Assert.Equal(ScreeningOutcome.Cleared, leaseCase.LatestScreening.Outcome);

        var ex = Record.Exception(() => leaseCase.ProgressWorkflow());
        Assert.Null(ex);
    }

    [Fact]
    public void WorkflowProgression_Succeeds_When_Screening_Is_Advisory_And_Current()
    {
        var snapshotId = Guid.NewGuid();
        var leaseCase = CreateLeaseCase(snapshotId);

        var screening = new ScreeningResult(
            Guid.NewGuid(),
            leaseCase.Id,
            snapshotId,
            ScreeningOutcome.Advisory,
            "Advisory notices detected but progression permitted",
            DateTime.UtcNow);

        leaseCase.RecordScreeningResult(screening);

        Assert.NotNull(leaseCase.LatestScreening);
        Assert.False(leaseCase.LatestScreening.IsStale(leaseCase.CurrentVerifiedFactSnapshotId));
        Assert.Equal(ScreeningOutcome.Advisory, leaseCase.LatestScreening.Outcome);

        var ex = Record.Exception(() => leaseCase.ProgressWorkflow());
        Assert.Null(ex);
    }
}
