namespace StateLandGovernance.UnitTests.WorkflowGovernance.Application;

using System.Text.Json;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;
using Xunit;

// Explicit synthetic fixtures. No real C4 model, spatial clearance, authority grant or live approval.
public sealed class PostWorkflowGovernanceSliceTests
{
    private sealed class Gateway : IPostWorkflowAssessmentGateway
    {
        public bool Available => true;
        public int Calls;
        public Action? DuringCall;
        public bool Mismatch;
        public Task<PostWorkflowAssessmentResult> AssessAsync(PostWorkflowTrace trace, CancellationToken ct)
        {
            Calls++; DuringCall?.Invoke();
            Assert.Equal("DS Final Notification", trace.Events.Last().Activity);
            Assert.All(trace.Events, e => Assert.Equal(DateTimeKind.Utc, e.AtUtc.Kind));
            return Task.FromResult(new PostWorkflowAssessmentResult(Guid.NewGuid(), Mismatch ? Guid.NewGuid() : trace.CaseId,
                trace.ExecutionId, trace.InputFingerprint, true, JsonSerializer.SerializeToElement(new { fixture = "AUTOMATED TEST ONLY", flagged = true })));
        }
    }
    private static VerifiedAuthorityContext Authority(ProjectWorkflowExecutionSliceTests.Fixture f) => f.Authority with {
        Capabilities = f.Authority.Capabilities.Concat(new[] { "DSNotificationReviewer", "PostWorkflowReviewer", "PostWorkflowFollowUpReviewer", "GovernanceRecordFinalizer" }).ToArray() };
    private static PostWorkflowGovernanceService Service(ProjectWorkflowExecutionSliceTests.Fixture f, InMemoryCorrectionRepository records, Gateway gateway) =>
        new(f.Cases, f.Executions, records, f.Documents, gateway, f.Screening, f.UnitOfWork, TimeProvider.System);
    private static async Task<ProjectWorkflowExecutionSliceTests.Fixture> Completed()
    {
        var f = new ProjectWorkflowExecutionSliceTests.Fixture(); await f.Start();
        await f.Decide("CABINET", "Approved"); await f.Decide("EXTERNAL_TEST_A", "Approved"); await f.Decide("DS", "Approved");
        await f.Decide("PROVINCIAL_LC", "Rejected");
        // The regional rejection is visible; it cannot itself stand in for the Commissioner's independent decision.
        Assert.Null(f.Case.CompletedWorkflow);
        await f.Decide("COMMISSIONER", "Approved");
        f.Case.RecordCompletedWorkflow(f.Executions.Value!.CreateCompletionRecord());
        return f;
    }
    private static async Task Notify(ProjectWorkflowExecutionSliceTests.Fixture f, PostWorkflowGovernanceService service)
    {
        var ds = f.Stage("DS"); var e = f.Executions.Value!;
        var doc = (await f.Documents.GetByLeaseCaseIdAsync(f.Case.Id)).Single(d => d.LogicalCategory == WorkflowExecutionService.DocumentCategory(e.Id.Value, ds.Definition.StageId.Value));
        await service.RecordNotificationAsync(f.Case.Id.Value, new(e.Revision, f.Case.CompletedWorkflow!.FinalDecisionId.Value,
            DateTime.UtcNow, doc.Id.Value, doc.ActiveVersionId.Value, "TEST ONLY reviewed actual notification receipt"), new(Authority(f), "DS"), default);
    }
    [Fact]
    public async Task PostWorkflow_ChangedCurrentScreeningBinding_MakesPreviousAdvisoryAssessmentStale()
    {
        var f = await Completed(); var records = new InMemoryCorrectionRepository(); var service = Service(f, records, new Gateway());
        await Notify(f, service);
        await service.AssessAsync(f.Case.Id.Value, new(f.Executions.Value!.Revision, "TEST ONLY"), Authority(f), default);
        Assert.True((await service.GetAsync(f.Case.Id.Value, default)).AssessmentCurrent);
        f.Screening.Value = f.Screening.Value with { LatestAssessment = f.Screening.Value.LatestAssessment! with { InputFingerprint = "CHANGED_TEST_EVIDENCE" } };
        Assert.False((await service.GetAsync(f.Case.Id.Value, default)).AssessmentCurrent);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinalizeAsync(f.Case.Id.Value,
            new(f.Executions.Value.Revision, "TEST ONLY"), Authority(f), default));
    }

    [Fact]
    public async Task PostWorkflow_MissingRealNotification_BlocksWithoutCallingC4()
    {
        var f = await Completed(); var gateway = new Gateway(); var service = Service(f, new(), gateway);
        var state = await service.GetAsync(f.Case.Id.Value, default);
        Assert.False(state.CanAssess); Assert.Contains(state.MissingInputs, m => m.Contains("DS Final Notification"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssessAsync(f.Case.Id.Value,
            new(f.Executions.Value!.Revision, "TEST review"), Authority(f), default));
        Assert.Equal(0, gateway.Calls);
    }
    [Fact]
    public async Task PostWorkflow_FlaggedResult_RequiresAuthorizedFollowUpBeforeImmutableFinalRecord()
    {
        var f = await Completed(); var records = new InMemoryCorrectionRepository(); var gateway = new Gateway(); var service = Service(f, records, gateway);
        await Notify(f, service); var id = f.Case.Id.Value; var actor = Authority(f); var request = new PostWorkflowReviewRequest(f.Executions.Value!.Revision, "TEST ONLY human review");
        await service.AssessAsync(id, request, actor, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinalizeAsync(id, request, actor, default));
        var state = await service.GetAsync(id, default);
        Assert.True(state.AssessmentCurrent); Assert.True(state.Assessment!.Flagged);
        await service.AssignFollowUpAsync(id, new(state.Assessment.AssessmentId, "DS", DateTime.UtcNow.AddDays(3), "TEST ONLY check records"), actor, default);
        var followUp = (await service.GetAsync(id, default)).FollowUps.Single();
        var resolution = new ResolvePostWorkflowFollowUpRequest(followUp.Revision, "TEST ONLY explanation reviewed", "test:source:receipt");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResolveFollowUpAsync(id, followUp.Id, resolution, new(actor, "TEST_A"), default));
        await service.ResolveFollowUpAsync(id, followUp.Id, resolution, new(actor, "DS"), default);
        await service.FinalizeAsync(id, request, actor, default);
        var finalized = await service.GetAsync(id, default);
        Assert.NotNull(finalized.FinalizedRecordId); Assert.StartsWith("Unavailable:", finalized.IntegrityStatus);
        Assert.Equal("Approved", f.Case.CompletedWorkflow!.FinalDecisionOutcome.ToString());
        Assert.Single((await records.ListAsync(id, default)).Where(r => r.Purpose == "FinalGovernanceRecord"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinalizeAsync(id, request, actor, default));
        Assert.NotNull(finalized.Assessment!.SourceTrace);
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task PostWorkflow_MismatchedOrChangedInputs_RejectsWithoutRecording(bool mismatch)
    {
        var f = await Completed(); var records = new InMemoryCorrectionRepository(); var gateway = new Gateway { Mismatch = mismatch };
        var service = Service(f, records, gateway); await Notify(f, service);
        if (!mismatch) gateway.DuringCall = () => f.Screening.Value = f.Screening.Value with { IsCurrent = false };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssessAsync(f.Case.Id.Value,
            new(f.Executions.Value!.Revision, "TEST ONLY"), Authority(f), default));
        Assert.DoesNotContain(await records.ListAsync(f.Case.Id.Value, default), r => r.Purpose == "PostWorkflowAssessment");
    }
}
