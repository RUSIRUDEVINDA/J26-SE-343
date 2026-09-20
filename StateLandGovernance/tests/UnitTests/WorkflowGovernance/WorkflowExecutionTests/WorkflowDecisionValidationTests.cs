namespace StateLandGovernance.UnitTests.WorkflowGovernance.WorkflowExecutionTests;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using Xunit;

public class WorkflowDecisionValidationTests
{
    private WorkflowStageDecisionId Did => new WorkflowStageDecisionId(Guid.NewGuid());
    private Guid Actor => Guid.NewGuid();
    private DateTime Now => DateTime.UtcNow;

    [Fact]
    public void ValidApproved()
    {
        var d = new WorkflowStageDecision(Did, new WorkflowStageId(Guid.NewGuid()), new InstitutionCode("I"), WorkflowStageDecisionOutcome.Approved, "Reason", null, Actor, Now);
        Assert.Equal("Reason", d.Reason);
        Assert.Empty(d.Conditions);
    }

    [Fact]
    public void Approved_WithConditions_Rejected()
    {
        Assert.Throws<InvalidWorkflowStageDecisionException>(() => new WorkflowStageDecision(Did, new WorkflowStageId(Guid.NewGuid()), new InstitutionCode("I"), WorkflowStageDecisionOutcome.Approved, null, new[] { "C" }, Actor, Now));
    }

    [Fact]
    public void ValidApprovedWithConditions()
    {
        var d = new WorkflowStageDecision(Did, new WorkflowStageId(Guid.NewGuid()), new InstitutionCode("I"), WorkflowStageDecisionOutcome.ApprovedWithConditions, "Reason", new[] { "C1" }, Actor, Now);
        Assert.Equal("Reason", d.Reason);
        Assert.Single(d.Conditions);
    }

    [Fact]
    public void ApprovedWithConditions_WithoutReason_Throws()
    {
        Assert.Throws<InvalidWorkflowStageDecisionException>(() => new WorkflowStageDecision(Did, new WorkflowStageId(Guid.NewGuid()), new InstitutionCode("I"), WorkflowStageDecisionOutcome.ApprovedWithConditions, null, new[] { "C" }, Actor, Now));
    }

    [Fact]
    public void ApprovedWithConditions_WithoutConditions_Throws()
    {
        Assert.Throws<InvalidWorkflowStageDecisionException>(() => new WorkflowStageDecision(Did, new WorkflowStageId(Guid.NewGuid()), new InstitutionCode("I"), WorkflowStageDecisionOutcome.ApprovedWithConditions, "Reason", null, Actor, Now));
    }

    [Fact]
    public void ValidRejected()
    {
        var d = new WorkflowStageDecision(Did, new WorkflowStageId(Guid.NewGuid()), new InstitutionCode("I"), WorkflowStageDecisionOutcome.Rejected, "Reason", null, Actor, Now);
        Assert.Equal("Reason", d.Reason);
    }

    [Fact]
    public void Rejected_WithoutReason_Throws()
    {
        Assert.Throws<InvalidWorkflowStageDecisionException>(() => new WorkflowStageDecision(Did, new WorkflowStageId(Guid.NewGuid()), new InstitutionCode("I"), WorkflowStageDecisionOutcome.Rejected, null, null, Actor, Now));
    }

    [Fact]
    public void Rejected_WithConditions_Throws()
    {
        Assert.Throws<InvalidWorkflowStageDecisionException>(() => new WorkflowStageDecision(Did, new WorkflowStageId(Guid.NewGuid()), new InstitutionCode("I"), WorkflowStageDecisionOutcome.Rejected, "Reason", new[] { "C" }, Actor, Now));
    }

    [Fact]
    public void ValidChangesRequested()
    {
        var d = new WorkflowStageDecision(Did, new WorkflowStageId(Guid.NewGuid()), new InstitutionCode("I"), WorkflowStageDecisionOutcome.ChangesRequested, "Reason", new[] { "C" }, Actor, Now);
        Assert.Equal("Reason", d.Reason);
        Assert.Single(d.Conditions);
    }

    [Fact]
    public void ChangesRequested_WithoutReason_Throws()
    {
        Assert.Throws<InvalidWorkflowStageDecisionException>(() => new WorkflowStageDecision(Did, new WorkflowStageId(Guid.NewGuid()), new InstitutionCode("I"), WorkflowStageDecisionOutcome.ChangesRequested, null, new[] { "C" }, Actor, Now));
    }

    [Fact]
    public void ChangesRequested_WithoutConditions_Throws()
    {
        Assert.Throws<InvalidWorkflowStageDecisionException>(() => new WorkflowStageDecision(Did, new WorkflowStageId(Guid.NewGuid()), new InstitutionCode("I"), WorkflowStageDecisionOutcome.ChangesRequested, "Reason", null, Actor, Now));
    }

    [Fact]
    public void ValidAbstained()
    {
        var d = new WorkflowStageDecision(Did, new WorkflowStageId(Guid.NewGuid()), new InstitutionCode("I"), WorkflowStageDecisionOutcome.Abstained, "Reason", null, Actor, Now);
        Assert.Equal("Reason", d.Reason);
    }

    [Fact]
    public void Abstained_WithoutReason_Throws()
    {
        Assert.Throws<InvalidWorkflowStageDecisionException>(() => new WorkflowStageDecision(Did, new WorkflowStageId(Guid.NewGuid()), new InstitutionCode("I"), WorkflowStageDecisionOutcome.Abstained, null, null, Actor, Now));
    }

    [Fact]
    public void Abstained_WithConditions_Throws()
    {
        Assert.Throws<InvalidWorkflowStageDecisionException>(() => new WorkflowStageDecision(Did, new WorkflowStageId(Guid.NewGuid()), new InstitutionCode("I"), WorkflowStageDecisionOutcome.Abstained, "Reason", new[] { "C" }, Actor, Now));
    }

    [Fact]
    public void OverlengthReason_Throws()
    {
        var longReason = new string('a', 501);
        Assert.Throws<InvalidWorkflowStageDecisionException>(() => new WorkflowStageDecision(Did, new WorkflowStageId(Guid.NewGuid()), new InstitutionCode("I"), WorkflowStageDecisionOutcome.Rejected, longReason, null, Actor, Now));
    }

    [Fact]
    public void ControlCharacterInReason_Throws()
    {
        Assert.Throws<InvalidWorkflowStageDecisionException>(() => new WorkflowStageDecision(Did, new WorkflowStageId(Guid.NewGuid()), new InstitutionCode("I"), WorkflowStageDecisionOutcome.Rejected, "Rea\nson", null, Actor, Now));
    }

        
    [Fact]
    public void DeterministicConditionOrdering()
    {
        var d = new WorkflowStageDecision(Did, new WorkflowStageId(Guid.NewGuid()), new InstitutionCode("I"), WorkflowStageDecisionOutcome.ChangesRequested, "R", new[] { "B", "a", "C" }, Actor, Now);
        var list = new List<string>(d.Conditions);
        Assert.Equal("a", list[0]);
        Assert.Equal("B", list[1]);
        Assert.Equal("C", list[2]);
    }

    [Fact]
    public void MoreThan10Conditions_Throws()
    {
        var conds = new List<string>();
        for (int i=0; i<11; i++) conds.Add("C" + i);
        Assert.Throws<InvalidWorkflowStageDecisionException>(() => new WorkflowStageDecision(Did, new WorkflowStageId(Guid.NewGuid()), new InstitutionCode("I"), WorkflowStageDecisionOutcome.ChangesRequested, "R", conds, Actor, Now));
    }
}



