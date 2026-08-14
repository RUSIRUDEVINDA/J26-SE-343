using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StateLandGovernance.GovernanceIntelligence.Application.Commands;

using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

public sealed record EvaluateGovernanceConsensusCommand(
    string ActionName,
    GovernanceConsensusInputDto Input
);

public sealed class EvaluateGovernanceConsensusCommandHandler
{
    private readonly IGovernanceConsensusEngine _engine;
    private readonly IGovernanceAuditRepository _auditRepository;
    private readonly TimeProvider _timeProvider;

    public EvaluateGovernanceConsensusCommandHandler(
        IGovernanceConsensusEngine engine,
        IGovernanceAuditRepository auditRepository,
        TimeProvider? timeProvider = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<GovernanceConsensusResultDto> HandleAsync(
        EvaluateGovernanceConsensusCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.ActionName))
        {
            throw new ArgumentException("Action name cannot be null or empty.", nameof(command));
        }

        if (command.Input is null)
        {
            throw new ArgumentNullException(nameof(command), "Consensus input cannot be null.");
        }

        var policyDto = command.Input.Policy ?? throw new ArgumentException("Consensus policy cannot be null.", nameof(command));
        
        if (!Enum.TryParse<ConsensusType>(policyDto.Mode, true, out var consensusMode))
        {
            throw new ArgumentException($"Invalid consensus mode: '{policyDto.Mode}'.", nameof(command));
        }

        var policy = new GovernanceConsensusPolicy(
            mode: consensusMode,
            expectedInstitutionIds: policyDto.ExpectedInstitutionIds,
            requiredPercentage: policyDto.RequiredPercentage,
            requiredApprovalCount: policyDto.RequiredApprovalCount,
            minQuorumPercentage: policyDto.MinQuorumPercentage,
            allowConditionalAsApproval: policyDto.AllowConditionalAsApproval,
            countAbstentionsInQuorum: policyDto.CountAbstentionsInQuorum,
            isRejectionBlocking: policyDto.IsRejectionBlocking,
            mandatoryInstitutionIds: policyDto.MandatoryInstitutionIds
        );

        var domainPositions = (command.Input.Positions ?? new System.Collections.Generic.List<InstitutionalGovernancePositionDto>())
            .Select(p =>
            {
                if (!Enum.TryParse<InstitutionalPositionType>(p.Position, true, out var posEnum))
                {
                    throw new ArgumentException($"Invalid institutional position: '{p.Position}'.", nameof(command));
                }

                return new InstitutionalGovernancePosition(
                    institutionId: p.InstitutionId,
                    position: posEnum,
                    authorityRole: p.AuthorityRole,
                    reasonCode: p.ReasonCode,
                    summaryNotes: p.SummaryNotes,
                    submittedTimestamp: p.SubmittedTimestamp
                );
            })
            .ToList();

        var evaluationTimestamp = _timeProvider.GetUtcNow().UtcDateTime;

        var domainResult = _engine.EvaluateConsensus(
            subjectId: command.Input.SubjectId,
            positions: domainPositions,
            policy: policy,
            evaluationTimestamp: evaluationTimestamp
        );

        // Safe aggregate privacy-compliant audit detail
        string auditDetails = $"Evaluated consensus across {domainResult.TotalExpectedInstitutions} expected institutions. Submitted: {domainResult.SubmittedCount}, Participating: {domainResult.ParticipatingCount}, Missing: {domainResult.MissingCount}, Approvals: {domainResult.ApprovalCount}, Rejections: {domainResult.RejectionCount}. Quorum: {domainResult.QuorumSatisfied}, Outcome: {domainResult.Outcome}.";

        var auditRecord = GovernanceAuditRecord.Create(
            engineType: EngineType.Consensus,
            actionName: command.ActionName.Trim(),
            status: domainResult.Outcome.ToString(),
            details: auditDetails,
            timestamp: evaluationTimestamp
        );

        await _auditRepository.AddAsync(auditRecord, cancellationToken);

        return new GovernanceConsensusResultDto(
            ConsensusEvaluationId: domainResult.ConsensusEvaluationId,
            SubjectId: domainResult.SubjectId,
            Outcome: domainResult.Outcome.ToString(),
            TotalExpectedInstitutions: domainResult.TotalExpectedInstitutions,
            SubmittedCount: domainResult.SubmittedCount,
            ParticipatingCount: domainResult.ParticipatingCount,
            MissingCount: domainResult.MissingCount,
            ApprovalCount: domainResult.ApprovalCount,
            RejectionCount: domainResult.RejectionCount,
            ConditionalApprovalCount: domainResult.ConditionalApprovalCount,
            AbstentionCount: domainResult.AbstentionCount,
            PendingCount: domainResult.PendingCount,
            QuorumSatisfied: domainResult.QuorumSatisfied,
            MandatoryInstitutionsSatisfied: domainResult.MandatoryInstitutionsSatisfied,
            ConsensusThresholdSatisfied: domainResult.ConsensusThresholdSatisfied,
            BlockingInstitutionCount: domainResult.BlockingInstitutionCount,
            SummaryExplanation: domainResult.SummaryExplanation,
            RecommendedAction: domainResult.RecommendedAction,
            EvaluationTimestamp: domainResult.EvaluationTimestamp
        );
    }
}
