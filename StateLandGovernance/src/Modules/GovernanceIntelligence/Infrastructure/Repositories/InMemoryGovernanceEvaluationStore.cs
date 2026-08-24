using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Mappings;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

public class InMemoryGovernanceEvaluationStore : IGovernanceEvaluationStore
{
    private readonly IGovernanceAuditRepository _auditRepository;

    private readonly ConcurrentBag<ComplianceEvaluationEntity> _complianceEvaluations = new();
    private readonly ConcurrentBag<ConflictEvaluationEntity> _conflictEvaluations = new();
    private readonly ConcurrentBag<RiskEvaluationEntity> _riskEvaluations = new();
    private readonly ConcurrentBag<GovernanceExplanationEvaluationEntity> _explanationEvaluations = new();
    private readonly ConcurrentBag<ConsensusEvaluationEntity> _consensusEvaluations = new();
    private readonly ConcurrentBag<ConditionalVerificationEvaluationEntity> _verificationEvaluations = new();

    public InMemoryGovernanceEvaluationStore(IGovernanceAuditRepository auditRepository)
    {
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
    }

    public async Task StoreComplianceEvaluationAsync(GovernanceAuditRecord auditRecord, ComplianceResult result, string actionName, CancellationToken cancellationToken = default)
    {
        if (auditRecord == null) throw new ArgumentNullException(nameof(auditRecord));
        if (result == null) throw new ArgumentNullException(nameof(result));

        await _auditRepository.AddAsync(auditRecord, cancellationToken);
        var (_, evalEntity) = ComplianceEvaluationMapper.MapToEntities(auditRecord, result, actionName);
        _complianceEvaluations.Add(evalEntity);
    }

    public async Task StoreConflictEvaluationAsync(GovernanceAuditRecord auditRecord, IReadOnlyList<DetectedConflict> conflicts, string actionName, int totalEvaluatedDecisions, CancellationToken cancellationToken = default)
    {
        if (auditRecord == null) throw new ArgumentNullException(nameof(auditRecord));
        if (conflicts == null) throw new ArgumentNullException(nameof(conflicts));

        await _auditRepository.AddAsync(auditRecord, cancellationToken);
        var (_, evalEntity) = ConflictEvaluationMapper.MapToEntities(auditRecord, conflicts, actionName, totalEvaluatedDecisions);
        _conflictEvaluations.Add(evalEntity);
    }

    public async Task StoreRiskEvaluationAsync(GovernanceAuditRecord auditRecord, GovernanceRiskAssessmentResult result, CancellationToken cancellationToken = default)
    {
        if (auditRecord == null) throw new ArgumentNullException(nameof(auditRecord));
        if (result == null) throw new ArgumentNullException(nameof(result));

        await _auditRepository.AddAsync(auditRecord, cancellationToken);
        var (_, evalEntity) = RiskEvaluationMapper.MapToEntities(auditRecord, result);
        _riskEvaluations.Add(evalEntity);
    }

    public async Task StoreExplanationEvaluationAsync(GovernanceAuditRecord auditRecord, GovernanceExplanationResult result, CancellationToken cancellationToken = default)
    {
        if (auditRecord == null) throw new ArgumentNullException(nameof(auditRecord));
        if (result == null) throw new ArgumentNullException(nameof(result));

        await _auditRepository.AddAsync(auditRecord, cancellationToken);
        var (_, evalEntity) = GovernanceExplanationMapper.MapToEntities(auditRecord, result);
        _explanationEvaluations.Add(evalEntity);
    }

    public async Task StoreConsensusEvaluationAsync(GovernanceAuditRecord auditRecord, GovernanceConsensusResult result, IReadOnlyList<InstitutionalGovernancePosition> positions, CancellationToken cancellationToken = default)
    {
        if (auditRecord == null) throw new ArgumentNullException(nameof(auditRecord));
        if (result == null) throw new ArgumentNullException(nameof(result));

        await _auditRepository.AddAsync(auditRecord, cancellationToken);
        var (_, evalEntity) = ConsensusEvaluationMapper.MapToEntities(auditRecord, result, positions);
        _consensusEvaluations.Add(evalEntity);
    }

    public async Task StoreConditionalVerificationEvaluationAsync(GovernanceAuditRecord auditRecord, ConditionalVerificationResult result, CancellationToken cancellationToken = default)
    {
        if (auditRecord == null) throw new ArgumentNullException(nameof(auditRecord));
        if (result == null) throw new ArgumentNullException(nameof(result));

        await _auditRepository.AddAsync(auditRecord, cancellationToken);
        var (_, evalEntity) = ConditionalVerificationMapper.MapToEntities(auditRecord, result);
        _verificationEvaluations.Add(evalEntity);
    }
}
