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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkflowGovernanceDbContext).Assembly);
    }
}
