using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;

public sealed class ComplaintAssignmentConfiguration : IEntityTypeConfiguration<ComplaintAssignmentEntity>
{
    public void Configure(EntityTypeBuilder<ComplaintAssignmentEntity> b)
    {
        b.ToTable("complaint_assignment_events", "governance_intelligence", t =>
        {
            t.HasCheckConstraint("CK_complaint_assignment_revision", "\"Revision\">0");
            t.HasCheckConstraint("CK_complaint_assignment_chain", "(\"Revision\"=1 AND \"PreviousAssignmentId\" IS NULL) OR (\"Revision\">1 AND \"PreviousAssignmentId\" IS NOT NULL)");
            t.HasCheckConstraint("CK_complaint_assignment_reason", "length(btrim(\"Reason\"))>0");
            t.HasCheckConstraint("CK_complaint_assignment_office", "length(btrim(\"TargetOfficeCode\"))>0");
            t.HasCheckConstraint("CK_complaint_assignment_authority", "length(btrim(\"AuthorizationSourceReference\"))>0");
            t.HasCheckConstraint("CK_complaint_assignment_canonical", "length(btrim(\"EventCanonicalJson\"))>0");
            t.HasCheckConstraint("CK_complaint_assignment_hashes", "\"InputSha256\" ~ '^[0-9a-f]{64}$' AND \"DirectorySnapshotSha256\" ~ '^[0-9a-f]{64}$' AND \"EventSha256\" ~ '^[0-9a-f]{64}$'");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.HasAlternateKey(x => new { x.Id, x.ComplaintId });
        b.HasAlternateKey(x => new { x.ComplaintId, x.Revision }).HasName("UQ_complaint_assignment_revision");
        b.HasAlternateKey(x => new { x.AssignedByActorId, x.SubmissionKey }).HasName("UQ_complaint_assignment_submission");
        b.HasOne<RegisteredComplaintEntity>().WithMany().HasForeignKey(x => x.ComplaintId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_complaint_assignment_complaint");
        b.HasOne<ComplaintAssignmentEntity>().WithMany().HasForeignKey(x => new { x.PreviousAssignmentId, x.ComplaintId })
            .HasPrincipalKey(x => new { x.Id, x.ComplaintId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_complaint_assignment_previous");
        b.HasIndex(x => x.PreviousAssignmentId).IsUnique().HasFilter("\"PreviousAssignmentId\" IS NOT NULL").HasDatabaseName("ux_complaint_assignment_one_successor");
        b.HasIndex(x => new { x.TargetActorId, x.ComplaintId, x.Revision }).IsDescending(false, false, true).HasDatabaseName("ix_complaint_assignment_target");
        b.Property(x => x.TargetOfficeCode).HasMaxLength(100).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
        b.Property(x => x.AuthorizationSourceReference).HasMaxLength(500).IsRequired();
        b.Property(x => x.InputSha256).HasMaxLength(64).IsRequired();
        b.Property(x => x.DirectorySnapshotSha256).HasMaxLength(64).IsRequired();
        b.Property(x => x.EventSha256).HasMaxLength(64).IsRequired();
        b.Property(x => x.EventCanonicalJson).HasColumnType("text").IsRequired();
    }
}
