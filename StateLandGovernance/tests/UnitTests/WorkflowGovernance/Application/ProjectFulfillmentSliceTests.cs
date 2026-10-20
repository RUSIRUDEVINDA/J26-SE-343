namespace StateLandGovernance.UnitTests.WorkflowGovernance.Application;

using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Fulfillment;
using StateLandGovernance.WorkflowGovernance.Domain.Handoff;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;
using Xunit;

// Explicit fixtures: no real documents, approval, production identity or runtime policy.
public sealed class ProjectFulfillmentSliceTests
{
    [Fact]
    public async Task Fulfillment_CurrentButBlockedScreening_DoesNotCountAsAnAcceptedGate()
    {
        var f = new ProjectWorkflowExecutionSliceTests.Fixture();
        f.Case.RecordScreeningResult(new(Guid.NewGuid(), f.Case.Id, f.Case.CurrentVerifiedFactSnapshotId,
            StateLandGovernance.WorkflowGovernance.Domain.Screening.ScreeningOutcome.Blocked, "EXPLICIT TEST BLOCKED", DateTime.UtcNow));
        var service = new ProjectFulfillmentService(f.Cases, new InMemoryCorrectionRepository(), f.Documents, f.Executions,
            f.Screening, f.UnitOfWork, TimeProvider.System);
        var state = await service.GetAsync(f.Case.Id.Value, default);
        Assert.Contains(state.MissingInputs, m => m.Contains("Screening outcome is blocked"));
        Assert.False(state.ReadyForFinalRecord); Assert.False(state.ReadyForHandoff);
    }

    [Fact]
    public async Task Fulfillment_SubmissionWaitsForReview_AndAcceptedLastRequirementReassessesCaseReadiness()
    {
        var f = new ProjectWorkflowExecutionSliceTests.Fixture(); var records = new InMemoryCorrectionRepository();
        var service = new ProjectFulfillmentService(f.Cases, records, f.Documents, f.Executions, f.Screening, f.UnitOfWork, TimeProvider.System);
        var authority = f.Authority with { Capabilities = f.Authority.Capabilities.Concat(new[] { "FulfillmentConfigurator", "FulfillmentReviewer" }).ToArray() };
        await service.ConfigureAsync(f.Case.Id.Value, new(f.Case.Revision, "TEST-ONLY-1", [new("APPLICANT_NIC", DateTime.UtcNow.AddDays(2), null, null)]), authority, default);
        var r = (await records.ListAsync(f.Case.Id.Value)).Single();
        await f.Start(); await f.Decide("CABINET", "Approved"); await f.Decide("EXTERNAL_TEST_A", "Approved");
        await f.Decide("DS", "Approved"); await f.Decide("PROVINCIAL_LC", "Approved"); await f.Decide("COMMISSIONER", "Approved");
        f.Case.RecordCompletedWorkflow(f.Executions.Value!.CreateCompletionRecord());
        Assert.Equal(LeaseCaseStatus.ApprovedWithConditions, f.Case.Status);
        var doc = new GovernedDocument(new(Guid.NewGuid()), f.Case.Id, "ProjectFulfillment:APPLICANT_NIC", new(Guid.NewGuid()),
            new("SHA-256", new string('a',64)), new("TEST_ONLY/nic.pdf"), "TEST_ONLY.pdf", "application/pdf", 10,
            f.Actor, DateTime.UtcNow, f.Authority.ToDomain()); await f.Documents.AddAsync(doc);
        await service.SubmitAsync(f.Case.Id.Value, r.Id, f.Actor, null, new(doc.Id.Value, doc.ActiveVersionId.Value), r.Revision, "TEST ONLY", default);
        r = (await records.GetAsync(f.Case.Id.Value, r.Id))!;
        Assert.Equal("SubmittedAwaitingReview", r.Status);
        Assert.Equal(FulfillmentStatus.Pending, f.Case.DocumentSubmissionRequirements.Single().Status);
        Assert.Equal(LeaseCaseStatus.ApprovedWithConditions, f.Case.Status);
        await service.ReviewAsync(f.Case.Id.Value, r.Id, new(r.Revision, true, "TEST ONLY exact version reviewed"), authority, default);
        Assert.Equal(LeaseCaseStatus.ReadyForHandoff, f.Case.Status);
        // Project completeness still blocks handoff: this test configured only one obligation.
        Assert.False((await service.GetAsync(f.Case.Id.Value, default)).ReadyForHandoff);
        var reviewed = (await records.GetAsync(f.Case.Id.Value, r.Id))!;
        Assert.Equal("Accepted", reviewed.Submissions.Single().Decision);
        Assert.Equal(f.Actor, reviewed.Submissions.Single().ReviewedBy);
    }
    [Fact]
    public async Task Fulfillment_UnknownTitleAndUnauthorizedConfiguration_RemainBlocked()
    {
        var f = new ProjectWorkflowExecutionSliceTests.Fixture(); var records = new InMemoryCorrectionRepository();
        var service = new ProjectFulfillmentService(f.Cases, records, f.Documents, f.Executions, f.Screening, f.UnitOfWork, TimeProvider.System);
        var request = new ConfigureProjectFulfillmentRequest(f.Case.Revision, "TEST-ONLY-1", [new("LC_APPROVAL_1", DateTime.UtcNow.AddDays(2), null, null)]);
        await Assert.ThrowsAnyAsync<Exception>(() => service.ConfigureAsync(f.Case.Id.Value, request, f.Authority, default));
        Assert.Empty(await records.ListAsync(f.Case.Id.Value));
        var actor = f.Authority with { Capabilities = f.Authority.Capabilities.Concat(new[] { "FulfillmentConfigurator", "FulfillmentReviewer", "FulfillmentWaiver" }).ToArray() };
        await service.ConfigureAsync(f.Case.Id.Value, request, actor, default);
        var r = (await records.ListAsync(f.Case.Id.Value)).Single();
        Assert.True(r.TitlePending);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.WaiveAsync(f.Case.Id.Value, r.Id,
            new(f.Case.Revision, r.Revision, "TEST placeholder", "TEST reference"), new(actor, "COMMISSIONER"), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAssignedAsync(f.Case.Id.Value, r.Id, Guid.NewGuid(), "TEST_A", default));
        Assert.Equal(FulfillmentStatus.Pending, f.Case.DocumentSubmissionRequirements.Single().Status);
    }
}
