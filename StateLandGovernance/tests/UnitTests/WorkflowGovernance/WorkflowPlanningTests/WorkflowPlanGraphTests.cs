namespace StateLandGovernance.UnitTests.WorkflowGovernance.WorkflowPlanningTests;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using Xunit;

public class WorkflowPlanGraphTests
{
    private WorkflowStage CreateStage(string id, string type = "Review", string? prereq1 = null, string? prereq2 = null, bool isFinal = false)
    {
        var prereqs = new List<WorkflowStageId>();
        if (prereq1 != null) prereqs.Add(new WorkflowStageId(Guid.Parse(prereq1)));
        if (prereq2 != null) prereqs.Add(new WorkflowStageId(Guid.Parse(prereq2)));

        var stageType = type switch
        {
            "Review" => WorkflowStageType.Review,
            "Approval" => WorkflowStageType.Approval,
            "Recommendation" => WorkflowStageType.Recommendation,
            "Final" => WorkflowStageType.FinalDecision,
            _ => WorkflowStageType.Review
        };

        return new WorkflowStage(
            new WorkflowStageId(Guid.Parse(id)),
            new WorkflowStageCode("CODE-" + id.Substring(0, 8)),
            new InstitutionCode("INST"),
            stageType,
            "Officer",
            new RoutingReasonCode("RR"),
            "Reason",
            null,
            isFinal,
            prereqs
        );
    }

    private WorkflowPlan CreatePlan(IEnumerable<WorkflowStage> stages)
    {
        return new WorkflowPlan(
            new WorkflowPlanId(Guid.NewGuid()),
            new LeaseCaseId(Guid.NewGuid()),
            WorkflowPlanSource.Manual,
            new VerifiedFactSnapshotId(Guid.NewGuid()),
            new DocumentCompletenessAssessmentId(Guid.NewGuid()),
            new WorkflowRuleSetReference("R", "1"),
            null,
            DateTime.UtcNow,
            stages
        );
    }

    private static readonly string id1 = "11111111-1111-1111-1111-111111111111";
    private static readonly string id2 = "22222222-2222-2222-2222-222222222222";
    private static readonly string id3 = "33333333-3333-3333-3333-333333333333";
    private static readonly string id4 = "44444444-4444-4444-4444-444444444444";

    [Fact]
    public void EmptyStages_Throws()
    {
        Assert.Throws<InvalidWorkflowPlanException>(() => CreatePlan(Array.Empty<WorkflowStage>()));
    }

    [Fact]
    public void DuplicateStageId_Throws()
    {
        var s1 = CreateStage(id1);
        var s2 = CreateStage(id1, isFinal: true, type: "Final");
        Assert.Throws<InvalidWorkflowPlanException>(() => CreatePlan(new[] { s1, s2 }));
    }

    [Fact]
    public void DuplicateStageCode_Throws()
    {
        var s1 = new WorkflowStage(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("SAME"), new InstitutionCode("A"), WorkflowStageType.Review, "C", new RoutingReasonCode("R"), "D", null, false, Array.Empty<WorkflowStageId>());
        var s2 = new WorkflowStage(new WorkflowStageId(Guid.NewGuid()), new WorkflowStageCode("SAME"), new InstitutionCode("A"), WorkflowStageType.FinalDecision, "C", new RoutingReasonCode("R"), "D", null, true, new[] { s1.Id });
        Assert.Throws<InvalidWorkflowPlanException>(() => CreatePlan(new[] { s1, s2 }));
    }

    [Fact]
    public void UnknownPrerequisite_Throws()
    {
        var s1 = CreateStage(id1, prereq1: id2, type: "Final", isFinal: true);
        Assert.Throws<InvalidWorkflowPlanException>(() => CreatePlan(new[] { s1 }));
    }

    [Fact]
    public void SelfDependency_Throws()
    {
        Assert.Throws<InvalidWorkflowPlanException>(() => CreateStage(id1, prereq1: id1));
    }

    [Fact]
    public void DuplicatePrerequisite_Throws()
    {
        Assert.Throws<InvalidWorkflowPlanException>(() => CreateStage(id1, prereq1: id2, prereq2: id2));
    }

    [Fact]
    public void DirectCycle_Throws()
    {
        var s1 = CreateStage(id1, prereq1: id2);
        var s2 = CreateStage(id2, prereq1: id1);
        Assert.Throws<InvalidWorkflowPlanException>(() => CreatePlan(new[] { s1, s2 }));
    }

    [Fact]
    public void IndirectCycle_Throws()
    {
        var s1 = CreateStage(id1, prereq1: id3);
        var s2 = CreateStage(id2, prereq1: id1);
        var s3 = CreateStage(id3, prereq1: id2);
        Assert.Throws<InvalidWorkflowPlanException>(() => CreatePlan(new[] { s1, s2, s3 }));
    }

