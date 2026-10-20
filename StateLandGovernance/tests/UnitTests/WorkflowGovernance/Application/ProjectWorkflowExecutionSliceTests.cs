namespace StateLandGovernance.UnitTests.WorkflowGovernance.Application;
using System.Text.Json;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Screening;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;

public sealed class ProjectWorkflowExecutionSliceTests
{
    // All successful authority, spatial and policy values below are synthetic test fixtures, never runtime configuration.
    public static RoutingPolicyDto Policy(Guid actor, params string[] provinces) => new("TEST_PROJECT", "1", "EXPLICIT TEST ONLY", actor,
        DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(1), new Dictionary<string,string> { ["TEST"] = "TEST" },
        ["TEST_A", "TEST_B"], true, 1, 1, 1, "DepartmentReviewer", "DSReviewer", "CommissionerReviewer", [],
        new("USER WORKFLOW TEST FIXTURE", "1", "MOCK_SPATIAL_TEST_V1", true, provinces, new Dictionary<string,string> { ["TEST_A"] = "Explicit test-selected department" }));
    public static Dictionary<string,string> Facts(string value, string unit) => new() { ["land_extent"] = value, ["land_extent_unit"] = unit };

    [Theory]
    [InlineData("49.999999", "acres", false)]
    [InlineData("50", "acres", false)]
    [InlineData("50.000001", "acres", true)]
    [InlineData("20.234282112", "ha", false)]
    [InlineData("20.234282113", "ha", true)]
    [InlineData("200", "roods", false)]
    [InlineData("8000", "perches", false)]
    public void ProjectGraph_CabinetThreshold_UsesExactNormalizedExtent(string value, string unit, bool cabinet)
    {
        var stages = ProjectWorkflowGraph.Build(Policy(Guid.NewGuid(), "TEST_P1"), "TEST", Facts(value, unit));
        Assert.Equal(cabinet, stages.Any(s => s.StageCode.Value == "CABINET"));
        Assert.Single(stages.Where(s => s.StageCode.Value == "DS"));
        Assert.DoesNotContain(stages, s => s.InstitutionCode.Value == "TEST_B");
        Assert.Equal("COMMISSIONER", stages.Single(s => s.IsFinalDecision).StageCode.Value);
    }
    [Fact]
    public void ProjectGraph_MultipleProvinces_UsesInterProvincialWithoutBorderInference()
    {
        var stages = ProjectWorkflowGraph.Build(Policy(Guid.NewGuid(), "TEST_P1", "TEST_P2"), "TEST", Facts("17", "acres"));
        Assert.Contains(stages, s => s.StageCode.Value == "INTER_PROVINCIAL_LC");
        Assert.DoesNotContain(stages, s => s.StageCode.Value == "PROVINCIAL_LC");
    }
    [Theory]
    [InlineData("province")][InlineData("selection")][InlineData("extent")]
    public void ProjectGraph_UnresolvedInputs_Block(string missing)
    {
        var p = Policy(Guid.NewGuid(), "TEST_P1"); var facts = Facts("17", "acres");
        if (missing == "province") p = p with { ProjectRouting = p.ProjectRouting! with { Provinces = [] } };
        if (missing == "selection") p = p with { ProjectRouting = p.ProjectRouting! with { SelectedDepartmentReasons = new Dictionary<string,string> { ["UNKNOWN"] = "Unconfigured" } } };
        if (missing == "extent") facts.Clear();
        Assert.Throws<InvalidOperationException>(() => ProjectWorkflowGraph.Build(p, "TEST", facts));
    }

