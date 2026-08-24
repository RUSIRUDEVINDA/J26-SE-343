using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Mappings;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

public class PostgresGovernanceEvaluationStore : IGovernanceEvaluationStore
{
    private readonly GovernanceIntelligenceDbContext _dbContext;

    public PostgresGovernanceEvaluationStore(GovernanceIntelligenceDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task StoreComplianceEvaluationAsync(GovernanceAuditRecord auditRecord, ComplianceResult result, string actionName, CancellationToken cancellationToken = default)
    {
        if (auditRecord == null) throw new ArgumentNullException(nameof(auditRecord));
        if (result == null) throw new ArgumentNullException(nameof(result));

        var (auditEntity, evalEntity) = ComplianceEvaluationMapper.MapToEntities(auditRecord, result, actionName);

        await _dbContext.GovernanceAuditRecords.AddAsync(auditEntity, cancellationToken);
        await _dbContext.ComplianceEvaluations.AddAsync(evalEntity, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task StoreConflictEvaluationAsync(GovernanceAuditRecord auditRecord, IReadOnlyList<DetectedConflict> conflicts, string actionName, int totalEvaluatedDecisions, CancellationToken cancellationToken = default)
    {
        if (auditRecord == null) throw new ArgumentNullException(nameof(auditRecord));
        if (conflicts == null) throw new ArgumentNullException(nameof(conflicts));

        var (auditEntity, evalEntity) = ConflictEvaluationMapper.MapToEntities(auditRecord, conflicts, actionName, totalEvaluatedDecisions);

        await _dbContext.GovernanceAuditRecords.AddAsync(auditEntity, cancellationToken);
        await _dbContext.ConflictEvaluations.AddAsync(evalEntity, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task StoreRiskEvaluationAsync(GovernanceAuditRecord auditRecord, GovernanceRiskAssessmentResult result, CancellationToken cancellationToken = default)
    {
        if (auditRecord == null) throw new ArgumentNullException(nameof(auditRecord));
        if (result == null) throw new ArgumentNullException(nameof(result));

        var (auditEntity, evalEntity) = RiskEvaluationMapper.MapToEntities(auditRecord, result);

        await _dbContext.GovernanceAuditRecords.AddAsync(auditEntity, cancellationToken);
        await _dbContext.RiskEvaluations.AddAsync(evalEntity, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task StoreExplanationEvaluationAsync(GovernanceAuditRecord auditRecord, GovernanceExplanationResult result, CancellationToken cancellationToken = default)
    {
        if (auditRecord == null) throw new ArgumentNullException(nameof(auditRecord));
        if (result == null) throw new ArgumentNullException(nameof(result));

        var (auditEntity, evalEntity) = GovernanceExplanationMapper.MapToEntities(auditRecord, result);

        await _dbContext.GovernanceAuditRecords.AddAsync(auditEntity, cancellationToken);
        await _dbContext.GovernanceExplanations.AddAsync(evalEntity, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task StoreConsensusEvaluationAsync(GovernanceAuditRecord auditRecord, GovernanceConsensusResult result, IReadOnlyList<InstitutionalGovernancePosition> positions, CancellationToken cancellationToken = default)
    {
        if (auditRecord == null) throw new ArgumentNullException(nameof(auditRecord));
        if (result == null) throw new ArgumentNullException(nameof(result));

        var (auditEntity, evalEntity) = ConsensusEvaluationMapper.MapToEntities(auditRecord, result, positions);

        await _dbContext.GovernanceAuditRecords.AddAsync(auditEntity, cancellationToken);
        await _dbContext.ConsensusEvaluations.AddAsync(evalEntity, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task StoreConditionalVerificationEvaluationAsync(GovernanceAuditRecord auditRecord, ConditionalVerificationResult result, CancellationToken cancellationToken = default)
    {
        if (auditRecord == null) throw new ArgumentNullException(nameof(auditRecord));
        if (result == null) throw new ArgumentNullException(nameof(result));

        var (auditEntity, evalEntity) = ConditionalVerificationMapper.MapToEntities(auditRecord, result);

        await _dbContext.GovernanceAuditRecords.AddAsync(auditEntity, cancellationToken);
        await _dbContext.ConditionalVerificationEvaluations.AddAsync(evalEntity, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
