// EXPLICIT ISOLATED FIXTURES ONLY: synthetic documents, authority, C4 inference and downstream transport.
// These adapters are registered only by this console's --execution-complete-* mode, never by the host.
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

internal static class FullWorkflowPersistenceProof
{
    public static async Task CompleteAsync(IServiceProvider sp, Guid id, VerifiedAuthorityContext actor)
    {
        var c = (await sp.GetRequiredService<ILeaseCaseRepository>().GetByIdAsync(new(id)))!;
        var execution = (await sp.GetRequiredService<IWorkflowExecutionStore>().GetAsync(id, default))!;
        var records = sp.GetRequiredService<ICorrectionRepository>();
        var documents = sp.GetRequiredService<IGovernedDocumentRepository>();
        var unit = sp.GetRequiredService<IWorkflowGovernanceUnitOfWork>();
        var fulfillment = sp.GetRequiredService<ProjectFulfillmentService>();
        var obligations = ProjectFulfillmentService.Catalogue.Select(o => new ConfigureProjectObligationRequest(o.Code,
            DateTime.UtcNow.AddDays(30), o.TitlePending ? "ISOLATED TEST TITLE: " + o.Code : null,
            o.TitlePending ? "ISOLATED AUTOMATED FIXTURE ONLY; not an owner-approved runtime title" : null)).ToArray();
        await fulfillment.ConfigureAsync(id, new(c.Revision, "ISOLATED-FULFILLMENT-TEST-1", obligations), actor, default);
        var report = (await records.ListAsync(id)).Single(r => r.ClassificationCode == "DS_RELEVANT_LAND_REPORT");
        await fulfillment.RecordLeaseRecommendationAsync(id, report.Id, new(report.Revision,
            execution.Stages.Single(s => s.Definition.StageCode.Value == "DS").Decision!.Id.Value, true, "ISOLATED TEST lease recommendation"), new(actor, "DS"), default);
        foreach (var entry in (await records.ListAsync(id)).Where(r => r.Purpose == "ProjectFulfillment"))
        {
            var r = (await records.GetAsync(id, entry.Id))!;
            var doc = new GovernedDocument(new(Guid.NewGuid()), c.Id, "ProjectFulfillment:" + r.ClassificationCode,
                new(Guid.NewGuid()), new("SHA-256", new string('b',64)), new("ISOLATED_TEST_ONLY/fulfillment.pdf"),
                "ISOLATED_TEST_ONLY.pdf", "application/pdf", 10, actor.ActorId, DateTime.UtcNow, actor.ToDomain());
            await documents.AddAsync(doc); await unit.CommitAsync();
            await fulfillment.SubmitAsync(id, r.Id, actor.ActorId, r.AssignedInstitution, new(doc.Id.Value, doc.ActiveVersionId.Value), r.Revision, "ISOLATED TEST upload metadata", default);
            if (r.ClassificationCode == "DS_RELEVANT_LAND_REPORT")
            {
                r = (await records.GetAsync(id, r.Id))!;
                await fulfillment.RecordForwardingAsync(id, r.Id, new(r.Revision, "ISOLATED TEST Provincial receipt"), new(actor, "PROVINCIAL_LC"), default);
                r = (await records.GetAsync(id, r.Id))!;
                await fulfillment.RecordForwardingAsync(id, r.Id, new(r.Revision, "ISOLATED TEST Commissioner receipt"), new(actor, "COMMISSIONER"), default);
            }
            r = (await records.GetAsync(id, r.Id))!;
            await fulfillment.ReviewAsync(id, r.Id, new(r.Revision, true, "ISOLATED TEST exact version reviewed"), actor, default);
        }
        await fulfillment.RecordFinalAsync(id, c.Revision, actor, default);
        foreach (var condition in c.ApprovalConditions)
            await fulfillment.FulfillConditionAsync(id, condition.Id, new(c.Revision, "ISOLATED TEST condition review", "test:evidence:condition"),
                new(actor, condition.InstitutionCode.Value), default);
        var handoff = sp.GetRequiredService<GovernedHandoffService>();
        var prepared = await handoff.PrepareAsync(id, new(c.Revision, Guid.NewGuid(), "ISOLATED-TEST-TRANSPORT-" + id.ToString("N")), actor, default);
        await handoff.DispatchAsync(id, new(prepared.CaseRevision, prepared.Package!.Id), actor, default);
        var post = sp.GetRequiredService<PostWorkflowGovernanceService>();
        var ds = execution.Stages.Single(s => s.Definition.StageCode.Value == "DS");
        var dsDoc = (await documents.GetByLeaseCaseIdAsync(c.Id)).Single(d => d.LogicalCategory == WorkflowExecutionService.DocumentCategory(execution.Id.Value, ds.Definition.StageId.Value));
        await post.RecordNotificationAsync(id, new(execution.Revision, c.CompletedWorkflow!.FinalDecisionId.Value,
            DateTime.UtcNow, dsDoc.Id.Value, dsDoc.ActiveVersionId.Value, "ISOLATED TEST notification evidence; no real message"), new(actor, "DS"), default);
        await post.AssessAsync(id, new(execution.Revision, "ISOLATED TEST C4 inference double"), actor, default);
        var state = await post.GetAsync(id, default);
        await post.AssignFollowUpAsync(id, new(state.Assessment!.AssessmentId, "DS", DateTime.UtcNow.AddDays(3), "ISOLATED TEST advisory follow-up"), actor, default);
        var followUp = (await post.GetAsync(id, default)).FollowUps.Single();
        await post.ResolveFollowUpAsync(id, followUp.Id, new(followUp.Revision, "ISOLATED TEST reviewed explanation", "test:evidence:post-review"), new(actor, "DS"), default);
        await post.FinalizeAsync(id, new(execution.Revision, "ISOLATED TEST human finalization; external integrity contract unavailable"), actor, default);
    }

