using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;
public sealed class InvestigationAttachmentConfiguration:IEntityTypeConfiguration<OfficerInvestigationAttachmentEntity>{
 public void Configure(EntityTypeBuilder<OfficerInvestigationAttachmentEntity>b){
  b.ToTable("officer_investigation_attachments",t=>{
   t.HasCheckConstraint("ck_investigation_attachment_identity","\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"CreatorActorId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"SubmissionKey\" <> '00000000-0000-0000-0000-000000000000'::uuid");
   t.HasCheckConstraint("ck_investigation_attachment_concern","\"Concern\" IN ('LegalDispute','UnauthorizedOccupation','UnauthorizedConstruction','FamilyOrInheritanceClaim','MultipleClaimants','UnresolvedObjection','PreviousIllegalLandActivity')");
   t.HasCheckConstraint("ck_investigation_attachment_filename","length(btrim(\"FileName\")) BETWEEN 1 AND 150 AND position('/' in \"FileName\")=0 AND position(chr(92) in \"FileName\")=0 AND position(':' in \"FileName\")=0 AND \"FileName\" !~ '[[:cntrl:]]'");
   t.HasCheckConstraint("ck_investigation_attachment_type","\"ContentType\" IN ('application/pdf','image/png','image/jpeg','text/plain')");
   t.HasCheckConstraint("ck_investigation_attachment_bytes","octet_length(\"Content\") BETWEEN 1 AND 5242880");
   t.HasCheckConstraint("ck_investigation_attachment_hashes","\"ContentSha256\" ~ '^[0-9a-f]{64}$' AND \"InputSha256\" ~ '^[0-9a-f]{64}$'");
   t.HasCheckConstraint("ck_investigation_attachment_content_hash","encode(sha256(\"Content\"),'hex')=\"ContentSha256\"");
  });
  b.HasKey(x=>x.Id).HasName("pk_investigation_attachments");b.HasAlternateKey(x=>new{x.CreatorActorId,x.SubmissionKey}).HasName("uq_investigation_attachment_submission");
  b.HasOne<OfficerInvestigationEntity>().WithMany().HasForeignKey(x=>new{x.InvestigationId,x.CreatorActorId}).HasPrincipalKey(x=>new{x.Id,x.CreatorActorId}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_investigation_attachment_owner");
  b.HasIndex(x=>new{x.InvestigationId,x.CreatedAtUtc,x.Id}).HasDatabaseName("ix_investigation_attachment_history");
  b.Property(x=>x.Concern).HasMaxLength(100);b.Property(x=>x.FileName).HasMaxLength(150);b.Property(x=>x.ContentType).HasMaxLength(50);b.Property(x=>x.ContentSha256).HasMaxLength(64);b.Property(x=>x.InputSha256).HasMaxLength(64);b.Property(x=>x.CreatedAtUtc).HasPrecision(6);
 }
}
