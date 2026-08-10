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
/// Command to evaluate regulatory compliance for a proposed lease.
/// </summary>
public sealed record EvaluateComplianceCommand(
    string ActionName,
    int LeaseDurationYears,
    string ProposedUse,
    decimal LeaseAmount,
    string ZoningArea
);

/// <summary>
/// Handler for the EvaluateComplianceCommand.
/// </summary>
public sealed class EvaluateComplianceCommandHandler
{
    private readonly IRegulatoryRuleProvider _ruleProvider;
    private readonly IRegulatoryComplianceEngine _complianceEngine;
    private readonly IGovernanceAuditRepository _auditRepository;

    public EvaluateComplianceCommandHandler(
        IRegulatoryRuleProvider ruleProvider,
        IRegulatoryComplianceEngine complianceEngine,
        IGovernanceAuditRepository auditRepository)
    {
        _ruleProvider = ruleProvider;
        _complianceEngine = complianceEngine;
        _auditRepository = auditRepository;
    }

    public async Task<ComplianceResultDto> HandleAsync(EvaluateComplianceCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Fetch active regulatory rules
        var rules = await _ruleProvider.GetActiveRulesAsync(cancellationToken);

        // 2. Build input value object
        var input = new LeaseEvaluationInput(
            command.LeaseDurationYears,
            command.ProposedUse,
            command.LeaseAmount,
            command.ZoningArea);

        // 3. Evaluate compliance using domain compliance engine
        var result = _complianceEngine.Evaluate(input, rules);

        // 4. Create and persist audit record
        var auditRecord = GovernanceAuditRecord.Create(
            EngineType.RegulatoryCompliance,
            command.ActionName,
            result.Status.ToString(),
            $"Violations: {result.Violations.Count}, Conditions: {result.Conditions.Count}");

        await _auditRepository.AddAsync(auditRecord, cancellationToken);

        // 5. Map to result DTO
        var violationDtos = result.Violations
            .Select(v => new ViolationDto(v.RuleCode, v.Message))
            .ToList();

        var conditionDtos = result.Conditions
            .Select(c => new ComplianceConditionDto(c.Description, c.RequiredByDate))
            .ToList();

        return new ComplianceResultDto(
            result.Status.ToString(),
            violationDtos,
            conditionDtos);
    }
}
