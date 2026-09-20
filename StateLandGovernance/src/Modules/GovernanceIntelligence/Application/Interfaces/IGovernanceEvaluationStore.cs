using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

/// <summary>
/// Application abstraction for persisting structured governance evaluations alongside governance audit records.
/// </summary>
public interface IGovernanceEvaluationStore
{
    Task StoreComplianceEvaluationAsync(GovernanceAuditRecord auditRecord, ComplianceResult result, string actionName, CancellationToken cancellationToken = default);
    Task StoreComplianceEvaluationAsync(GovernanceAuditRecord auditRecord, ComplianceResult result, string actionName, string? proposalId, CancellationToken cancellationToken = default);
    Task StoreConflictEvaluationAsync(GovernanceAuditRecord auditRecord, IReadOnlyList<DetectedConflict> conflicts, string actionName, int totalEvaluatedDecisions, CancellationToken cancellationToken = default);
    Task StoreRiskEvaluationAsync(GovernanceAuditRecord auditRecord, GovernanceRiskAssessmentResult result, CancellationToken cancellationToken = default);
    Task StoreExplanationEvaluationAsync(GovernanceAuditRecord auditRecord, GovernanceExplanationResult result, CancellationToken cancellationToken = default);
    Task StoreConsensusEvaluationAsync(GovernanceAuditRecord auditRecord, GovernanceConsensusResult result, IReadOnlyList<InstitutionalGovernancePosition> positions, CancellationToken cancellationToken = default);
    Task StoreConditionalVerificationEvaluationAsync(GovernanceAuditRecord auditRecord, ConditionalVerificationResult result, CancellationToken cancellationToken = default);
}
