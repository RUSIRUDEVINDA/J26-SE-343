using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Application.Commands;

public sealed record EvaluateConditionalVerificationCommand(
    string ActionName,
    ConditionalVerificationInputDto Input
);

public sealed class EvaluateConditionalVerificationCommandHandler
{
    private readonly IConditionalGovernanceVerificationEngine _engine;
    private readonly IGovernanceEvaluationStore _evaluationStore;
    private readonly TimeProvider _timeProvider;

    public EvaluateConditionalVerificationCommandHandler(
        IConditionalGovernanceVerificationEngine engine,
        IGovernanceEvaluationStore evaluationStore,
        TimeProvider? timeProvider = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _evaluationStore = evaluationStore ?? throw new ArgumentNullException(nameof(evaluationStore));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ConditionalVerificationResultDto> HandleAsync(
        EvaluateConditionalVerificationCommand command,
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
            throw new ArgumentNullException(nameof(command), "Verification input cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(command.Input.SubjectId))
        {
            throw new ArgumentException("Subject ID cannot be null or empty.", nameof(command));
        }

        if (command.Input.Conditions is null || command.Input.Conditions.Count == 0)
        {
            throw new ArgumentException("Conditions collection cannot be null or empty.", nameof(command));
        }

        var domainConditions = command.Input.Conditions
            .Select(c => new GovernanceCondition(
                conditionId: c.ConditionId,
                title: c.Title,
                category: c.Category,
                isMandatory: c.IsMandatory,
                maxEvidenceAgeDays: c.MaxEvidenceAgeDays,
                description: c.Description
            ))
            .ToList();

        var domainEvidence = (command.Input.Evidence ?? new List<VerificationEvidenceDto>())
            .Select(e =>
            {
                if (!Enum.TryParse<SuppliedEvidenceStatus>(e.ProvidedStatus, true, out var statusEnum))
                {
                    throw new ArgumentException($"Invalid provided evidence status: '{e.ProvidedStatus}'.", nameof(command));
                }

                return new VerificationEvidence(
                    conditionId: e.ConditionId,
                    evidenceId: e.EvidenceId,
                    providedStatus: statusEnum,
                    evidenceTimestamp: e.EvidenceTimestamp,
                    expiryTimestamp: e.ExpiryTimestamp,
                    issuerOrAuthority: e.IssuerOrAuthority,
                    remarks: e.Remarks
                );
            })
            .ToList();

        var policyDto = command.Input.Policy ?? new ConditionalVerificationPolicyDto();
        var domainPolicy = new ConditionalVerificationPolicy(
            AllowProvisionalVerification: policyDto.AllowProvisionalVerification
        );

        var evaluationTimestamp = _timeProvider.GetUtcNow().UtcDateTime;

        var domainResult = _engine.EvaluateVerification(
            subjectId: command.Input.SubjectId,
            conditions: domainConditions,
            evidenceList: domainEvidence,
            policy: domainPolicy,
            evaluationTimestamp: evaluationTimestamp
        );

        // Safe aggregate privacy-compliant audit detail (no raw evidence remarks or officer PII)
        string auditDetails = $"Evaluated conditional verification across {domainResult.TotalConditionsCount} conditions ({domainResult.MandatoryConditionsCount} mandatory). Satisfied mandatory: {domainResult.SatisfiedMandatoryCount}, Unsatisfied mandatory: {domainResult.UnsatisfiedMandatoryCount}, Pending: {domainResult.PendingConditionsCount}, Outcome: {domainResult.Outcome}.";

        var auditRecord = GovernanceAuditRecord.Create(
            engineType: EngineType.ConditionalVerification,
            actionName: command.ActionName.Trim(),
            status: domainResult.Outcome.ToString(),
            details: auditDetails,
            timestamp: evaluationTimestamp
        );

        await _evaluationStore.StoreConditionalVerificationEvaluationAsync(auditRecord, domainResult, cancellationToken);

        var statusDtos = domainResult.ConditionStatuses
            .Select(s => new ConditionVerificationStatusDto(
                ConditionId: s.ConditionId,
                IsMandatory: s.IsMandatory,
                Status: s.Status.ToString(),
                IsSatisfied: s.IsSatisfied,
                FailureReason: s.FailureReason,
                Notes: s.Notes
            ))
            .ToList();

        return new ConditionalVerificationResultDto(
            VerificationId: domainResult.VerificationId,
            SubjectId: domainResult.SubjectId,
            Outcome: domainResult.Outcome.ToString(),
            TotalConditionsCount: domainResult.TotalConditionsCount,
            MandatoryConditionsCount: domainResult.MandatoryConditionsCount,
            SatisfiedMandatoryCount: domainResult.SatisfiedMandatoryCount,
            UnsatisfiedMandatoryCount: domainResult.UnsatisfiedMandatoryCount,
            SatisfiedOptionalCount: domainResult.SatisfiedOptionalCount,
            PendingConditionsCount: domainResult.PendingConditionsCount,
            MissingConditionsCount: domainResult.MissingConditionsCount,
            ExpiredConditionsCount: domainResult.ExpiredConditionsCount,
            ConditionStatuses: statusDtos,
            SummaryExplanation: domainResult.SummaryExplanation,
            RecommendedAction: domainResult.RecommendedAction,
            EvaluationTimestamp: domainResult.EvaluationTimestamp
        );
    }
}
