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

/// <summary>
/// Command to evaluate governance risk observations and detect potential risk indicators.
/// </summary>
public sealed record EvaluateGovernanceRiskCommand(
    string ActionName,
    GovernanceRiskEvaluationInputDto Input
);

/// <summary>
/// Handler for the EvaluateGovernanceRiskCommand.
/// </summary>
public sealed class EvaluateGovernanceRiskCommandHandler
{
    private const string RiskDisclaimer = "This governance risk assessment is an automated decision-support indicator for human review and does not constitute a finding of legal wrongdoing or legal fraud.";

    private readonly IGovernanceRiskEngine _riskEngine;
    private readonly IGovernanceAuditRepository _auditRepository;
    private readonly TimeProvider _timeProvider;

    public EvaluateGovernanceRiskCommandHandler(
        IGovernanceRiskEngine riskEngine,
        IGovernanceAuditRepository auditRepository,
        TimeProvider timeProvider)
    {
        _riskEngine = riskEngine ?? throw new ArgumentNullException(nameof(riskEngine));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<GovernanceRiskAssessmentResultDto> HandleAsync(EvaluateGovernanceRiskCommand command, CancellationToken cancellationToken = default)
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
            throw new ArgumentException("Risk evaluation input cannot be null.", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.Input.SubjectId))
        {
            throw new ArgumentException("Subject identifier cannot be null or whitespace.", nameof(command));
        }

        // Obtain UTC timestamp from TimeProvider
        var utcTimestamp = _timeProvider.GetUtcNow().UtcDateTime;

        // 1. Map DTO inputs to Domain Value Objects
        var domainInput = MapToDomainInput(command.Input);

        // 2. Evaluate risk using the Domain Engine
        var domainResult = _riskEngine.AssessRisk(domainInput, utcTimestamp);

        // 3. Record audit trail using summary metadata only
        int totalObservations = (domainInput.DecisionHistory?.Count ?? 0) +
                                (domainInput.ComplianceViolations?.Count ?? 0) +
                                (domainInput.GovernanceConflicts?.Count ?? 0) +
                                (domainInput.Complaints?.Count ?? 0) +
                                (domainInput.InstitutionalValidations?.Count ?? 0);

        var auditStatus = domainResult.RequiresHumanReview ? "ElevatedRiskDetected" : "LowRisk";
        var auditDetails = $"Evaluated subject '{domainResult.SubjectId}'. Evaluated {totalObservations} observations. Triggered indicators: {domainResult.TriggeredIndicators.Count}. Score: {domainResult.OverallRiskScore} ({domainResult.Severity}).";

        var auditRecord = GovernanceAuditRecord.Create(
            EngineType.RiskAndCorruption,
            command.ActionName,
            auditStatus,
            auditDetails,
            utcTimestamp);

        await _auditRepository.AddAsync(auditRecord, cancellationToken);

        // 4. Map Domain Result to Response DTO
        var indicatorDtos = domainResult.TriggeredIndicators.Select(i => new GovernanceRiskIndicatorDto(
            i.IndicatorId,
            i.Category.ToString(),
            i.ScoreContribution,
            i.Severity.ToString(),
            i.SubjectId,
            i.InvolvedActors,
            i.EvidenceSummary,
            i.TriggeredRule,
            i.Explanation,
            i.RecommendedAction)).ToList();

        return new GovernanceRiskAssessmentResultDto(
            domainResult.SubjectId,
            domainResult.OverallRiskScore,
            domainResult.Severity.ToString(),
            domainResult.RequiresHumanReview,
            RiskDisclaimer,
            indicatorDtos,
            utcTimestamp);
    }

    private static GovernanceRiskEvaluationInput MapToDomainInput(GovernanceRiskEvaluationInputDto dto)
    {
        var decisions = (dto.DecisionHistory ?? Array.Empty<DecisionHistoryObservationDto>())
            .Where(d => d is not null)
            .Select(d => new DecisionHistoryObservation(
                d.DecisionId, d.SubjectId, d.OfficerId, d.InstitutionId, d.DecisionType, d.Timestamp, d.IsOverride, d.IsException))
            .ToList();

        var violations = (dto.ComplianceViolations ?? Array.Empty<ComplianceViolationEvidenceDto>())
            .Where(v => v is not null)
            .Select(v => new ComplianceViolationEvidence(
                v.ViolationId, v.SubjectId, v.RuleId, v.Category, v.Timestamp))
            .ToList();

        var conflicts = (dto.GovernanceConflicts ?? Array.Empty<GovernanceConflictEvidenceDto>())
            .Where(c => c is not null)
            .Select(c => new GovernanceConflictEvidence(
                c.ConflictId, c.SubjectId, c.ConflictType, c.Severity, c.Timestamp))
            .ToList();

        var complaints = (dto.Complaints ?? Array.Empty<ComplaintObservationDto>())
            .Where(c => c is not null)
            .Select(c => new ComplaintObservation(
                c.ComplaintId, c.SubjectId, c.FilingTimestamp, c.Category, c.SeverityLevel, c.VerificationStatus))
            .ToList();

        var validations = (dto.InstitutionalValidations ?? Array.Empty<InstitutionalValidationObservationDto>())
            .Where(v => v is not null)
            .Select(v => new InstitutionalValidationObservation(
                v.ValidationId, v.SubjectId, v.InstitutionId, v.IsValidated, v.FailureReason, v.Timestamp))
            .ToList();

        return new GovernanceRiskEvaluationInput(
            dto.SubjectId,
            decisions,
            violations,
            conflicts,
            complaints,
            validations);
    }
}
