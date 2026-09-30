namespace StateLandGovernance.UnitTests.WorkflowGovernance.WorkflowExecutionTests;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution.Events;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using Xunit;

public class WorkflowExecutionStateTests
{
        private WorkflowExecutionDefinition CreateDef(List<WorkflowStageDefinition>? stages = null)
    {
        var list = stages ?? new List<WorkflowStageDefinition>();
        if (!list.Any(s => s.IsFinalDecision))
        {
            list.Add(new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("F"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>()));
        }
        return new WorkflowExecutionDefinition(
            new WorkflowPlanId(Guid.NewGuid()),
            1,
            new LeaseCaseId(Guid.NewGuid()),
            new WorkflowRuleSetReference("R", "1"),
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            list.AsReadOnly()
        );
    }

    [Fact]
    public void Constructor_NullDefinition_Throws()
    {
        Assert.Throws<InvalidWorkflowExecutionException>(() => new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), null!, DateTime.UtcNow));
    }

    [Fact]
    public void Constructor_InitializesEntryStagesToReady()
    {
        var s1 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), WorkflowStageType.Review, "Cap", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
        var def = CreateDef(new List<WorkflowStageDefinition> { s1 });
        var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, DateTime.UtcNow);

        Assert.Equal(WorkflowStageExecutionStatus.Ready, exec.Stages.First(x => !x.Definition.IsFinalDecision).Status);
    }

    [Fact]
    public void Constructor_InitializesDependentStagesToBlocked()
    {
        var s1 = new WorkflowStageId(Guid.NewGuid());
        var s2 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), WorkflowStageType.Review, "Cap", new RoutingReasonCode("R"), "D", null, false, new[] { s1 });
        var def = CreateDef(new List<WorkflowStageDefinition> { s2 });
        var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, DateTime.UtcNow);

        Assert.Equal(WorkflowStageExecutionStatus.Blocked, exec.Stages.First(x => !x.Definition.IsFinalDecision).Status);
    }

    [Fact]
    public void Constructor_InitializesFinalStageToBlocked()
    {
        var sf = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), WorkflowStageType.FinalDecision, "Cap", new RoutingReasonCode("R"), "D", null, true, Array.Empty<WorkflowStageId>());
        var def = CreateDef(new List<WorkflowStageDefinition> { sf });
        var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, DateTime.UtcNow);

        Assert.Equal(WorkflowStageExecutionStatus.Blocked, exec.Stages.Single(x => x.Definition.IsFinalDecision).Status);
    }

    [Fact]
    public void Constructor_RaisesExecutionStartedEvent()
    {
        var def = CreateDef();
        var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, DateTime.UtcNow);

        var ev = Assert.Single(exec.DomainEvents.OfType<WorkflowExecutionStarted>());
        Assert.Equal(1, exec.Revision);
        Assert.Equal(1, ev.WorkflowExecutionRevision);
        Assert.Equal(exec.Id, ev.WorkflowExecutionId);
        Assert.Equal(exec.WorkflowPlanId, ev.WorkflowPlanId);
    }

    [Fact]
    public void Constructor_InitializesMultipleParallelEntriesReady()
    {
        var s1 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("A"), new InstitutionCode("I"), WorkflowStageType.Review, "C", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
        var s2 = new WorkflowStageDefinition(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("B"), new InstitutionCode("I"), WorkflowStageType.Review, "C", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
        var def = CreateDef(new List<WorkflowStageDefinition> { s1, s2 });
        var exec = new WorkflowExecution(new WorkflowExecutionId(Guid.NewGuid()), def, DateTime.UtcNow);

        Assert.Equal(2, exec.Stages.Count(x => x.Status == WorkflowStageExecutionStatus.Ready));
    }
}






