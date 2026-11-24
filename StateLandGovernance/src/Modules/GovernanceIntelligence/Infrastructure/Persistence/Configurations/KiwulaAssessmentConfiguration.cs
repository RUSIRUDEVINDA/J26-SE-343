using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;
public sealed class KiwulaAssessmentConfiguration : IEntityTypeConfiguration<KiwulaAssessmentEntity>
{
    public void Configure(EntityTypeBuilder<KiwulaAssessmentEntity> b)
    {
        b.ToTable("kiwula_assessment_provenance", GovernanceIntelligenceDbContext.SchemaName, t => t.HasCheckConstraint("CK_kiwula_engine_link", "(\"Engine\" = 'conflict' AND \"ConflictEvaluationId\" IS NOT NULL AND \"EarlyAssessmentId\" IS NULL) OR (\"Engine\" = 'early' AND \"EarlyAssessmentId\" IS NOT NULL AND \"ConflictEvaluationId\" IS NULL)"));
        b.HasKey(x => x.Id); b.Property(x => x.Engine).HasMaxLength(16); b.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        b.HasIndex(x => new { x.CreatorActorId, x.SubmissionKey }).IsUnique();
        b.HasOne<ConflictEvaluationEntity>().WithMany().HasForeignKey(x => x.ConflictEvaluationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EarlyGovernanceScreeningEvaluationEntity>().WithMany().HasForeignKey(x => x.EarlyAssessmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
