namespace StateLandGovernance.UnitTests.WorkflowGovernance.WorkflowExecutionTests;

using System;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using Xunit;

// Explicit synthetic project-policy fixtures; these are not approved live case configuration.
public sealed class GovernedExecutionRevisionTests
{
    private readonly DateTime _at = new(2026, 10, 16, 10, 0, 0, DateTimeKind.Utc);
    private readonly Guid _actor = Guid.NewGuid();
    private WorkflowStageDefinition Stage(string code, params WorkflowStageId[] prerequisites) =>
        new(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode(code), new InstitutionCode("TEST"),
            code == "FINAL" ? WorkflowStageType.FinalDecision : WorkflowStageType.Review, "Review",
            new RoutingReasonCode("TEST_ONLY"), "Synthetic test fixture", null, code == "FINAL", prerequisites);

    private (WorkflowExecution Execution, WorkflowStageDefinition External, WorkflowStageDefinition Ds) Fixture(bool gated = true)
    {
        var external = Stage("EXTERNAL");
        var ds = Stage("DS", external.StageId);
        var final = Stage("FINAL", ds.StageId);
        var definition = new WorkflowExecutionDefinition(new WorkflowPlanId(Guid.NewGuid()), 1,
            new LeaseCaseId(Guid.NewGuid()), new WorkflowRuleSetReference("TEST", "1"), _at.AddDays(-1), _at,
            new[] { external, ds, final });
        if (gated) definition = definition.WithPrerequisiteGatePolicy(new("TEST_APPROVAL", "1", new[] { ds.StageId }));
        return (new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), definition, _at), external, ds);
    }

    private AuthorityScope Scope(WorkflowExecution e) => new(AuthorityScopeKind.LeaseCase, e.LeaseCaseId.Value.ToString("D"));
    private VerifiedInstitutionalAuthoritySnapshot Auth(WorkflowExecution e, bool revise = true) =>
        new(_actor, new InstitutionCode("TEST"), revise ? new[] { "Review", "WorkflowDecisionReviser" } : new[] { "Review" },
            Scope(e), _at.AddHours(-1), _at, _at.AddHours(1));
    private void Decide(WorkflowExecution e, WorkflowStageDefinition stage, WorkflowStageDecisionOutcome outcome)
    {
        e.StartStage(stage.StageId, _actor, _at.AddMinutes(1), Auth(e));
        e.RecordStageDecision(new(Guid.NewGuid()), stage.StageId, outcome,
            outcome == WorkflowStageDecisionOutcome.Approved ? null : "Test decision reason",
            outcome == WorkflowStageDecisionOutcome.ApprovedWithConditions ? new[] { "Test condition" } : null,
            _actor, _at.AddMinutes(2), Auth(e));
    }

    [Theory]
    [InlineData(WorkflowStageDecisionOutcome.Rejected, WorkflowStageExecutionStatus.Blocked)]
    [InlineData(WorkflowStageDecisionOutcome.Approved, WorkflowStageExecutionStatus.Ready)]
    [InlineData(WorkflowStageDecisionOutcome.ApprovedWithConditions, WorkflowStageExecutionStatus.Ready)]
    public void RecordDecision_ExplicitApprovalGate_RespectsOutcome(WorkflowStageDecisionOutcome outcome, WorkflowStageExecutionStatus expected)
    {
        var (e, external, ds) = Fixture();
        Decide(e, external, outcome);
        Assert.Equal(expected, e.Stages.Single(s => s.Definition.StageId == ds.StageId).Status);
    }

    [Fact]
    public void RecordDecision_LegacyDefinition_RetainsCompletionGate()
    {
        var (e, external, ds) = Fixture(false);
        Decide(e, external, WorkflowStageDecisionOutcome.Rejected);
        Assert.Equal(WorkflowStageExecutionStatus.Ready, e.Stages.Single(s => s.Definition.StageId == ds.StageId).Status);
    }

    [Fact]
    public void ReviseDecision_Authorized_RetainsHistoryAndReleasesGate()
    {
        var (e, external, ds) = Fixture();
        Decide(e, external, WorkflowStageDecisionOutcome.Rejected);
        var stage = e.Stages.Single(s => s.Definition.StageId == external.StageId);
        var previous = stage.Decision!;
        e.ReviseStageDecision(external.StageId, e.Revision, previous.Id, new(Guid.NewGuid()),
            WorkflowStageDecisionOutcome.Approved, null, null, _actor, Auth(e), "Reviewed corrected evidence", _at.AddMinutes(3));
        Assert.Same(previous, Assert.Single(stage.DecisionHistory).PreviousDecision);
        Assert.Equal("Reviewed corrected evidence", stage.DecisionHistory.Single().Reason);
        Assert.Equal(WorkflowStageExecutionStatus.Ready, e.Stages.Single(s => s.Definition.StageId == ds.StageId).Status);
        var restored = StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings.PersistenceJsonSerializer
            .Deserialize<WorkflowExecution>(StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings.PersistenceJsonSerializer.Serialize(e))!;
        var history = Assert.Single(restored.Stages.Single(s => s.Definition.StageId == external.StageId).DecisionHistory);
        Assert.Equal(previous.Id, history.PreviousDecision.Id);
        Assert.Equal(e.Revision, restored.Revision);
        Assert.Equal("TEST_APPROVAL", restored.Definition.PrerequisiteGatePolicy!.Identifier);
        Assert.Empty(restored.DomainEvents);
        restored.StartStage(ds.StageId, _actor, _at.AddMinutes(4), Auth(restored));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReviseDecision_StaleOrUnauthorized_DoesNotMutate(bool stale)
    {
        var (e, external, _) = Fixture();
        Decide(e, external, WorkflowStageDecisionOutcome.Rejected);
        var stage = e.Stages.Single(s => s.Definition.StageId == external.StageId);
        var prior = stage.Decision!;
        var revision = e.Revision;
        var error = Record.Exception(() => e.ReviseStageDecision(external.StageId, stale ? revision - 1 : revision,
            prior.Id, new(Guid.NewGuid()), WorkflowStageDecisionOutcome.Approved, null, null,
            _actor, Auth(e, stale), "Test revision", _at.AddMinutes(3)));
        if (stale) Assert.IsType<InvalidWorkflowExecutionException>(error);
        else Assert.IsType<MissingVerifiedAuthorityException>(error);
        Assert.Same(prior, stage.Decision);
        Assert.Empty(stage.DecisionHistory);
        Assert.Equal(revision, e.Revision);
    }

    [Fact]
    public void ReviseDecision_DownstreamStarted_BlocksWithoutMutation()
    {
        var (e, external, ds) = Fixture();
        Decide(e, external, WorkflowStageDecisionOutcome.Approved);
        e.StartStage(ds.StageId, _actor, _at.AddMinutes(3), Auth(e));
        var previous = e.Stages.Single(s => s.Definition.StageId == external.StageId).Decision!;
        Assert.Throws<InvalidWorkflowExecutionException>(() => e.ReviseStageDecision(external.StageId, e.Revision,
            previous.Id, new(Guid.NewGuid()), WorkflowStageDecisionOutcome.Rejected, "New evidence", null,
            _actor, Auth(e), "Test revision", _at.AddMinutes(4)));
    }

    [Fact]
    public void AdoptPlan_NewDepartment_PreservesDecisionAndBlocksDs()
    {
        var (e, external, ds) = Fixture();
        Decide(e, external, WorkflowStageDecisionOutcome.Approved);
        var extra = Stage("EXTRA");
        var revisedDs = new WorkflowStageDefinition(ds.StageId, ds.StageCode, ds.InstitutionCode, ds.StageType,
            ds.RequiredOfficerCapability, ds.RoutingReasonCode, ds.ReasonDescription, null, false, new[] { external.StageId, extra.StageId });
        var replacement = new WorkflowExecutionDefinition(new(Guid.NewGuid()), 1, e.LeaseCaseId,
            new("TEST", "2"), _at.AddMinutes(3), _at.AddMinutes(4),
            new[] { external, extra, revisedDs, e.Definition.Stages.Single(s => s.IsFinalDecision) })
            .WithPrerequisiteGatePolicy(new("TEST_APPROVAL", "2", new[] { ds.StageId }));
        var prior = e.Stages.Single(s => s.Definition.StageId == external.StageId).Decision;
        e.AdoptRevisedPlan(replacement, e.Revision, _actor,
            new(_actor, new[] { "WorkflowPlanSuperseder" }, Scope(e), _at.AddHours(-1), _at, _at.AddHours(1)),
            "DS requested additional review", _at.AddMinutes(5));
        Assert.Same(prior, e.Stages.Single(s => s.Definition.StageId == external.StageId).Decision);
        Assert.Single(e.PlanHistory);
        Assert.Equal(WorkflowStageExecutionStatus.Blocked, e.Stages.Single(s => s.Definition.StageId == ds.StageId).Status);
        Assert.Equal(WorkflowStageExecutionStatus.Ready, e.Stages.Single(s => s.Definition.StageId == extra.StageId).Status);
        var restored = StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings.PersistenceJsonSerializer
            .Deserialize<WorkflowExecution>(StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings.PersistenceJsonSerializer.Serialize(e))!;
        Assert.Equal(e.PlanHistory.Single().PreviousDefinition.WorkflowPlanId, restored.PlanHistory.Single().PreviousDefinition.WorkflowPlanId);
        Assert.Equal(replacement.WorkflowPlanId, restored.WorkflowPlanId);
        Assert.Equal(WorkflowStageExecutionStatus.Blocked, restored.Stages.Single(s => s.Definition.StageId == ds.StageId).Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AdoptPlan_StartedStageChangedOrUnauthorized_DoesNotMutate(bool changeStartedStage)
    {
        var (e, external, ds) = Fixture();
        Decide(e, external, WorkflowStageDecisionOutcome.Approved);
        e.StartStage(ds.StageId, _actor, _at.AddMinutes(3), Auth(e));
        var extra = Stage("EXTRA");
        var revisedDs = changeStartedStage
            ? new WorkflowStageDefinition(ds.StageId, ds.StageCode, ds.InstitutionCode, ds.StageType,
                ds.RequiredOfficerCapability, ds.RoutingReasonCode, ds.ReasonDescription, null, false,
                new[] { external.StageId, extra.StageId }) : ds;
        var definition = new WorkflowExecutionDefinition(new(Guid.NewGuid()), 1, e.LeaseCaseId,
            new("TEST", "2"), _at.AddMinutes(3), _at.AddMinutes(4),
            new[] { external, extra, revisedDs, e.Definition.Stages.Single(s => s.IsFinalDecision) })
            .WithPrerequisiteGatePolicy(new("TEST_APPROVAL", "2", new[] { ds.StageId }));
        var prior = e.Definition;
        var revision = e.Revision;
        var error = Record.Exception(() => e.AdoptRevisedPlan(definition, revision, _actor,
            new(_actor, new[] { changeStartedStage ? "WorkflowPlanSuperseder" : "Review" }, Scope(e),
                _at.AddHours(-1), _at, _at.AddHours(1)), "Test revision", _at.AddMinutes(5)));
        if (changeStartedStage) Assert.IsType<InvalidWorkflowExecutionException>(error);
        else Assert.IsType<MissingVerifiedAuthorityException>(error);
        Assert.Same(prior, e.Definition);
        Assert.Equal(revision, e.Revision);
        Assert.Empty(e.PlanHistory);
    }

    [Fact]
    public void ReviseDecision_ToRejection_ReblocksUnstartedDs()
    {
        var (e, external, ds) = Fixture();
        Decide(e, external, WorkflowStageDecisionOutcome.Approved);
        var previous = e.Stages.Single(s => s.Definition.StageId == external.StageId).Decision!;
        e.ReviseStageDecision(external.StageId, e.Revision, previous.Id, new(Guid.NewGuid()),
            WorkflowStageDecisionOutcome.Rejected, "New source-backed objection", null, _actor, Auth(e),
            "Reconsidered evidence", _at.AddMinutes(3));
        Assert.Equal(WorkflowStageExecutionStatus.Blocked, e.Stages.Single(s => s.Definition.StageId == ds.StageId).Status);
        Assert.Throws<InvalidWorkflowStageTransitionException>(() => e.StartStage(ds.StageId, _actor, _at.AddMinutes(4), Auth(e)));
        Assert.Single(e.DomainEvents.OfType<StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution.Events.WorkflowStageDecisionRevised>());
    }

    [Fact]
    public void CompletedExecution_RevisionsRejected_CompletionSurvivesRoundtrip()
    {
        var (e, external, ds) = Fixture();
        Decide(e, external, WorkflowStageDecisionOutcome.Approved);
        e.StartStage(ds.StageId, _actor, _at.AddMinutes(3), Auth(e));
        e.RecordStageDecision(new(Guid.NewGuid()), ds.StageId, WorkflowStageDecisionOutcome.Approved,
            null, null, _actor, _at.AddMinutes(4), Auth(e));
        e.EvaluateConsensus(new(Guid.NewGuid()), new(new(Guid.NewGuid()), "TEST_ONLY", "1", e.LeaseCaseId,
            e.WorkflowPlanId, e.WorkflowPlanRevision, e.RuleSetReference, ConsensusRuleType.Unanimous,
            null, _at, new[] { new ConsensusParticipantRule(external.StageId, external.InstitutionCode, true, true),
                new ConsensusParticipantRule(ds.StageId, ds.InstitutionCode, true, true) }), _at.AddMinutes(5));
        var final = e.Definition.Stages.Single(s => s.IsFinalDecision);
        e.StartStage(final.StageId, _actor, _at.AddMinutes(6), Auth(e));
        e.RecordStageDecision(new(Guid.NewGuid()), final.StageId, WorkflowStageDecisionOutcome.Approved,
            "Synthetic authorized final decision", null, _actor, _at.AddMinutes(7), Auth(e));
        var completion = e.CreateCompletionRecord();
        var restored = StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings.PersistenceJsonSerializer
            .Deserialize<WorkflowExecution>(StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings.PersistenceJsonSerializer.Serialize(e))!;
        Assert.Equal(completion, restored.CreateCompletionRecord());
        var previous = restored.Stages.Single(s => s.Definition.StageId == external.StageId).Decision!;
        Assert.Throws<InvalidWorkflowExecutionException>(() => restored.ReviseStageDecision(external.StageId, restored.Revision,
            previous.Id, new(Guid.NewGuid()), WorkflowStageDecisionOutcome.Rejected, "Test", null, _actor,
            Auth(restored), "Test immutable guard", _at.AddMinutes(8)));
        Assert.Throws<InvalidWorkflowExecutionException>(() => restored.AdoptRevisedPlan(e.Definition, restored.Revision,
            _actor, new(_actor, new[] { "WorkflowPlanSuperseder" }, Scope(e), _at.AddHours(-1), _at, _at.AddHours(1)),
            "Test immutable guard", _at.AddMinutes(8)));
    }
}
