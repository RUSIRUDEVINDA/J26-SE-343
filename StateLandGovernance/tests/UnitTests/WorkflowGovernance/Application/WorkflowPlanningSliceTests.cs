namespace StateLandGovernance.UnitTests.WorkflowGovernance.Application;
using System.Text.Json;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.Screening;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;

public sealed class WorkflowPlanningSliceTests
{
    // Explicitly synthetic fixture. Never registered, published or inserted into a runtime database.
    private sealed class Fixture
    {
        public readonly Guid Actor = Guid.NewGuid();
        public readonly InMemoryLeaseCaseRepository Cases = new();
        public readonly InMemoryDocumentAnalysisRepository Analyses = new();
        public readonly InMemoryDocumentCompletenessAssessmentRepository Completeness = new();
        public readonly Store Storage = new();
        public readonly Readiness Screening = new();
        public readonly UnitOfWork Unit = new();
        public readonly StateLandGovernance.WorkflowGovernance.Domain.LeaseCases.LeaseCase Case;
        public readonly WorkflowPlanningService Service;
        public VerifiedAuthorityContext Authority;
        public Fixture()
        {
            var f = ScreeningOrchestrationTests.CreateCaseWithVerifiedSnapshot(actorId: Actor);
            Case = f.LeaseCase;
            Cases.AddAsync(Case).GetAwaiter().GetResult(); Analyses.AddAsync(f.Analysis).GetAwaiter().GetResult();
            var now = DateTime.UtcNow;
            Authority = new(Actor, ["WorkflowPlanner", "WorkflowPlanApprover", "WorkflowPlanSuperseder"], "LeaseCase", Case.Id.Value.ToString("D"), now.AddHours(-1), now, now.AddHours(1));
            var documentBinding = new AssessedDocumentBinding(f.Document.Id, f.Document.ActiveVersionId, f.Document.Versions.First().Checksum);
            var classification = new ClassifiedDocument(new(Guid.NewGuid()), documentBinding, new("TEST"), new(1m),
                DocumentClassificationStatus.Accepted, null, null, null, now);
            var assessment = new DocumentCompletenessAssessment(new(Guid.NewGuid()), Case.Id, "TEST-ONLY", "1",
                [documentBinding], [classification], [], now);
            Completeness.AddAsync(assessment).GetAwaiter().GetResult();
            Case.RecordScreeningResult(new(Guid.NewGuid(), Case.Id, f.Snapshot.Id.Value, ScreeningOutcome.Advisory, "TEST-ONLY", now));
            var result = JsonSerializer.SerializeToElement(new { assessmentId = Case.LatestScreening!.Id, caseId = Case.Id.Value,
                verifiedFactSnapshotId = f.Snapshot.Id.Value, overallStatus = "RequiresReview" });
            Screening.Value = new(Case.Id.Value, Case.Revision, f.Snapshot.Id.Value, null, "Assessed", true, [],
                new("c3-c4-v1", Case.Id.Value, f.Snapshot.Id.Value, null, "TEST-INPUT", "TEST-POLICY", result, "{}", "TEST-BINDING"));
            Storage.Policies.Add(new("TEST-ONLY-ROUTING", "1", "TEST fixture — not approved runtime policy", Actor,
                now.AddHours(-1), now.AddHours(1), new Dictionary<string,string> { ["SurveyPlan.PlanNumber"] = "SP-2026-9901" },
                ["TEST_A", "TEST_B"], true, 10, 8, 5, "TestReviewer", "TestDS", "TestLC", ["TEST evidence"]));
            Service = new(Cases, Analyses, Completeness, Storage, Screening, Unit, TimeProvider.System);
        }
        public Task<WorkflowPlanningState> Generate(string? reason = null) => Service.GenerateAsync(Case.Id.Value,
            new(Case.CurrentVerifiedFactSnapshotId, Case.Revision, Case.ActiveWorkflowPlan?.Id.Value, Case.ActiveWorkflowPlan?.Revision, reason), Authority, default);
        public Task<WorkflowPlanningState> Approve(WorkflowPlanningState state) => Service.ApproveAsync(Case.Id.Value, state.ActivePlan!.Id,
            new(state.SnapshotId!.Value, state.CaseRevision, state.ActivePlan.Revision), Authority, default);
    }
    private sealed class Readiness : ICaseScreeningReadiness
    {
        public CaseScreeningState Value = null!;
        public Task<CaseScreeningState> GetAsync(Guid id, Guid? snapshot, Guid? parcel, CancellationToken ct) => Task.FromResult(Value);
    }
    private sealed class UnitOfWork : IWorkflowGovernanceUnitOfWork
    {
        public int Commits;
        public Task<int> CommitAsync(CancellationToken ct = default) => Task.FromResult(++Commits);
    }
    private sealed class Store : IWorkflowPlanningStore
    {
        public List<RoutingPolicyDto> Policies = [];
        public List<WorkflowPlanDto> Plans = [];
        public bool Execution;
        public Task<IReadOnlyList<RoutingPolicyDto>> GetPoliciesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<RoutingPolicyDto>>(Policies);
        public Task<IReadOnlyList<WorkflowPlanDto>> GetPlansAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyList<WorkflowPlanDto>>(Plans.ToList());
        public Task<bool> HasExecutionAsync(Guid id, CancellationToken ct) => Task.FromResult(Execution);
        public Task SaveAsync(Guid id, WorkflowPlanDto plan, CancellationToken ct) { Plans.RemoveAll(p => p.Id == plan.Id); Plans.Add(plan); return Task.CompletedTask; }
    }
    [Fact]
    public async Task Fixture_GenerationPreviewApproval_PreservesBlueprintAndHistory()
    {
        var f = new Fixture(); var draft = await f.Generate();
        Assert.Equal("Draft", draft.ActivePlan!.Status); Assert.Null(draft.ActivePlan.ApprovedAt); Assert.False(f.Storage.Execution);
        Assert.Equal(4, draft.ActivePlan.Stages.Count); Assert.Equal(2, draft.ActivePlan.Stages.Count(s => s.Dependencies.Count == 0));
        var approved = await f.Approve(draft);
        Assert.Equal("Approved", approved.ActivePlan!.Status); Assert.Equal("InWorkflow", f.Case.Status.ToString());
        Assert.Null(f.Case.CompletedWorkflow); Assert.Null(f.Case.HandoffPackage);
        var revised = await f.Generate("TEST reason: updated officer routing review");
        Assert.Equal(2, revised.History.Count); Assert.Equal("Draft", revised.ActivePlan!.Status);
        Assert.Contains(revised.History, p => p.Id == approved.ActivePlan.Id && p.Status == "Superseded" && p.RevisionReason!.Contains("TEST reason"));
        Assert.Equal(2, f.Case.WorkflowPlanHistory.Count);
        var reloaded = LeaseCasePersistenceMapper.ToDomain(LeaseCasePersistenceMapper.ToPersistence(f.Case));
        Assert.Equal(revised.ActivePlan.Id, reloaded.ActiveWorkflowPlan!.Id.Value);
        Assert.Equal(2, reloaded.WorkflowPlanHistory.Count);
        Assert.Contains(reloaded.WorkflowPlanHistory, p => p.Id.Value == approved.ActivePlan.Id
            && p.Status == WorkflowPlanStatus.Superseded && p.ApprovedAt.HasValue && p.SupersessionReason!.Contains("TEST reason"));
    }
    [Fact]
    public async Task Fixture_ChangedInputBinding_RejectsApprovalAndRequiresRevision()
    {
        var f = new Fixture(); var draft = await f.Generate();
        f.Screening.Value = f.Screening.Value with { LatestAssessment = f.Screening.Value.LatestAssessment! with { InputFingerprint = "CHANGED" } };
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Approve(draft));
        var state = await f.Service.GetAsync(f.Case.Id.Value, f.Authority, default);
        Assert.False(state.Ready); Assert.False(state.CanApprove); Assert.Equal(WorkflowPlanStatus.Draft, f.Case.ActiveWorkflowPlan!.Status);
        var revised = await f.Generate("TEST changed inputs"); Assert.NotEqual(draft.ActivePlan!.Binding, revised.ActivePlan!.Binding);
    }
    [Theory]
    [InlineData("rules")][InlineData("screening")][InlineData("execution")][InlineData("authority")]
    public async Task Fixture_MissingOrUnauthorizedInputs_DoNotGenerate(string condition)
    {
        var f = new Fixture();
        if (condition == "rules") f.Storage.Policies.Clear();
        if (condition == "screening") f.Screening.Value = f.Screening.Value with { IsCurrent = false, MissingInputs = ["TEST stale assessment"] };
        if (condition == "execution") f.Storage.Execution = true;
        if (condition == "authority") f.Authority = f.Authority with { Capabilities = ["FactVerifier"] };
        await Assert.ThrowsAnyAsync<Exception>(() => f.Generate()); Assert.Empty(f.Storage.Plans); Assert.Equal(0, f.Unit.Commits);
    }
    [Fact]
    public async Task Fixture_OutdatedRevisionAndUnprivilegedApproval_AreRejected()
    {
        var f = new Fixture(); var draft = await f.Generate(); await f.Approve(draft);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Approve(draft));
        var revised = await f.Generate("TEST revision"); f.Authority = f.Authority with { Capabilities = ["WorkflowPlanner"] };
        await Assert.ThrowsAnyAsync<Exception>(() => f.Approve(revised)); Assert.Equal(WorkflowPlanStatus.Draft, f.Case.ActiveWorkflowPlan!.Status);
    }
    [Fact]
    public async Task Fixture_PlanSerialization_RetainsApprovalRevisionAndDependencyGraph()
    {
        var f = new Fixture(); var approved = await f.Approve(await f.Generate());
        var plan = f.Case.ActiveWorkflowPlan!;
        var json = PersistenceJsonSerializer.Serialize(plan);
        var copy = PersistenceJsonSerializer.Deserialize<WorkflowPlan>(json)!;
        Assert.Equal(plan.Status, copy.Status); Assert.Equal(plan.Revision, copy.Revision); Assert.Equal(plan.ApprovedAt, copy.ApprovedAt);
        Assert.Equal(plan.RuleSetReference, copy.RuleSetReference);
        Assert.Contains("TEST-POLICY", approved.ActivePlan!.InputBindingsJson);
        Assert.Equal(plan.Stages.Select(s => s.StageCode), copy.Stages.Select(s => s.StageCode)); Assert.Empty(copy.DomainEvents);
        var other = WorkflowPlanningService.BuildPlan(f.Case.Id.Value, approved.SnapshotId!.Value, plan.DocumentCompletenessAssessmentId.Value,
            f.Storage.Policies.Single(), approved.ActivePlan!.Binding, DateTime.UtcNow);
        Assert.Equal(plan.Stages.Select(s => s.Id), other.Stages.Select(s => s.Id));
    }
    [Fact]
    public async Task Fixture_ChangedRoutingVersion_RequiresExplicitRevisionReason()
    {
        var f = new Fixture(); var first = await f.Generate();
        f.Storage.Policies[0] = f.Storage.Policies[0] with { Version = "2", StandardDays = 11 };
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Approve(first));
        await Assert.ThrowsAnyAsync<Exception>(() => f.Generate());
        Assert.Single(f.Storage.Plans); Assert.Equal(WorkflowPlanStatus.Draft, f.Case.ActiveWorkflowPlan!.Status);
        var next = await f.Generate("TEST policy revision"); Assert.Equal("2", next.ActivePlan!.RuleVersion);
        Assert.Equal(2, next.History.Count);
    }
    [Fact]
    public async Task Fixture_OutdatedCaseAndSnapshot_AreRejectedBeforeMutation()
    {
        var f = new Fixture();
        await Assert.ThrowsAnyAsync<Exception>(() => f.Service.GenerateAsync(f.Case.Id.Value,
            new(f.Case.CurrentVerifiedFactSnapshotId, f.Case.Revision + 1, null, null, null), f.Authority, default));
        await Assert.ThrowsAnyAsync<Exception>(() => f.Service.GenerateAsync(f.Case.Id.Value,
            new(Guid.NewGuid(), f.Case.Revision, null, null, null), f.Authority, default));
        Assert.Empty(f.Storage.Plans); Assert.Equal(0, f.Unit.Commits);
    }
}
