// EXPLICIT ISOLATED FIXTURES ONLY. No normal-host DI, live approval, real officer grant or parcel clearance.
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.UnitTests.WorkflowGovernance.Application;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Screening;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;

internal static class ExecutionPersistenceProof
{
    public static async Task RunAsync(IServiceProvider sp, bool write, bool complete = false)
    {
        var caseId = Guid.Parse(complete ? "fac60000-0000-4000-8000-000000000001" : "fac30000-0000-0000-0000-000000000003");
        var actor = Guid.Parse("fac30000-0000-0000-0000-000000000004");
        var cases = sp.GetRequiredService<ILeaseCaseRepository>(); var store = sp.GetRequiredService<IWorkflowExecutionStore>();
        var plans = sp.GetRequiredService<IWorkflowPlanningStore>(); var unit = sp.GetRequiredService<IWorkflowGovernanceUnitOfWork>();
        var documents = sp.GetRequiredService<IGovernedDocumentRepository>(); var service = sp.GetRequiredService<WorkflowExecutionService>();
        var now = DateTime.UtcNow;
        var authority = new VerifiedAuthorityContext(actor, ["WorkflowExecutor", "WorkflowPlanner", "WorkflowPlanApprover", "WorkflowPlanSuperseder",
            "CabinetReviewer", "DepartmentReviewer", "DSReviewer", "ProvincialReviewer", "CommissionerReviewer", "WorkflowDecisionReviser", "DocumentSubmitter", "LeaseInitiator", "FulfillmentConfigurator", "FulfillmentReviewer", "FulfillmentForwardReviewer", "ConditionReviewer", "WorkflowFinalRecorder", "HandoffPreparer", "HandoffDispatcher", "HandoffAcknowledger", "DSNotificationReviewer", "PostWorkflowReviewer", "PostWorkflowFollowUpReviewer", "GovernanceRecordFinalizer"],
            "LeaseCase", caseId.ToString("D"), now.AddMinutes(-1), now, now.AddHours(1));
        if (write)
        {
            if (await cases.GetByIdAsync(new(caseId)) != null) throw new InvalidOperationException("Fixture already exists; use --execution-read, never overwrite.");
            var f = ScreeningOrchestrationTests.CreateCaseWithVerifiedSnapshot(caseId, actor);
            if (complete) f = (new StateLandGovernance.WorkflowGovernance.Domain.LeaseCases.LeaseCase(new(caseId),
                "ISOLATED-EXECUTION-COMPLETE-20261017", actor, now, authority.ToDomain(), null, f.Snapshot.Id.Value), f.Document, f.Analysis, f.Snapshot);
            await cases.AddAsync(f.LeaseCase); await documents.AddAsync(f.Document);
            await sp.GetRequiredService<IDocumentAnalysisRepository>().AddAsync(f.Analysis);
            var binding = new AssessedDocumentBinding(f.Document.Id, f.Document.ActiveVersionId, f.Document.Versions.First().Checksum);
            var assessment = new DocumentCompletenessAssessment(new(Guid.NewGuid()), f.LeaseCase.Id, "ISOLATED_EXECUTION_TEST", "1",
                [binding], [new(new(Guid.NewGuid()), binding, new("TEST"), new(1), DocumentClassificationStatus.Accepted, null, null, null, now)], [], now);
            await sp.GetRequiredService<IDocumentCompletenessAssessmentRepository>().AddAsync(assessment);
            f.LeaseCase.RecordScreeningResult(new(Guid.NewGuid(), f.LeaseCase.Id, f.Snapshot.Id.Value, ScreeningOutcome.Advisory, "ISOLATED TEST C4 DOUBLE", now));
            await unit.CommitAsync();
            var policy = ProjectWorkflowExecutionSliceTests.Policy(actor, "ISOLATED_MOCK_PROVINCE") with {
                Identifier = complete ? "ISOLATED_EXECUTION_COMPLETION_TEST" : "ISOLATED_EXECUTION_TEST", SourceReference = "ISOLATED TEST ONLY, not approved runtime rules", ApplicableFacts = new Dictionary<string,string> { ["SurveyPlan.PlanNumber"] = "SP-2026-9901" } };
            await sp.GetRequiredService<PostgresWorkflowPlanningStore>().PublishPolicyAsync(policy, default);
            var plan = WorkflowPlanningService.BuildPlan(caseId, f.Snapshot.Id.Value, assessment.Id.Value, policy, "ISOLATED_TEST",
                DateTime.UtcNow, ProjectWorkflowExecutionSliceTests.Facts("51", "acres"));
            f.LeaseCase.SetInitialWorkflowPlan(plan); plan.ApprovePlan(actor, DateTime.UtcNow, authority.ToDomain());
            var current = await sp.GetRequiredService<ICaseScreeningReadiness>().GetAsync(caseId, null, null, default);
            var inputs = JsonSerializer.Serialize(new { inputFingerprint = "ISOLATED_EXECUTION_TEST", assessment = current.LatestAssessment!.Assessment, policy }, CaseScreeningService.JsonOptions);
            await plans.SaveAsync(caseId, new(plan.Id.Value, plan.Revision, "Approved", f.Snapshot.Id.Value, policy.Identifier, policy.Version,
                WorkflowPlanningService.Fingerprint(inputs), actor, actor, plan.CreatedAt, plan.ApprovedAt, null, [],
                plan.Stages.Select(s => new PlanStageDto(s.Id.Value, s.StageCode.Value, s.InstitutionCode.Value, s.RequiredOfficerCapability,
                    s.StageType.ToString(), s.Prerequisites.Select(p => p.Value).ToArray(), null, s.ReasonDescription)).ToArray(), inputs), default);
            await unit.CommitAsync();
            await service.StartAsync(caseId, new(plan.Id.Value, plan.Revision, f.LeaseCase.Revision), authority, default);

            async Task Decide(string code, string outcome, bool revise = false)
            {
                var e = (await store.GetAsync(caseId, default))!;
                var stage = e.Stages.Single(s => s.Definition.StageCode.Value == code);
                var institution = new InstitutionalActorContext(authority, stage.Definition.InstitutionCode.Value);
                if (!revise) await service.StageStartAsync(caseId, stage.Definition.StageId.Value, e.Revision, institution, default);
                e = (await store.GetAsync(caseId, default))!; stage = e.Stages.Single(s => s.Definition.StageCode.Value == code);
                var doc = new GovernedDocument(new(Guid.NewGuid()), f.LeaseCase.Id, WorkflowExecutionService.DocumentCategory(e.Id.Value, stage.Definition.StageId.Value),
                    new(Guid.NewGuid()), new("SHA-256", new string('a',64)), new("ISOLATED_TEST/decision.pdf"), "ISOLATED_TEST_ONLY.pdf", "application/pdf", 10,
                    actor, DateTime.UtcNow, authority.ToDomain());
                await documents.AddAsync(doc); await unit.CommitAsync();
                await service.DecideAsync(caseId, stage.Definition.StageId.Value, new(e.Revision, outcome, "ISOLATED TEST decision",
                    outcome == "ApprovedWithConditions" ? ["ISOLATED TEST condition"] : [], [new(doc.Id.Value, doc.ActiveVersionId.Value)],
                    revise ? stage.Decision!.Id.Value : null, revise ? "ISOLATED TEST reasoned decision revision" : null), institution, default);
            }
            await Decide("CABINET", "Approved"); await Decide("EXTERNAL_TEST_A", "Rejected");
            if ((await store.GetAsync(caseId, default))!.Stages.Single(s => s.Definition.StageCode.Value == "DS").Status.ToString() != "Blocked")
                throw new InvalidOperationException("Rejection did not block DS.");
            await Decide("EXTERNAL_TEST_A", "ApprovedWithConditions", true);
            var execution = (await store.GetAsync(caseId, default))!;
            await service.AddDepartmentAsync(caseId, new(execution.Revision, f.LeaseCase.Revision, "TEST_B", "ISOLATED TEST DS department addition"), new(authority, "DS"), default);
            execution = (await store.GetAsync(caseId, default))!;
            await service.ApproveRevisionAsync(caseId, f.LeaseCase.ActiveWorkflowPlan!.Id.Value, f.LeaseCase.Revision,
                f.LeaseCase.ActiveWorkflowPlan.Revision, execution.Revision, authority, default);
            await Decide("EXTERNAL_TEST_B", "Approved");
            if (complete)
            {
                await Decide("DS", "Approved"); await Decide("PROVINCIAL_LC", "Rejected"); await Decide("COMMISSIONER", "Approved");
                await FullWorkflowPersistenceProof.CompleteAsync(sp, caseId, authority);
            }
        }
        var stored = await store.GetAsync(caseId, default) ?? throw new InvalidOperationException("Run --execution-write first.");
        var external = stored.Stages.Single(s => s.Definition.StageCode.Value == "EXTERNAL_TEST_A");
        if (external.DecisionHistory.Count != 1 || stored.PlanHistory.Count != 1 ||
            external.DecisionHistory.Single().PreviousDecision.Outcome.ToString() != "Rejected" ||
            stored.Stages.Single(s => s.Definition.StageCode.Value == "DS").Status.ToString() != (complete ? "Completed" : "Ready"))
            throw new InvalidOperationException("Persisted execution/revision history assertions failed.");
        var c = (await cases.GetByIdAsync(new(caseId)))!;
        if (c.WorkflowPlanHistory.Count != 2) throw new InvalidOperationException("Prior plan history was lost.");
        Console.WriteLine(JsonSerializer.Serialize(new { fixture = "ISOLATED TEST ONLY; not real approval/OCR/spatial evidence",
            database = "workflow_execution_20261016", mode = write ? "write-decide-revise-add-department-approve-adopt" : "fresh-process-restart-retrieval", completeFixture = complete,
            caseId, executionId = stored.Id.Value, stored.Revision, activePlanId = c.ActiveWorkflowPlan!.Id.Value,
            plans = c.WorkflowPlanHistory.Select(p => new { id = p.Id.Value, status = p.Status.ToString(), p.Revision }),
            decisionHistory = external.DecisionHistory.Select(h => new { previousId = h.PreviousDecision.Id.Value,
                previousOutcome = h.PreviousDecision.Outcome.ToString(), replacementId = h.ReplacementDecision.Id.Value,
                replacementOutcome = h.ReplacementDecision.Outcome.ToString(), h.Reason, h.ExecutionRevision }),
            planHistory = stored.PlanHistory.Select(h => new { previousPlan = h.PreviousDefinition.WorkflowPlanId.Value,
                replacementPlan = h.ReplacementDefinition.WorkflowPlanId.Value, h.Reason, h.At }),
            stages = stored.Stages.Select(s => new { id = s.Definition.StageId.Value, code = s.Definition.StageCode.Value,
                status = s.Status.ToString(), outcome = s.Decision?.Outcome.ToString() }) }));
        if (complete) await FullWorkflowPersistenceProof.AssertAsync(sp, caseId, write);
    }
}
internal sealed class ExecutionFixtureReadiness(ILeaseCaseRepository cases) : ICaseScreeningReadiness
{
    public async Task<CaseScreeningState> GetAsync(Guid id, Guid? snapshot, Guid? parcel, CancellationToken ct)
    {
        var c = (await cases.GetByIdAsync(new(id), ct))!;
        return new(id, c.Revision, c.CurrentVerifiedFactSnapshotId, null, "Assessed", true, [],
            new("c3-c4-v1", id, c.CurrentVerifiedFactSnapshotId, null, "ISOLATED_EXECUTION_TEST", "ISOLATED_EXECUTION_TEST",
                JsonSerializer.SerializeToElement(new { assessmentId = c.LatestScreening!.Id, caseId = id,
                    verifiedFactSnapshotId = c.CurrentVerifiedFactSnapshotId, overallStatus = "RequiresReview" })));
    }
}
