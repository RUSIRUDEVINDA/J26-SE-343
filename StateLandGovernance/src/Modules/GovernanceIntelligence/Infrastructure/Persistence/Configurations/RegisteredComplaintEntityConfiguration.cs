using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;
public sealed class RegisteredComplaintEntityConfiguration : IEntityTypeConfiguration<RegisteredComplaintEntity>
{
    public void Configure(EntityTypeBuilder<RegisteredComplaintEntity> b){
        b.ToTable("registered_complaints","governance_intelligence");b.HasKey(x=>x.Id);b.Property(x=>x.Id).ValueGeneratedNever();
        b.HasIndex(x=>new{x.ApplicantId,x.SubmissionKey}).IsUnique();b.HasIndex(x=>new{x.CaseId,x.SubmittedAtUtc});
        b.Property(x=>x.Revision).IsConcurrencyToken();b.Property(x=>x.PayloadJson).HasColumnType("jsonb").IsRequired();
    }
}
public sealed class RegisteredComplaintAssessmentLinkConfiguration : IEntityTypeConfiguration<RegisteredComplaintAssessmentLinkEntity>
{
    public void Configure(EntityTypeBuilder<RegisteredComplaintAssessmentLinkEntity> b){
        b.ToTable("registered_complaint_classifications","governance_intelligence");b.HasKey(x=>x.AssessmentId);
        b.HasOne<RegisteredComplaintEntity>().WithMany().HasForeignKey(x=>x.ComplaintId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ComplaintClassificationAssessmentEntity>().WithMany().HasForeignKey(x=>x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
