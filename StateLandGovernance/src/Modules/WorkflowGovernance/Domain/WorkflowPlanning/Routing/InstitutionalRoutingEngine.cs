namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning.Routing;

using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

public static class InstitutionalRoutingEngine
{
    public static readonly InstitutionCode DivisionalSecretariat = new("DS");
    public static readonly InstitutionCode LandCommissioner = new("LC");

    public static IReadOnlyCollection<WorkflowStage> GeneratePlanStages(
        IEnumerable<InstitutionCode> requiredInstitutions,
        bool requiresCommissionerApproval)
    {
        if (requiredInstitutions == null)
        {
            throw new ArgumentNullException(nameof(requiredInstitutions), "Required institutions collection cannot be null.");
        }

        var standardCodes = requiredInstitutions
            .Where(i => !string.Equals(i.Value, DivisionalSecretariat.Value, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(i.Value, LandCommissioner.Value, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(i => i.Value)
            .ToList();

        var standardStages = new List<WorkflowStage>();
        foreach (var inst in standardCodes)
        {
            var stageId = new WorkflowStageId(Guid.NewGuid());
            var stageCode = new WorkflowStageCode($"REVIEW_{inst.Value}");
            var stage = new WorkflowStage(
                stageId,
                stageCode,
                inst,
                WorkflowStageType.Review,
                "InstitutionalReviewer",
                new RoutingReasonCode("MANDATORY_REVIEW"),
                $"Mandatory review by institution {inst.Value}",
                targetDurationDays: 14,
                isFinalDecision: false,
                prerequisites: Array.Empty<WorkflowStageId>()
            );
            standardStages.Add(stage);
        }

        var dsPrereqs = standardStages.Select(s => s.Id).ToList();
        var dsStageId = new WorkflowStageId(Guid.NewGuid());
        var dsStageCode = new WorkflowStageCode("DS_CONCURRENCE");
        var dsStage = new WorkflowStage(
            dsStageId,
            dsStageCode,
            DivisionalSecretariat,
            requiresCommissionerApproval ? WorkflowStageType.Recommendation : WorkflowStageType.FinalDecision,
            "DivisionalSecretary",
            new RoutingReasonCode("DS_CLEARANCE"),
            "Divisional Secretariat assessment and concurrence",
            targetDurationDays: 14,
            isFinalDecision: !requiresCommissionerApproval,
            prerequisites: dsPrereqs
        );

        var stages = new List<WorkflowStage>(standardStages) { dsStage };

        if (requiresCommissionerApproval)
        {
            var commissionerStageId = new WorkflowStageId(Guid.NewGuid());
            var commissionerStageCode = new WorkflowStageCode("COMMISSIONER_FINAL_DECISION");
            var commissionerStage = new WorkflowStage(
                commissionerStageId,
                commissionerStageCode,
                LandCommissioner,
                WorkflowStageType.FinalDecision,
                "LandCommissioner",
                new RoutingReasonCode("FINAL_STATUTORY_APPROVAL"),
                "Final statutory lease approval by Land Commissioner",
                targetDurationDays: 7,
                isFinalDecision: true,
                prerequisites: new[] { dsStageId }
            );
            stages.Add(commissionerStage);
        }

        return stages.AsReadOnly();
    }
}
