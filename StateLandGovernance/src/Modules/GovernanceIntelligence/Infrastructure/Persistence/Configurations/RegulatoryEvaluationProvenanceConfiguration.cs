using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;
public sealed class RegulatoryEvaluationProvenanceConfiguration:IEntityTypeConfiguration<RegulatoryEvaluationProvenanceEntity>{
 public void Configure(EntityTypeBuilder<RegulatoryEvaluationProvenanceEntity> b){
  b.ToTable("regulatory_evaluation_provenance","governance_intelligence");b.HasKey(x=>x.EvaluationId);b.Property(x=>x.EvaluationId).ValueGeneratedNever();
  b.HasIndex(x=>new{x.CreatorActorId,x.SubmissionKey}).IsUnique();b.Property(x=>x.SubmissionHash).HasMaxLength(64).IsRequired();b.Property(x=>x.ProvenanceJson).HasColumnType("jsonb").IsRequired();
  b.HasOne<ComplianceEvaluationEntity>().WithMany().HasForeignKey(x=>x.EvaluationId).OnDelete(DeleteBehavior.Restrict);
 }
}
