namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

/// <summary>
/// Entity Framework Core DbContext for Component 3 (WorkflowGovernance) persistence boundary.
/// Encapsulated strictly within the Infrastructure layer.
/// Uses dedicated PostgreSQL schema "workflow_governance".
/// </summary>
public class WorkflowGovernanceDbContext : DbContext
{
    public const string SchemaName = "workflow_governance";

    public WorkflowGovernanceDbContext(DbContextOptions<WorkflowGovernanceDbContext> options)
        : base(options)
    {
    }

    public DbSet<LeaseCaseEntity> LeaseCases => Set<LeaseCaseEntity>();
    public DbSet<CompletenessDefinitionEntity> CompletenessDefinitions => Set<CompletenessDefinitionEntity>();
    public DbSet<GovernedDocumentEntity> GovernedDocuments => Set<GovernedDocumentEntity>();
    public DbSet<DocumentVersionEntity> DocumentVersions => Set<DocumentVersionEntity>();
    public DbSet<DocumentAnalysisEntity> DocumentAnalyses => Set<DocumentAnalysisEntity>();
    public DbSet<AnalysisRunEntity> AnalysisRuns => Set<AnalysisRunEntity>();
    public DbSet<HumanFactVerificationEntity> HumanFactVerifications => Set<HumanFactVerificationEntity>();
    public DbSet<VerifiedFactSnapshotEntity> VerifiedFactSnapshots => Set<VerifiedFactSnapshotEntity>();
    public DbSet<DocumentCompletenessAssessmentEntity> DocumentCompletenessAssessments => Set<DocumentCompletenessAssessmentEntity>();
    public DbSet<WorkflowPlanEntity> WorkflowPlans => Set<WorkflowPlanEntity>();
    public DbSet<WorkflowExecutionEntity> WorkflowExecutions => Set<WorkflowExecutionEntity>();
    public DbSet<InstitutionalRoutingConfigEntity> InstitutionalRoutingConfigs => Set<InstitutionalRoutingConfigEntity>();

    public DbSet<CaseAssignmentEntity> CaseAssignments => Set<CaseAssignmentEntity>();
    public DbSet<IntakeJurisdictionPolicyEntity> IntakeJurisdictionPolicies => Set<IntakeJurisdictionPolicyEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkflowGovernanceDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<IntakeJurisdictionPolicyEntity>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Jurisdiction policies are append-only. Publish a new version.");
        if (ChangeTracker.Entries<InstitutionalRoutingConfigEntity>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Routing definitions are append-only. Publish a new version.");
        if (ChangeTracker.Entries<CompletenessDefinitionEntity>().Any(x =>
            x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Completeness definitions are append-only. Publish a new version.");
        return base.SaveChangesAsync(cancellationToken);
    }
}
