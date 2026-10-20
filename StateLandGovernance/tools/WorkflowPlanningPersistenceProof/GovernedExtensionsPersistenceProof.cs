// ISOLATED TEST FIXTURES ONLY. No real approval, extraction, evidence clearance or recipient.
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using StateLandGovernance.UnitTests.WorkflowGovernance.Application;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.Fulfillment;
using StateLandGovernance.WorkflowGovernance.Domain.Handoff;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.Screening;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

internal static class GovernedExtensionsPersistenceProof
{
    public static async Task RunAsync(IServiceProvider sp, bool write, bool version2 = false)
    {
        var caseId = Guid.Parse(version2 ? "fac40000-0000-4000-8000-000000000011" : "fac40000-0000-4000-8000-000000000001");
        var actor = Guid.Parse("fac40000-0000-4000-8000-000000000002");
        var cases = sp.GetRequiredService<ILeaseCaseRepository>();
        var analyses = sp.GetRequiredService<IDocumentAnalysisRepository>();
        var unit = sp.GetRequiredService<IWorkflowGovernanceUnitOfWork>();
        if (write)
        {
            if (await cases.GetByIdAsync(new(caseId)) != null) throw new InvalidOperationException("Fixture exists; use --extensions-read. Never overwrite.");
            var fixture = ScreeningOrchestrationTests.CreateCaseWithVerifiedSnapshot(caseId, actor);
            var at = DateTime.UtcNow;
            var authority = new VerifiedAuthoritySnapshot(actor,
                ["LeaseInitiator", "WorkflowPlanApprover", "FulfillmentWaiver", "HandoffPreparer", "HandoffDispatcher", "HandoffAcknowledger"],
                new(AuthorityScopeKind.LeaseCase, caseId.ToString("D")), at.AddMinutes(-1), at, at.AddHours(1));
            var c = new LeaseCase(new(caseId), "ISOLATED-EXTENSIONS-20261017" + (version2 ? "-V2" : ""), actor, at, authority, null, fixture.Snapshot.Id.Value);
            await cases.AddAsync(c);
            await sp.GetRequiredService<IGovernedDocumentRepository>().AddAsync(fixture.Document);
            await analyses.AddAsync(fixture.Analysis);
            var evidenceAuthority = new VerifiedAuthoritySnapshot(actor, ["HumanEvidenceReviewer", "FactSnapshotPublisher"],
                new(AuthorityScopeKind.GovernedDocument, fixture.Document.Id.Value.ToString("D")), at.AddMinutes(-1), at, at.AddHours(1));
            var evidence = new HumanEvidenceFact(Guid.NewGuid(), fixture.Document.Id, fixture.Document.ActiveVersionId,
                fixture.Analysis.DocumentChecksum, new("district"), new(AnalysisFactValueKind.Text, "ISOLATED TEST DISTRICT"),
                5, "ISOLATED TEST source quote", "TEST ONLY human observation", actor, at, evidenceAuthority);
            fixture.Analysis.PublishHumanEvidenceSnapshot(fixture.Snapshot.Id, new(Guid.NewGuid()), [evidence], fixture.Analysis.Revision, actor, at, evidenceAuthority);
            c.RecordScreeningResult(new(Guid.NewGuid(), c.Id, fixture.Snapshot.Id.Value, ScreeningOutcome.Advisory, "ISOLATED C4 DOUBLE", at));
            var plan = new WorkflowPlan(new(Guid.NewGuid()), c.Id, WorkflowPlanSource.Manual, fixture.Snapshot.Id,
                new(Guid.NewGuid()), new("ISOLATED-EXTENSIONS", "1"), null, at,
                [new(new(Guid.NewGuid()), new("FINAL"), new("TEST_COMMISSIONER"), WorkflowStageType.FinalDecision,
                    "WorkflowPlanApprover", new("ISOLATED_TEST"), "TEST ONLY", null, true, [])]);
            c.SetInitialWorkflowPlan(plan); plan.ApprovePlan(actor, at, authority);
            var requirement = c.AddDocumentSubmissionRequirement(new("TEST_CONDITIONAL"), at.AddDays(1));
            var condition = c.AddApprovalCondition(new("TEST_COMMISSIONER"), "ISOLATED TEST condition");
            c.WaiveFulfillment(requirement.Id, false, c.Revision, authority, "TEST ONLY non-applicability", "test:review:1", at);
            c.WaiveFulfillment(condition.Id, true, c.Revision, authority, "TEST ONLY condition waiver", "test:review:2", at);
            c.RecordCompletedWorkflow(new(new(Guid.NewGuid()), plan.Id, plan.Revision, c.Id, new(Guid.NewGuid()), new(Guid.NewGuid()),
                WorkflowStageDecisionOutcome.Approved, new("TEST_COMMISSIONER"), actor, at, []));
            var package = c.PrepareGovernedHandoff(c.Revision, authority, "ISOLATED-TEST-RECIPIENT:v1", "ISOLATED-TEST-IDEMPOTENCY", at, Guid.NewGuid());
            c.RecordGovernedHandoffDispatch(package.Id, c.Revision, authority, Guid.NewGuid(), false, "test:failed-send", at);
            if (c.Status == LeaseCaseStatus.HandedOff) throw new InvalidOperationException("Failed send incorrectly claims delivery.");
            c.RecordGovernedHandoffDispatch(package.Id, c.Revision, authority, Guid.NewGuid(), true, "test:transport-accepted", at);
            c.AcknowledgeGovernedHandoff(package.Id, package.RecipientContract!, package.IdempotencyKey!, plan.Id.Value,
                fixture.Snapshot.Id.Value, c.CompletedWorkflow!.FinalDecisionId.Value, Guid.NewGuid(), c.Revision, authority, at);
            await unit.CommitAsync();
        }
        var stored = await cases.GetByIdAsync(new(caseId)) ?? throw new InvalidOperationException("Run --extensions-write first.");
        var analysis = await analyses.GetByDocumentVersionIdAsync(new(stored.HandoffPackage == null ? Guid.Empty :
            (await analyses.GetSnapshotByIdAsync(new(stored.CurrentVerifiedFactSnapshotId)))!.DocumentVersionId.Value))
            ?? throw new InvalidOperationException("Analysis was not retrieved.");
        var manual = analysis.VerifiedFactSnapshots.SelectMany(s => s.Entries).Single(e => e.HumanEvidence != null);
        if (stored.Status != LeaseCaseStatus.HandedOff || stored.HandoffPackage?.DispatchHistory?.Count != 2 ||
            stored.DocumentSubmissionRequirements.Single().Waiver?.PreviousStatus != FulfillmentStatus.Pending ||
            stored.ApprovalConditions.Single().Waiver?.EvidenceReference != "test:review:2" || manual.SourceExtractedFactId != null ||
            analysis.VerifiedFactSnapshots.Count != 2 || stored.HandoffPackage.DownstreamAcknowledgementId == null)
            throw new InvalidOperationException("Governed extension persistence assertions failed.");
        if (version2 && (manual.HumanEvidence!.VerifiedCapability != "HumanEvidenceReviewer" || manual.HumanEvidence.AuthorityValidUntilUtc == null)) throw new InvalidOperationException("Human authority metadata was not preserved.");
        Console.WriteLine(JsonSerializer.Serialize(new { fixture = "ISOLATED TEST ONLY; no real case approval or delivery",
            database = "workflow_execution_20261016", mode = write ? "append-fixture" : "fresh-process-restart-retrieval", caseId,
            stored.Revision, status = stored.Status.ToString(), packageId = stored.HandoffPackage.Id,
            acknowledgementId = stored.HandoffPackage.DownstreamAcknowledgementId,
            dispatchAttempts = stored.HandoffPackage.DispatchHistory.Count,
            requirementWaiver = stored.DocumentSubmissionRequirements.Single().Waiver,
            conditionWaiver = stored.ApprovalConditions.Single().Waiver,
            snapshotIds = analysis.VerifiedFactSnapshots.Select(s => s.Id.Value),
            humanObservationId = manual.HumanEvidence!.Id, manual.SourceKind, manual.HumanEvidence.VerifiedCapability, manual.HumanEvidence.AuthorityScopeIdentifier, manual.HumanEvidence.AuthorityVerificationTimeUtc, sourceExtractedFactId = manual.SourceExtractedFactId?.Value,
            preservedMachineCandidateCount = analysis.Runs.Single().Result!.ExtractedFacts.Count }));
    }
}
