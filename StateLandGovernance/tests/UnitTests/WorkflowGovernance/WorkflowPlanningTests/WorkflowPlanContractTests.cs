namespace StateLandGovernance.UnitTests.WorkflowGovernance.WorkflowPlanningTests;

using System;
using System.Linq;
using System.Reflection;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using Xunit;

public class WorkflowPlanContractTests
{
    [Fact]
    public void WorkflowPlanId_Empty_Throws()
    {
        Assert.Throws<InvalidWorkflowPlanException>(() => new WorkflowPlanId(Guid.Empty));
    }

    [Fact]
    public void WorkflowStageCode_EmptyOrControl_Throws()
    {
        Assert.Throws<InvalidWorkflowPlanException>(() => new WorkflowStageCode(""));
        Assert.Throws<InvalidWorkflowPlanException>(() => new WorkflowStageCode("A\nB"));
        Assert.Throws<InvalidWorkflowPlanException>(() => new WorkflowStageCode(new string('a', 101)));
    }

    [Fact]
    public void InstitutionCode_Normalizes_UpperAndTrim()
    {
        var code = new InstitutionCode(" abc ");
        Assert.Equal("ABC", code.Value);
        Assert.Throws<InvalidWorkflowPlanException>(() => new InstitutionCode("a b"));
    }

    [Fact]
    public void RecommendationReference_EmptyModelOrIdentifier_Throws()
    {
        var modelRef = new AnalysisModelReference("P", "m1", "v1");
        Assert.Throws<InvalidWorkflowPlanException>(() => new WorkflowRecommendationReference("", modelRef, DateTime.UtcNow, null));
        Assert.Throws<InvalidWorkflowPlanException>(() => new WorkflowRecommendationReference("id", null!, DateTime.UtcNow, null));
    }

    [Fact]
    public void RuleSetReference_EmptyOrControl_Throws()
    {
        Assert.Throws<InvalidWorkflowPlanException>(() => new WorkflowRuleSetReference("", "v1"));
        Assert.Throws<InvalidWorkflowPlanException>(() => new WorkflowRuleSetReference("id", ""));
        Assert.Throws<InvalidWorkflowPlanException>(() => new WorkflowRuleSetReference("i\td", "v1"));
    }

    [Fact]
    public void WorkflowPlan_NoPublicSetters()
    {
        var props = typeof(WorkflowPlan).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in props)
        {
            var setter = prop.GetSetMethod(false);
            Assert.Null(setter);
        }
    }

    [Fact]
    public void WorkflowStage_NoPublicSetters()
    {
        var props = typeof(WorkflowStage).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in props)
        {
            var setter = prop.GetSetMethod(false);
            Assert.Null(setter);
        }
    }

    [Fact]
    public void RecommendationReference_GeneratedAtNotUtc_Throws()
    {
        var modelRef = new AnalysisModelReference("P", "m", "v");
        Assert.Throws<InvalidWorkflowPlanException>(() => new WorkflowRecommendationReference("id", modelRef, DateTime.Now, null));
    }

    [Fact]
    public void WorkflowStage_StringsBounded()
    {
        var tooLong = new string('a', 600);
        Assert.Throws<InvalidWorkflowPlanException>(() => new WorkflowStage(
            new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), 
            WorkflowStageType.Review, tooLong, new RoutingReasonCode("R"), "reason", null, false, Array.Empty<WorkflowStageId>()));

        Assert.Throws<InvalidWorkflowPlanException>(() => new WorkflowStage(
            new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("C"), new InstitutionCode("I"), 
            WorkflowStageType.Review, "Cap", new RoutingReasonCode("R"), tooLong, null, false, Array.Empty<WorkflowStageId>()));
    }
}
