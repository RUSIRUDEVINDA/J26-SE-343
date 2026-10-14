namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

using System;
using System.Collections.Generic;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

public sealed record CompletedWorkflowDecision(
    WorkflowExecutionId ExecutionId,
    WorkflowPlanId PlanId,
    int PlanRevision,
    LeaseCaseId LeaseCaseId,
    ConsensusAssessmentId ConsensusAssessmentId,
    WorkflowStageDecisionId FinalDecisionId,
    WorkflowStageDecisionOutcome FinalDecisionOutcome,
    InstitutionCode FinalDecisionInstitution,
    Guid DecidingOfficerId,
    DateTime DecidedAtUtc,
    IReadOnlyCollection<string> Conditions);
