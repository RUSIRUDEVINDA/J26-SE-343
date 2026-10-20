namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
public sealed class ConsolidatedScreeningEntityConfiguration : IEntityTypeConfiguration<ConsolidatedScreeningEntity>
{
    public void Configure(EntityTypeBuilder<ConsolidatedScreeningEntity> b)
    {
        b.ToTable("consolidated_screening_assessments", GovernanceIntelligenceDbContext.SchemaName);
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.CaseId).HasColumnName("case_id").HasMaxLength(100).IsRequired();
        b.Property(x => x.ProposalVersion).HasColumnName("proposal_version").HasMaxLength(200).IsRequired();
        b.Property(x => x.SnapshotSha256).HasColumnName("snapshot_sha256").HasMaxLength(64).IsRequired();
        b.Property(x => x.EvaluatedAtUtc).HasColumnName("evaluated_at_utc").IsRequired();
        b.Property(x => x.ResultJson).HasColumnName("result_json").HasColumnType("jsonb").IsRequired();
        b.HasIndex(x => new { x.CaseId, x.ProposalVersion, x.SnapshotSha256 }).IsUnique();
        b.HasIndex(x => new { x.CaseId, x.EvaluatedAtUtc });
    }
}
