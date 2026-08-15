using System;
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

/// <summary>
/// Command to synthesize transparent governance explanations from summarized evidence contracts.
/// </summary>
public sealed record GenerateGovernanceExplanationCommand(
    string ActionName,
    GovernanceExplanationInputDto Input
);

/// <summary>
/// Command handler for GenerateGovernanceExplanationCommand.
/// </summary>
public sealed class GenerateGovernanceExplanationCommandHandler
{
    private readonly IExplainableGovernanceEngine _explanationEngine;
    private readonly IGovernanceAuditRepository _auditRepository;
    private readonly TimeProvider _timeProvider;

    public GenerateGovernanceExplanationCommandHandler(
        IExplainableGovernanceEngine explanationEngine,
        IGovernanceAuditRepository auditRepository,
        TimeProvider timeProvider)
    {
        _explanationEngine = explanationEngine ?? throw new ArgumentNullException(nameof(explanationEngine));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<GovernanceExplanationResultDto> HandleAsync(GenerateGovernanceExplanationCommand command, CancellationToken cancellationToken = default)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command), "Command details cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(command.ActionName))
        {
            throw new ArgumentException("Action name cannot be empty.", nameof(command));
        }

        if (command.Input is null)
        {
            throw new ArgumentException("Explanation input cannot be null.", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.Input.SubjectId))
        {
            throw new ArgumentException("Subject ID cannot be null or whitespace.", nameof(command));
        }

        var utcTimestamp = _timeProvider.GetUtcNow().UtcDateTime;

        // 1. Map DTO to Phase 5-owned Domain Value Objects
        var domainInput = MapToDomainInput(command.Input);

        // 2. Synthesize explanation using Domain Engine
        var domainResult = _explanationEngine.SynthesizeExplanation(domainInput, utcTimestamp);

        // 3. Record Audit Metadata (Privacy Safe: NO raw SubjectId, NO PII, NO sensitive payloads)
        int sourceCount = (domainInput.ComplianceEvidence != null ? 1 : 0) +
                          (domainInput.ConflictEvidence != null ? 1 : 0) +
                          (domainInput.RiskEvidence != null ? 1 : 0);

        string auditStatus = domainResult.RequiresHumanReview ? "ElevatedGovernanceAttentionRequired" : "ExplanationGenerated";
        string auditDetails = $"Evaluation completed across {sourceCount} source engines. Generated {domainResult.Explanations.Count} explanation items. Highest severity: {domainResult.OverallSeverity}. Human review required: {domainResult.RequiresHumanReview}.";

        var auditRecord = GovernanceAuditRecord.Create(
            EngineType.ExplainableGovernance,
            command.ActionName,
            auditStatus,
            auditDetails,
            utcTimestamp);

        await _auditRepository.AddAsync(auditRecord, cancellationToken);

        // 4. Map Domain Result to DTO Response
        var itemDtos = domainResult.Explanations.Select(i => new GovernanceExplanationItemDto(
            i.ItemId,
            i.SourceEngine.ToString(),
            i.OutcomeStatus,
            i.Severity.ToString(),
            i.ReasonCode,
            i.Title,
            i.PlainLanguageExplanation,
            i.EvidenceSummaries,
            i.RecommendedAction,
            i.RequiresHumanAttention)).ToList();

        return new GovernanceExplanationResultDto(
            domainResult.ExplanationId,
            domainResult.SubjectId,
            domainResult.OverallSeverity.ToString(),
            domainResult.RequiresHumanReview,
            itemDtos,
            utcTimestamp,
            domainResult.Disclaimer);
    }

    private static GovernanceExplanationInput MapToDomainInput(GovernanceExplanationInputDto dto)
    {
        ComplianceExplanationEvidence? complianceEvidence = null;
        if (dto.ComplianceEvidence != null)
        {
            var violations = (dto.ComplianceEvidence.ViolatedRules ?? Array.Empty<ComplianceViolationSummaryDto>())
                .Where(v => v != null)
                .Select(v => new ComplianceViolationSummary(v.RuleCode, v.RuleCategory, v.SummaryMessage))
                .ToList();

            complianceEvidence = new ComplianceExplanationEvidence(
                dto.ComplianceEvidence.Status,
                violations,
                dto.ComplianceEvidence.UnsatisfiedConditions);
        }

        ConflictExplanationEvidence? conflictEvidence = null;
        if (dto.ConflictEvidence != null)
        {
            var conflicts = (dto.ConflictEvidence.Conflicts ?? Array.Empty<ConflictSummaryDto>())
                .Where(c => c != null)
                .Select(c => new ConflictSummary(
                    c.ConflictType,
                    ParseSeverity(c.Severity),
                    c.SummaryMessage,
                    c.EvidenceRuleCode,
                    c.RecommendedAction))
                .ToList();

            conflictEvidence = new ConflictExplanationEvidence(conflicts);
        }

        RiskExplanationEvidence? riskEvidence = null;
        if (dto.RiskEvidence != null)
        {
            var indicators = (dto.RiskEvidence.TriggeredIndicators ?? Array.Empty<RiskIndicatorSummaryDto>())
                .Where(i => i != null)
                .Select(i => new RiskIndicatorSummary(
                    i.IndicatorCode,
                    i.Category,
                    ParseSeverity(i.Severity),
                    i.EvidenceSummary,
                    i.TriggeredRule,
                    i.RecommendedAction))
                .ToList();

            riskEvidence = new RiskExplanationEvidence(
                dto.RiskEvidence.OverallScore,
                ParseSeverity(dto.RiskEvidence.RiskSeverity),
                dto.RiskEvidence.RequiresHumanReview,
                indicators);
        }

        return new GovernanceExplanationInput(
            dto.SubjectId,
            complianceEvidence,
            conflictEvidence,
            riskEvidence);
    }

    private static GovernanceExplanationSeverity ParseSeverity(string? severityStr)
    {
        if (Enum.TryParse<GovernanceExplanationSeverity>(severityStr, true, out var parsed))
        {
            return parsed;
        }

        return GovernanceExplanationSeverity.Moderate;
    }
}