    internal sealed class Fixture
    {
        public readonly Guid Actor = Guid.NewGuid();
        public readonly InMemoryLeaseCaseRepository Cases = new();
        public readonly InMemoryGovernedDocumentRepository Documents = new();
        public readonly ExecutionStore Executions = new();
        public readonly PlanStore Plans = new();
        public readonly Readiness Screening = new();
        public readonly Unit UnitOfWork = new();
        public readonly StateLandGovernance.WorkflowGovernance.Domain.LeaseCases.LeaseCase Case;
        public readonly WorkflowExecutionService Service;
        public readonly VerifiedAuthorityContext Authority;
        public Fixture()
        {
            var f = ScreeningOrchestrationTests.CreateCaseWithVerifiedSnapshot(actorId: Actor); Case = f.LeaseCase;
            Cases.AddAsync(Case).GetAwaiter().GetResult();
            var now = DateTime.UtcNow;
            Authority = new(Actor, ["WorkflowExecutor", "WorkflowPlanApprover", "WorkflowPlanSuperseder", "WorkflowPlanner",
                "CabinetReviewer", "DepartmentReviewer", "DSReviewer", "ProvincialReviewer", "CommissionerReviewer", "WorkflowDecisionReviser", "DocumentSubmitter"],
                "LeaseCase", Case.Id.Value.ToString("D"), now.AddHours(-1), now.AddSeconds(-1), now.AddHours(1));
            var policy = Policy(Actor, "TEST_P1"); Plans.Policies.Add(policy);
            var plan = WorkflowPlanningService.BuildPlan(Case.Id.Value, f.Snapshot.Id.Value, Guid.NewGuid(), policy, "TEST", now, Facts("51", "acres"));
            Case.SetInitialWorkflowPlan(plan); plan.ApprovePlan(Actor, DateTime.UtcNow, Authority.ToDomain());
            Case.RecordScreeningResult(new(Guid.NewGuid(), Case.Id, f.Snapshot.Id.Value, ScreeningOutcome.Advisory, "TEST ONLY", now));
            Screening.Value = new(Case.Id.Value, Case.Revision, f.Snapshot.Id.Value, null, "Assessed", true, [],
                new("c3-c4-v1", Case.Id.Value, f.Snapshot.Id.Value, null, "TEST_INPUT", "TEST_POLICY", JsonSerializer.SerializeToElement(new { assessmentId = Guid.NewGuid() }),
                    SourceBindingsJson: JsonSerializer.Serialize(new { spatialProvenance = new { version = "MOCK_SPATIAL_TEST_V1", provinces = new[] { "TEST_P1" } } })), IsDevelopmentSpatialMock: true);
            var inputs = JsonSerializer.Serialize(new { inputFingerprint = "TEST_INPUT", assessment = Screening.Value.LatestAssessment!.Assessment, policy }, CaseScreeningService.JsonOptions);
            Plans.Rows.Add(new(plan.Id.Value, plan.Revision, "Approved", f.Snapshot.Id.Value, policy.Identifier, policy.Version,
                WorkflowPlanningService.Fingerprint(inputs), Actor, Actor, plan.CreatedAt, plan.ApprovedAt, null, [],
                plan.Stages.Select(s => new PlanStageDto(s.Id.Value, s.StageCode.Value, s.InstitutionCode.Value, s.RequiredOfficerCapability,
                    s.StageType.ToString(), s.Prerequisites.Select(p => p.Value).ToArray(), null, s.ReasonDescription)).ToArray(), inputs));
            Service = new(Cases, Executions, Plans, Screening, Documents, UnitOfWork, TimeProvider.System);
        }
        public InstitutionalActorContext ActorFor(string institution) => new(Authority, institution);
        public Task<WorkflowExecutionState> Start() => Service.StartAsync(Case.Id.Value, new(Case.ActiveWorkflowPlan!.Id.Value,
            Case.ActiveWorkflowPlan.Revision, Case.Revision), Authority, default);
        public WorkflowStageExecution Stage(string code) => Executions.Value!.Stages.Single(s => s.Definition.StageCode.Value == code);
        public async Task Decide(string code, string outcome, bool revision = false)
        {
            var e = Executions.Value!; var s = Stage(code);
            if (!revision) await Service.StageStartAsync(Case.Id.Value, s.Definition.StageId.Value, e.Revision, ActorFor(s.Definition.InstitutionCode.Value), default);
            e = Executions.Value!; s = Stage(code);
            var doc = new GovernedDocument(new(Guid.NewGuid()), Case.Id, WorkflowExecutionService.DocumentCategory(e.Id.Value, s.Definition.StageId.Value),
                new(Guid.NewGuid()), new("SHA-256", new string('a',64)), new("TEST_ONLY/decision.pdf"), "TEST_ONLY.pdf", "application/pdf", 10,
                Actor, DateTime.UtcNow, Authority.ToDomain());
            await Documents.AddAsync(doc);
            await Service.DecideAsync(Case.Id.Value, s.Definition.StageId.Value, new(e.Revision, outcome, "Explicit fixture decision",
                outcome == "ApprovedWithConditions" ? ["Explicit test condition"] : [], [new(doc.Id.Value, doc.ActiveVersionId.Value)],
                revision ? s.Decision!.Id.Value : null, revision ? "Reviewed corrected test evidence" : null), ActorFor(s.Definition.InstitutionCode.Value), default);
        }
    }
    internal sealed class ExecutionStore : IWorkflowExecutionStore
    {
        public WorkflowExecution? Value;
        public Task<WorkflowExecution?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(Value);
        public Task SaveAsync(WorkflowExecution e, CancellationToken ct) { Value = PersistenceJsonSerializer.Deserialize<WorkflowExecution>(PersistenceJsonSerializer.Serialize(e)); return Task.CompletedTask; }
        public Task<IReadOnlyList<Guid>> RejectedCasesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>(Value?.Stages.Any(s => s.Decision?.Outcome == WorkflowStageDecisionOutcome.Rejected) == true ? [Value.LeaseCaseId.Value] : []);
    }
    internal sealed class PlanStore : IWorkflowPlanningStore
    {
        public List<RoutingPolicyDto> Policies = []; public List<WorkflowPlanDto> Rows = [];
        public Task<IReadOnlyList<RoutingPolicyDto>> GetPoliciesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<RoutingPolicyDto>>(Policies);
        public Task<IReadOnlyList<WorkflowPlanDto>> GetPlansAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyList<WorkflowPlanDto>>(Rows);
        public Task<bool> HasExecutionAsync(Guid id, CancellationToken ct) => Task.FromResult(false);
        public Task SaveAsync(Guid id, WorkflowPlanDto p, CancellationToken ct) { Rows.RemoveAll(r => r.Id == p.Id); Rows.Add(p); return Task.CompletedTask; }
    }
    internal sealed class Readiness : ICaseScreeningReadiness
    { public CaseScreeningState Value = null!; public Task<CaseScreeningState> GetAsync(Guid id, Guid? snapshot, Guid? parcel, CancellationToken ct) => Task.FromResult(Value); }
    internal sealed class Unit : IWorkflowGovernanceUnitOfWork
    { public Task<int> CommitAsync(CancellationToken ct = default) => Task.FromResult(1); }

