using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;
public sealed class PostWorkflowDemonstrationConfiguration : IEntityTypeConfiguration<PostWorkflowDemonstrationEntity>
{
    public void Configure(EntityTypeBuilder<PostWorkflowDemonstrationEntity> b)
    {
        b.ToTable("post_workflow_demonstrations", GovernanceIntelligenceDbContext.SchemaName);
        b.HasKey(x => x.Id);
        b.Property(x => x.Scenario).HasMaxLength(64);
        b.Property(x => x.Version).HasMaxLength(64);
        b.Property(x => x.State).HasMaxLength(16);
        b.Property(x => x.Failure).HasMaxLength(512);
        b.Property(x => x.AttemptsJson).HasColumnType("jsonb");
        b.HasIndex(x => new { x.CreatorActorId, x.Scenario, x.Version }).IsUnique();
        b.HasIndex(x => x.WorkflowRunId).IsUnique();
    }
}
