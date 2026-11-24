using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;
public sealed class ComplaintHandlingConfiguration : IEntityTypeConfiguration<ComplaintHandlingEntity>
{
 public void Configure(EntityTypeBuilder<ComplaintHandlingEntity> b)
 {
  b.ToTable("complaint_handling_events","governance_intelligence",t=>{
   t.HasCheckConstraint("CK_complaint_handling_revision","\n  (\"Revision\"=1 AND \"PreviousEventId\" IS NULL) OR (\"Revision\">1 AND \"PreviousEventId\" IS NOT NULL)");
   t.HasCheckConstraint("CK_complaint_handling_operation","\"Operation\" IN\n  ('TakeDirect','AssignDs','ReassignDs','StartInvestigation','RecordNote','RequestInformation','RecordFindings','Resolve','Close','Reopen','DsReviewRecorded')");
   t.HasCheckConstraint("CK_complaint_handling_states","\n  \"FromState\" IN ('Unassigned','UnderCommissionerReview','AssignedToDs','InformationRequested','UnderInvestigation','Resolved','Closed','Reopened') AND\n  \"ToState\" IN ('Unassigned','UnderCommissionerReview','AssignedToDs','InformationRequested','UnderInvestigation','Resolved','Closed','Reopened')");
   t.HasCheckConstraint("CK_complaint_handling_responsibility","\n  (\"HandlingMode\"='Commissioner' AND \"ResponsibleActorId\" IS NOT NULL AND \"AssignmentEventId\" IS NULL AND\n   \"ToState\" IN ('UnderCommissionerReview','InformationRequested','UnderInvestigation','Reopened')) OR\n  (\"HandlingMode\"='Ds' AND \"ResponsibleActorId\" IS NOT NULL AND \"AssignmentEventId\" IS NOT NULL AND\n   \"ToState\" IN ('AssignedToDs','InformationRequested','UnderInvestigation')) OR\n  (\"HandlingMode\"='None' AND \"ResponsibleActorId\" IS NULL AND \"AssignmentEventId\" IS NULL AND \"ToState\" IN ('Resolved','Closed'))");
   t.HasCheckConstraint("CK_complaint_handling_authority","\n  (\"AuthorizationGrantId\" IS NULL AND \"AuthorizationSourceReference\" IS NULL AND \"HandlingMode\"='Ds' AND\n   \"Operation\" IN ('StartInvestigation','RecordNote','RequestInformation','RecordFindings','DsReviewRecorded')) OR\n  (\"AuthorizationGrantId\" IS NOT NULL AND \"AuthorizationSourceReference\" IS NOT NULL AND length(btrim(\"AuthorizationSourceReference\"))>0)");
   t.HasCheckConstraint("CK_complaint_handling_reason","length(btrim(\"Reason\"))>0");
   t.HasCheckConstraint("CK_complaint_handling_details","jsonb_typeof(\"DetailsJson\")='object' AND octet_length(\"DetailsJson\"::text)<=32768");
   t.HasCheckConstraint("CK_complaint_handling_evidence","\n  CASE WHEN jsonb_typeof(\"EvidenceReferencesJson\")='array' THEN jsonb_array_length(\"EvidenceReferencesJson\")<=20 ELSE false END");
   t.HasCheckConstraint("CK_complaint_handling_hashes","\n  \"InputSha256\" ~ '^[0-9a-f]{64}$' AND \"EventSha256\" ~ '^[0-9a-f]{64}$'");
   t.HasCheckConstraint("CK_complaint_handling_canonical","length(btrim(\"EventCanonicalJson\"))>0");
  });
  b.HasKey(x=>x.Id);b.Property(x=>x.Id).ValueGeneratedNever();
  b.HasAlternateKey(x=>new{x.Id,x.ComplaintId}).HasName("AK_complaint_handling_event_parent");
  b.HasAlternateKey(x=>new{x.ComplaintId,x.Revision}).HasName("UQ_complaint_handling_revision");
  b.HasAlternateKey(x=>new{x.ActorId,x.SubmissionKey}).HasName("UQ_complaint_handling_submission");
  b.HasOne<RegisteredComplaintEntity>().WithMany().HasForeignKey(x=>x.ComplaintId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_complaint_handling_complaint");
  b.HasOne<ComplaintHandlingEntity>().WithMany().HasForeignKey(x=>new{x.PreviousEventId,x.ComplaintId}).HasPrincipalKey(x=>new{x.Id,x.ComplaintId}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_complaint_handling_previous");
  b.HasOne<ComplaintAssignmentEntity>().WithMany().HasForeignKey(x=>new{x.AssignmentEventId,x.ComplaintId}).HasPrincipalKey(x=>new{x.Id,x.ComplaintId}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_complaint_handling_assignment");
  b.HasIndex(x=>x.PreviousEventId).IsUnique().HasFilter("\"PreviousEventId\" IS NOT NULL").HasDatabaseName("ux_complaint_handling_one_successor");
  b.HasIndex(x=>new{x.ResponsibleActorId,x.ComplaintId,x.Revision}).IsDescending(false,false,true).HasDatabaseName("ix_complaint_handling_responsible");
  b.HasIndex(x=>new{x.ToState,x.ComplaintId,x.Revision}).IsDescending(false,false,true).HasDatabaseName("ix_complaint_handling_status");
  b.Property(x=>x.Operation).HasMaxLength(32).IsRequired();
  b.Property(x=>x.FromState).HasMaxLength(32).IsRequired();
  b.Property(x=>x.ToState).HasMaxLength(32).IsRequired();
  b.Property(x=>x.HandlingMode).HasMaxLength(16).IsRequired();
  b.Property(x=>x.Reason).HasMaxLength(2000).IsRequired();
  b.Property(x=>x.DetailsJson).HasColumnType("jsonb").IsRequired();
  b.Property(x=>x.EvidenceReferencesJson).HasColumnType("jsonb").IsRequired();
  b.Property(x=>x.PublicSummary).HasMaxLength(2000).IsRequired();
  b.Property(x=>x.InputSha256).HasMaxLength(64).IsRequired();
  b.Property(x=>x.AuthorizationSourceReference).HasMaxLength(500);
  b.Property(x=>x.EventCanonicalJson).HasColumnType("text").IsRequired();
  b.Property(x=>x.EventSha256).HasMaxLength(64).IsRequired();
 }
}
