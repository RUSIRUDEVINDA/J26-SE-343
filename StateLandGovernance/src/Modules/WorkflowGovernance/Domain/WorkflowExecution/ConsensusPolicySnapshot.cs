using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StateLandGovernance.WorkflowGovernance.Domain.WorkflowExecution;

public sealed class ConsensusPolicySnapshot
{
    public ConsensusPolicyId PolicyId { get; }
    public string PolicyIdentifier { get; }
    public string PolicyVersion { get; }
    public LeaseCaseId LeaseCaseId { get; }
    public WorkflowPlanId WorkflowPlanId { get; }
    public int WorkflowPlanRevision { get; }
    public WorkflowRuleSetReference RuleSetReference { get; }
    public ConsensusRuleType RuleType { get; }
    public int? RequiredApprovalCount { get; }
    public DateTime GeneratedAt { get; }
    
    private readonly List<ConsensusParticipantRule> _participants;
    public IReadOnlyCollection<ConsensusParticipantRule> Participants => _participants.AsReadOnly();

    public ConsensusPolicySnapshot(
        ConsensusPolicyId policyId,
        string policyIdentifier,
        string policyVersion,
        LeaseCaseId leaseCaseId,
        WorkflowPlanId workflowPlanId,
        int workflowPlanRevision,
        WorkflowRuleSetReference ruleSetReference,
        ConsensusRuleType ruleType,
        int? requiredApprovalCount,
        DateTime generatedAt,
        IEnumerable<ConsensusParticipantRule> participants)
    {
        if (string.IsNullOrWhiteSpace(policyIdentifier)) throw new InvalidConsensusPolicyException("Policy identifier is required.");
        if (string.IsNullOrWhiteSpace(policyVersion)) throw new InvalidConsensusPolicyException("Policy version is required.");
        if (generatedAt.Kind != DateTimeKind.Utc) throw new InvalidConsensusPolicyException("GeneratedAt must be UTC.");
        
        var participantList = participants?.ToList() ?? new List<ConsensusParticipantRule>();
        if (!participantList.Any()) throw new InvalidConsensusPolicyException("Participants cannot be empty.");
        
        if (participantList.Select(p => p.WorkflowStageId).Distinct().Count() != participantList.Count)
            throw new InvalidConsensusPolicyException("Duplicate participant stage IDs are not allowed.");
            
        if (ruleType == ConsensusRuleType.Unanimous && requiredApprovalCount.HasValue)
            throw new InvalidConsensusPolicyException("Unanimous rule cannot specify an approval count.");
            
        if (ruleType == ConsensusRuleType.ApprovalThreshold)
        {
            if (!requiredApprovalCount.HasValue || requiredApprovalCount.Value <= 0)
                throw new InvalidConsensusPolicyException("ApprovalThreshold requires a positive approval count.");
            if (requiredApprovalCount.Value > participantList.Count)
                throw new InvalidConsensusPolicyException("Required approval count cannot exceed the number of participants.");
        }

        PolicyId = policyId;
        PolicyIdentifier = policyIdentifier;
        PolicyVersion = policyVersion;
        LeaseCaseId = leaseCaseId;
        WorkflowPlanId = workflowPlanId;
        WorkflowPlanRevision = workflowPlanRevision;
        RuleSetReference = ruleSetReference;
        RuleType = ruleType;
        RequiredApprovalCount = requiredApprovalCount;
        GeneratedAt = generatedAt;
        _participants = participantList;
    }
}
