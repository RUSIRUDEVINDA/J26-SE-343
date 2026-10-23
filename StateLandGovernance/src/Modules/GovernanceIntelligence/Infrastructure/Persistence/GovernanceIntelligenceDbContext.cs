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
    public DbSet<RegulatorySourceEntity> RegulatorySources => Set<RegulatorySourceEntity>();
    public DbSet<ComplianceRuleEntity> ComplianceRules => Set<ComplianceRuleEntity>();
    public DbSet<RuleParameterEntity> RuleParameters => Set<RuleParameterEntity>();
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
    public DbSet<EarlyGovernanceScreeningEvaluationEntity> EarlyGovernanceScreeningEvaluations => Set<EarlyGovernanceScreeningEvaluationEntity>();
    public DbSet<EarlyGovernanceReferralEntity> EarlyGovernanceReferrals => Set<EarlyGovernanceReferralEntity>();
    public DbSet<ComplaintClassificationAssessmentEntity> ComplaintClassificationAssessments => Set<ComplaintClassificationAssessmentEntity>();
    public DbSet<GovernanceAuditAnchorReceiptEntity> GovernanceAuditAnchorReceipts => Set<GovernanceAuditAnchorReceiptEntity>();
    public DbSet<GovernanceAuditAnchorOutboxEntity> GovernanceAuditAnchorOutbox => Set<GovernanceAuditAnchorOutboxEntity>();
    public DbSet<Component1CaseProjectionEntity> Component1CaseProjections => Set<Component1CaseProjectionEntity>();
    public DbSet<Component3WorkflowProjectionEntity> Component3WorkflowProjections => Set<Component3WorkflowProjectionEntity>();
    public DbSet<WorkflowAnomalyAssessmentEntity> WorkflowAnomalyAssessments => Set<WorkflowAnomalyAssessmentEntity>();
    public DbSet<LeaseApprovalTimingAssessmentEntity> LeaseApprovalTimingAssessments => Set<LeaseApprovalTimingAssessmentEntity>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if(ChangeTracker.Entries<ScreeningEvidenceAttachmentEntity>().Any(x=>x.State is EntityState.Modified or EntityState.Deleted) || ChangeTracker.Entries<ScreeningEvidenceVerificationEntity>().Any(x=>x.State is EntityState.Modified or EntityState.Deleted) || ChangeTracker.Entries<ComplaintAssignmentEntity>().Any(x=>x.State is EntityState.Modified or EntityState.Deleted) || ChangeTracker.Entries<ComplaintHandlingEntity>().Any(x=>x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Complaint assignment history is append-only.");
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GovernanceIntelligenceDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Conventions.Replace<Microsoft.EntityFrameworkCore.Metadata.Conventions.ForeignKeyIndexConvention>(
            services => new OfficerIndexScopeConvention(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure.ProviderConventionSetBuilderDependencies>(services)));
    }

    private sealed class OfficerIndexScopeConvention(Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure.ProviderConventionSetBuilderDependencies dependencies)
        : Microsoft.EntityFrameworkCore.Metadata.Conventions.ForeignKeyIndexConvention(dependencies)
    {
        protected override Microsoft.EntityFrameworkCore.Metadata.IConventionIndex? CreateIndex(
            IReadOnlyList<Microsoft.EntityFrameworkCore.Metadata.IConventionProperty> properties, bool unique,
            Microsoft.EntityFrameworkCore.Metadata.Builders.IConventionEntityTypeBuilder entityTypeBuilder)
            => (entityTypeBuilder.Metadata.ClrType.Name.StartsWith("Officer", StringComparison.Ordinal) || entityTypeBuilder.Metadata.ClrType == typeof(ComplaintAssignmentEntity) || entityTypeBuilder.Metadata.ClrType == typeof(ComplaintHandlingEntity))
                ? null : base.CreateIndex(properties, unique, entityTypeBuilder);
    }
}
