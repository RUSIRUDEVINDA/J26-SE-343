using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

public sealed class ConsensusAssessment
{
    public ConsensusAssessmentId AssessmentId { get; }
    public WorkflowExecutionId WorkflowExecutionId { get; }
    public WorkflowPlanId WorkflowPlanId { get; }
    public int WorkflowPlanRevision { get; }
    public LeaseCaseId LeaseCaseId { get; }
    public WorkflowRuleSetReference RuleSetReference { get; }
    public ConsensusPolicyId PolicyId { get; }
    public string PolicyIdentifier { get; }
    public string PolicyVersion { get; }
    public DateTime PolicyGeneratedAt { get; }
    public ConsensusRuleType RuleType { get; }
    public int? RequiredApprovalCount { get; }
    public DateTime EvaluatedAt { get; }
    public ConsensusOutcome Outcome { get; }
    public int ParticipantCount { get; }
    public int ApprovalCount { get; }
    public int ConditionalApprovalCount { get; }
    public int RejectionCount { get; }
    public int ChangesRequestedCount { get; }
    public int AbstentionCount { get; }
    public bool BlockingRejectionDetected { get; }

    private readonly List<ConsensusReasonCode> _reasonCodes;
    public IReadOnlyCollection<ConsensusReasonCode> ReasonCodes => _reasonCodes.AsReadOnly();

    private readonly List<ConsensusParticipantResult> _participantResults;
    public IReadOnlyCollection<ConsensusParticipantResult> ParticipantResults => _participantResults.AsReadOnly();

    internal ConsensusAssessment(
        ConsensusAssessmentId assessmentId,
        WorkflowExecutionId workflowExecutionId,
        WorkflowPlanId workflowPlanId,
        int workflowPlanRevision,
        LeaseCaseId leaseCaseId,
        WorkflowRuleSetReference ruleSetReference,
        ConsensusPolicyId policyId,
        string policyIdentifier,
        string policyVersion,
        DateTime policyGeneratedAt,
        ConsensusRuleType ruleType,
        int? requiredApprovalCount,
        DateTime evaluatedAt,
        ConsensusOutcome outcome,
        int participantCount,
        int approvalCount,
        int conditionalApprovalCount,
        int rejectionCount,
        int changesRequestedCount,
        int abstentionCount,
        bool blockingRejectionDetected,
        IEnumerable<ConsensusReasonCode> reasonCodes,
        IEnumerable<ConsensusParticipantResult> participantResults)
    {
        AssessmentId = assessmentId;
        WorkflowExecutionId = workflowExecutionId;
        WorkflowPlanId = workflowPlanId;
        WorkflowPlanRevision = workflowPlanRevision;
        LeaseCaseId = leaseCaseId;
        RuleSetReference = ruleSetReference;
        PolicyId = policyId;
        PolicyIdentifier = policyIdentifier;
        PolicyVersion = policyVersion;
        PolicyGeneratedAt = policyGeneratedAt;
        RuleType = ruleType;
        RequiredApprovalCount = requiredApprovalCount;
        EvaluatedAt = evaluatedAt;
        Outcome = outcome;
        ParticipantCount = participantCount;
        ApprovalCount = approvalCount;
        ConditionalApprovalCount = conditionalApprovalCount;
        RejectionCount = rejectionCount;
        ChangesRequestedCount = changesRequestedCount;
        AbstentionCount = abstentionCount;
        BlockingRejectionDetected = blockingRejectionDetected;
        _reasonCodes = reasonCodes.ToList();
        _participantResults = participantResults.ToList();
    }
}

