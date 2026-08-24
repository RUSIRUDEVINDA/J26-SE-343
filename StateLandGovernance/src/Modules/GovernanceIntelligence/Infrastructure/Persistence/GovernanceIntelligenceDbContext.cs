using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for Component 4 (GovernanceIntelligence) persistence boundary.
/// Encapsulated strictly within the Infrastructure layer.
/// </summary>
public class GovernanceIntelligenceDbContext : DbContext
{
    public const string SchemaName = "governance_intelligence";

    public GovernanceIntelligenceDbContext(DbContextOptions<GovernanceIntelligenceDbContext> options)
        : base(options)
    {
    }

    public DbSet<GovernanceAuditRecordEntity> GovernanceAuditRecords => Set<GovernanceAuditRecordEntity>();
    public DbSet<ComplianceEvaluationEntity> ComplianceEvaluations => Set<ComplianceEvaluationEntity>();
    public DbSet<ComplianceViolationEntity> ComplianceViolations => Set<ComplianceViolationEntity>();
    public DbSet<ComplianceConditionEntity> ComplianceConditions => Set<ComplianceConditionEntity>();
    public DbSet<ConflictEvaluationEntity> ConflictEvaluations => Set<ConflictEvaluationEntity>();
    public DbSet<ConflictFindingEntity> ConflictFindings => Set<ConflictFindingEntity>();
    public DbSet<RiskEvaluationEntity> RiskEvaluations => Set<RiskEvaluationEntity>();
    public DbSet<RiskIndicatorEntity> RiskIndicators => Set<RiskIndicatorEntity>();
    public DbSet<GovernanceExplanationEvaluationEntity> GovernanceExplanations => Set<GovernanceExplanationEvaluationEntity>();
    public DbSet<GovernanceExplanationItemEntity> GovernanceExplanationItems => Set<GovernanceExplanationItemEntity>();
    public DbSet<ConsensusEvaluationEntity> ConsensusEvaluations => Set<ConsensusEvaluationEntity>();
    public DbSet<InstitutionPositionEntity> InstitutionPositions => Set<InstitutionPositionEntity>();
    public DbSet<ConditionalVerificationEvaluationEntity> ConditionalVerificationEvaluations => Set<ConditionalVerificationEvaluationEntity>();
    public DbSet<ConditionResultEntity> ConditionResults => Set<ConditionResultEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GovernanceIntelligenceDbContext).Assembly);
    }
}