    public static async Task AssertAsync(IServiceProvider sp, Guid id, bool write)
    {
        var c = (await sp.GetRequiredService<ILeaseCaseRepository>().GetByIdAsync(new(id)))!;
        var all = await sp.GetRequiredService<ICorrectionRepository>().ListAsync(id);
        var post = await sp.GetRequiredService<PostWorkflowGovernanceService>().GetAsync(id, default);
        var fulfillment = await sp.GetRequiredService<ProjectFulfillmentService>().GetAsync(id, default);
        if (c.Status.ToString() != "HandedOff" || c.CompletedWorkflow == null || c.HandoffPackage?.DownstreamAcknowledgementId == null ||
            fulfillment.Requirements.Count != 13 || fulfillment.Requirements.Any(r => r.Status != "Fulfilled") ||
            c.ApprovalConditions.Any(r => r.Status.ToString() != "Fulfilled") || (write && !post.AssessmentCurrent) ||
            post.FinalizedRecordId == null || post.FollowUps.Single().Status != "ResolvedAfterHumanReview")
            throw new InvalidOperationException("Full PostgreSQL fixture lifecycle retrieval assertions failed.");
        Console.WriteLine(JsonSerializer.Serialize(new { fixture = "ISOLATED FULL LIFECYCLE TEST ONLY; no real approvals, files, C4 inference or downstream delivery",
            mode = write ? "application-lifecycle-write" : "fresh-process-application-lifecycle-retrieval", database = "workflow_execution_20261016",
            caseId = id, c.Revision, status = c.Status.ToString(), finalDecisionId = c.CompletedWorkflow.FinalDecisionId.Value,
            packageId = c.HandoffPackage.Id, acknowledgementId = c.HandoffPackage.DownstreamAcknowledgementId,
            reviewedRequirements = fulfillment.Requirements.Select(r => new { r.Id, r.Code, r.Status, r.Revision,
                documentId = r.Submissions.Last().DocumentId, versionId = r.Submissions.Last().VersionId, r.Forwarding }),
            conditions = c.ApprovalConditions.Select(r => new { r.Id, status = r.Status.ToString() }),
            assessmentId = post.Assessment!.AssessmentId, assessmentCurrent = post.AssessmentCurrent,
            currentnessNotice = post.AssessmentCurrent ? null : "Historical fixture retrieved; stricter source bindings require reassessment. No history was rewritten or treated as current approval.",
            post.FollowUps, post.FinalizedRecordId, post.IntegrityStatus,
            finalManifestSha256 = all.Single(r => r.Purpose == "FinalGovernanceRecord").RequirementSourceReference }));
    }
}

internal sealed class FullWorkflowFixtureGateway : IPostWorkflowAssessmentGateway
{
    public bool Available => true;
    public Task<PostWorkflowAssessmentResult> AssessAsync(PostWorkflowTrace trace, CancellationToken ct) => Task.FromResult(
        new PostWorkflowAssessmentResult(Guid.NewGuid(), trace.CaseId, trace.ExecutionId, trace.InputFingerprint, true,
            JsonSerializer.SerializeToElement(new { fixture = "ISOLATED TEST INFERENCE DOUBLE", flagged = true })));
}
internal sealed class FullWorkflowFixtureTransport : IGovernedHandoffTransport
{
    public Task<HandoffTransportContract?> ReadContractAsync(CancellationToken ct) => Task.FromResult<HandoffTransportContract?>(new("ISOLATED_TEST_RECIPIENT", "1"));
    public Task<HandoffTransportResult> DispatchAsync(HandoffTransportRequest request, CancellationToken ct) =>
        Task.FromResult(new HandoffTransportResult(true, "ISOLATED TEST TRANSPORT DOUBLE; not real delivery", Guid.NewGuid()));
}
