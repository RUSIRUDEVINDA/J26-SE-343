using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Application.Commands;

/// <summary>
/// Command to evaluate compliance for a state land action.
/// </summary>
public sealed record EvaluateComplianceCommand(string ActionName, string ActionDetails);

/// <summary>
/// Handler for the EvaluateComplianceCommand.
/// </summary>
public sealed class EvaluateComplianceCommandHandler
{
    private readonly IRegulatoryComplianceService _complianceService;
    private readonly IGovernanceAuditRepository _auditRepository;

    public EvaluateComplianceCommandHandler(
        IRegulatoryComplianceService complianceService,
        IGovernanceAuditRepository auditRepository)
    {
        _complianceService = complianceService;
        _auditRepository = auditRepository;
    }

    public async Task<bool> HandleAsync(EvaluateComplianceCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Evaluate compliance through compliance engine application service
        bool isCompliant = await _complianceService.EvaluateComplianceAsync(command.ActionDetails, cancellationToken);

        // 2. Create and persist the audit record
        var auditRecord = GovernanceAuditRecord.Create(
            EngineType.RegulatoryCompliance,
            command.ActionName,
            isCompliant ? "Compliant" : "NonCompliant",
            $"Action details evaluated. Result: {isCompliant}");

        await _auditRepository.AddAsync(auditRecord, cancellationToken);

        return isCompliant;
    }
}