    [Fact]
    public void NoEntryStage_Throws()
    {
        // Cycle forms a graph with no entry
        var s1 = CreateStage(id1, prereq1: id2);
        var s2 = CreateStage(id2, prereq1: id1);
        Assert.Throws<InvalidWorkflowPlanException>(() => CreatePlan(new[] { s1, s2 }));
    }

    [Fact]
    public void MultipleEntryStages_AndParallelStages_Succeeds()
    {
        var e1 = CreateStage(id1);
        var e2 = CreateStage(id2);
        var f = CreateStage(id3, prereq1: id1, prereq2: id2, type: "Final", isFinal: true);
        var plan = CreatePlan(new[] { e1, e2, f });
        Assert.Equal(3, plan.Stages.Count);
    }

    [Fact]
    public void NoFinalStage_Throws()
    {
        var e1 = CreateStage(id1);
        Assert.Throws<InvalidWorkflowPlanException>(() => CreatePlan(new[] { e1 }));
    }

    [Fact]
    public void MultipleFinalStages_Throws()
    {
        var e1 = CreateStage(id1, type: "Final", isFinal: true);
        var e2 = CreateStage(id2, type: "Final", isFinal: true);
        Assert.Throws<InvalidWorkflowPlanException>(() => CreatePlan(new[] { e1, e2 }));
    }

    [Fact]
    public void FinalStageWithDependents_Throws()
    {
        var f = CreateStage(id1, type: "Final", isFinal: true);
        var d = CreateStage(id2, prereq1: id1);
        Assert.Throws<InvalidWorkflowPlanException>(() => CreatePlan(new[] { f, d }));
    }

    [Fact]
    public void DisconnectedStages_Throws()
    {
        var e1 = CreateStage(id1);
        var f1 = CreateStage(id2, prereq1: id1, type: "Final", isFinal: true);
        var e2 = CreateStage(id3); // Unreachable to final
        Assert.Throws<InvalidWorkflowPlanException>(() => CreatePlan(new[] { e1, f1, e2 }));
    }

    [Fact]
    public void NonFinalCannotReachFinal_Throws()
    {
        var e1 = CreateStage(id1);
        var e2 = CreateStage(id2, prereq1: id1); // Stops here
        var f1 = CreateStage(id3, prereq1: id1, type: "Final", isFinal: true);
        // e2 does not reach final
        Assert.Throws<InvalidWorkflowPlanException>(() => CreatePlan(new[] { e1, e2, f1 }));
    }

    [Fact]
    public void FinalMarkerAndTypeConsistency_Checked()
    {
        Assert.Throws<InvalidWorkflowPlanException>(() => CreateStage(id1, type: "Review", isFinal: true));
        Assert.Throws<InvalidWorkflowPlanException>(() => CreateStage(id1, type: "Final", isFinal: false));
    }

    [Fact]
    public void DeterministicTopologicalOrdering_AndDefensiveCopying()
    {
        var s2 = CreateStage(id2);
        var s1 = CreateStage(id1);
        var s4 = CreateStage(id4, prereq1: id1, prereq2: id2, type: "Final", isFinal: true);
        var s3 = CreateStage(id3, prereq1: id2);

        // id3 cannot reach final! Wait, let's fix it so it reaches final.
        s3 = CreateStage(id3, prereq1: id2);
        s4 = CreateStage(id4, prereq1: id1, prereq2: id3, type: "Final", isFinal: true);

        // s1 and s2 are entries.
        // Code-based tie breaking: id1 and id2 are entries. 
        var plan = CreatePlan(new[] { s2, s1, s4, s3 });

        var list = new List<WorkflowStage>(plan.Stages);
        Assert.Equal(id1, list[0].Id.Value.ToString());
        Assert.Equal(id2, list[1].Id.Value.ToString());
        Assert.Equal(id3, list[2].Id.Value.ToString());
        Assert.Equal(id4, list[3].Id.Value.ToString());
    }
    
    [Fact]
    public void ExternalMutation_ConstructorCollections_IsDefensive()
    {
        var prereqsList = new List<WorkflowStageId> { new WorkflowStageId(Guid.Parse(id1)) };
        var s2 = new WorkflowStage(new WorkflowStageId(Guid.Parse(id2)), new WorkflowStageCode("CODE"), new InstitutionCode("INST"), WorkflowStageType.Review, "Cap", new RoutingReasonCode("R"), "D", null, false, prereqsList);
        
        prereqsList.Add(new WorkflowStageId(Guid.Parse(id3)));
        Assert.Single(s2.Prerequisites);
        
        var s1 = CreateStage(id1);
        var s3 = CreateStage(id3, type: "Final", isFinal: true, prereq1: id2);
        var stagesList = new List<WorkflowStage> { s1, s2, s3 };
        
        var plan = CreatePlan(stagesList);
        stagesList.Clear();
        
        Assert.Equal(3, plan.Stages.Count);
    }

}
