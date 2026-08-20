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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GovernanceIntelligenceDbContext).Assembly);
    }
}