    [Fact]
    public void ProjectRouting_ChangedSpatialVersionOrProvinces_CannotReuseTheScreeningBinding()
    {
        var context = Policy(Guid.NewGuid(), "TEST_P1").ProjectRouting!;
        var stored = new StoredCaseScreening("c3-c4-v1", Guid.NewGuid(), Guid.NewGuid(), null, "TEST", "TEST", JsonSerializer.SerializeToElement(new { }),
            SourceBindingsJson: JsonSerializer.Serialize(new { spatialProvenance = new { version = context.SpatialVersion, provinces = new[] { "TEST_P1" } } }));
        Assert.Null(ProjectWorkflowGraph.SourceBindingError(context, stored, true));
        Assert.NotNull(ProjectWorkflowGraph.SourceBindingError(context with { SpatialVersion = "CHANGED" }, stored, true));
        Assert.NotNull(ProjectWorkflowGraph.SourceBindingError(context with { Provinces = ["TEST_P1", "TEST_P2"] }, stored, true));
        Assert.NotNull(ProjectWorkflowGraph.SourceBindingError(context with { IsDevelopmentMock = false }, stored, true));
    }

    [Fact]
    public async Task Execution_CurrentButBlockedScreening_CannotActivateAnApprovedPlan()
    {
        var f = new Fixture();
        f.Case.RecordScreeningResult(new(Guid.NewGuid(), f.Case.Id, f.Case.CurrentVerifiedFactSnapshotId,
            ScreeningOutcome.Blocked, "EXPLICIT TEST BLOCKED", DateTime.UtcNow));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Start());
        Assert.Null(f.Executions.Value);
    }

    [Fact]
    public async Task Execution_RejectionWithoutReason_CannotUseDocumentReferencesAsItsReason()
    {
        var f = new Fixture(); await f.Start(); var stage = f.Stage("CABINET");
        await f.Service.StageStartAsync(f.Case.Id.Value, stage.Definition.StageId.Value, f.Executions.Value!.Revision, f.ActorFor("CABINET"), default);
        var doc = new GovernedDocument(new(Guid.NewGuid()), f.Case.Id,
            WorkflowExecutionService.DocumentCategory(f.Executions.Value!.Id.Value, stage.Definition.StageId.Value),
            new(Guid.NewGuid()), new("SHA-256", new string('a',64)), new("TEST_ONLY/rejection.pdf"), "TEST_ONLY.pdf", "application/pdf", 1,
            f.Actor, DateTime.UtcNow, f.Authority.ToDomain());
        await f.Documents.AddAsync(doc);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.DecideAsync(f.Case.Id.Value, stage.Definition.StageId.Value,
            new(f.Executions.Value.Revision, "Rejected", " ", [], [new(doc.Id.Value, doc.ActiveVersionId.Value)]), f.ActorFor("CABINET"), default));
        Assert.Null(f.Stage("CABINET").Decision);
    }

    [Fact]
    public async Task Execution_DecisionWithSupersededEvidenceVersion_IsRejected()
    {
        var f = new Fixture(); await f.Start(); var e = f.Executions.Value!; var stage = f.Stage("CABINET");
        await f.Service.StageStartAsync(f.Case.Id.Value, stage.Definition.StageId.Value, e.Revision, f.ActorFor("CABINET"), default);
        var doc = new GovernedDocument(new(Guid.NewGuid()), f.Case.Id, WorkflowExecutionService.DocumentCategory(e.Id.Value, stage.Definition.StageId.Value),
            new(Guid.NewGuid()), new("SHA-256", new string('a',64)), new("TEST_ONLY/old.pdf"), "TEST_ONLY_old.pdf", "application/pdf", 10,
            f.Actor, DateTime.UtcNow, f.Authority.ToDomain());
        var old = doc.ActiveVersionId;
        doc.AddVersion(new(Guid.NewGuid()), old, new("SHA-256", new string('b',64)), new("TEST_ONLY/new.pdf"), "TEST_ONLY_new.pdf", "application/pdf", 11,
            f.Actor, DateTime.UtcNow, f.Authority.ToDomain());
        await f.Documents.AddAsync(doc);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.DecideAsync(f.Case.Id.Value, stage.Definition.StageId.Value,
            new(f.Executions.Value!.Revision, "Approved", "TEST ONLY", [], [new(doc.Id.Value, old.Value)]), f.ActorFor("CABINET"), default));
        Assert.Null(f.Stage("CABINET").Decision); Assert.Equal(2, doc.Versions.Count);
    }

    [Fact]
    public async Task Execution_ExternalRejection_BlocksDsAndReportsCommissioner()
    {
        var f = new Fixture(); await f.Start(); await f.Decide("CABINET", "Approved"); await f.Decide("EXTERNAL_TEST_A", "Rejected");
        Assert.Equal(WorkflowStageExecutionStatus.Blocked, f.Stage("DS").Status);
        await Assert.ThrowsAnyAsync<Exception>(() => f.Service.StageStartAsync(f.Case.Id.Value, f.Stage("DS").Definition.StageId.Value,
            f.Executions.Value!.Revision, f.ActorFor("DS"), default));
        Assert.Contains(f.Case.Id.Value, await f.Service.RejectedCasesAsync(default));
        await f.Decide("EXTERNAL_TEST_A", "ApprovedWithConditions", true);
        Assert.Equal(WorkflowStageExecutionStatus.Ready, f.Stage("DS").Status);
        Assert.Single(f.Stage("EXTERNAL_TEST_A").DecisionHistory);
    }
    [Fact]
    public async Task Execution_DsAddition_PreviewApprovalAdoptionPreservesDecisions()
    {
        var f = new Fixture(); await f.Start(); await f.Decide("CABINET", "Approved"); await f.Decide("EXTERNAL_TEST_A", "Approved");
        var oldDecision = f.Stage("EXTERNAL_TEST_A").Decision!.Id;
        await f.Service.AddDepartmentAsync(f.Case.Id.Value, new(f.Executions.Value!.Revision, f.Case.Revision, "TEST_B", "Explicit DS fixture request"), f.ActorFor("DS"), default);
        Assert.Equal(WorkflowPlanStatus.Draft, f.Case.ActiveWorkflowPlan!.Status);
        Assert.DoesNotContain(f.Executions.Value.Stages, s => s.Definition.InstitutionCode.Value == "TEST_B");
        await f.Service.ApproveRevisionAsync(f.Case.Id.Value, f.Case.ActiveWorkflowPlan.Id.Value, f.Case.Revision,
            f.Case.ActiveWorkflowPlan.Revision, f.Executions.Value.Revision, f.Authority, default);
        Assert.Equal(oldDecision, f.Stage("EXTERNAL_TEST_A").Decision!.Id);
        Assert.Single(f.Executions.Value!.PlanHistory);
        Assert.Equal(WorkflowStageExecutionStatus.Blocked, f.Stage("DS").Status);
        await f.Decide("EXTERNAL_TEST_B", "Approved");
        Assert.Equal(WorkflowStageExecutionStatus.Ready, f.Stage("DS").Status);
    }
    [Theory]
    [InlineData("stale")][InlineData("permission")][InlineData("revision")]
    public async Task Execution_StaleOrUnauthorizedStart_Rejects(string kind)
    {
        var f = new Fixture();
        if (kind == "stale") f.Screening.Value = f.Screening.Value with { IsCurrent = false };
        var authority = kind == "permission" ? f.Authority with { Capabilities = ["Officer"] } : f.Authority;
        await Assert.ThrowsAnyAsync<Exception>(() => f.Service.StartAsync(f.Case.Id.Value, new(f.Case.ActiveWorkflowPlan!.Id.Value,
            f.Case.ActiveWorkflowPlan.Revision, kind == "revision" ? f.Case.Revision - 1 : f.Case.Revision), authority, default));
        Assert.Null(f.Executions.Value);
    }
    [Fact]
    public async Task InstitutionalProjection_CrossInstitutionRejected_OwnHistoryOnly()
    {
        var f = new Fixture(); await f.Start();
        await Assert.ThrowsAnyAsync<Exception>(() => f.Service.RestrictedAsync(f.Case.Id.Value, f.Stage("EXTERNAL_TEST_A").Definition.StageId.Value, f.ActorFor("TEST_B"), default));
        var own = await f.Service.RestrictedAsync(f.Case.Id.Value, f.Stage("EXTERNAL_TEST_A").Definition.StageId.Value, f.ActorFor("TEST_A"), default);
        var json = JsonSerializer.Serialize(own);
        Assert.DoesNotContain("C4", json); Assert.DoesNotContain("TEST_B", json); Assert.DoesNotContain("snapshot", json, StringComparison.OrdinalIgnoreCase);
    }
}
