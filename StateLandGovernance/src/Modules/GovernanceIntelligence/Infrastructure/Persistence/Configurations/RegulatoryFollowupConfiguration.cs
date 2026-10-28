using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;

public sealed class RegulatoryFollowupConfiguration : IEntityTypeConfiguration<RegulatoryFollowupEntity>
{
    public void Configure(EntityTypeBuilder<RegulatoryFollowupEntity> b)
    {
        b.ToTable("regulatory_followup_events", t =>
        {
            t.HasCheckConstraint("ck_regulatory_followup_operation", "(\"Operation\"='ClarificationRequested' AND \"ReviewOutcome\" IS NULL AND \"MissingInformation\" IS NOT NULL AND length(\"MissingInformation\") BETWEEN 20 AND 2000) OR (\"Operation\"='HumanReviewRecorded' AND \"MissingInformation\" IS NULL AND \"ReviewOutcome\" IS NOT NULL AND \"ReviewOutcome\" IN ('NoAdditionalConcern','AdditionalInformationRequired','FurtherInstitutionalLegalReview')) OR (\"Operation\"='NoFollowupRequired' AND \"ReviewOutcome\" IS NULL AND \"MissingInformation\" IS NULL) OR (\"Operation\"='FindingVerifiedClosed' AND \"ReviewOutcome\" IS NULL AND \"MissingInformation\" IS NULL) ");
            t.HasCheckConstraint("ck_regulatory_followup_remarks", "length(\"Remarks\") <= 2000 AND (\"Operation\"='FindingVerifiedClosed' OR length(\"Remarks\")>=20)");
            t.HasCheckConstraint("ck_regulatory_followup_refs", "jsonb_typeof(\"ReferencesJson\")='array' AND jsonb_array_length(\"ReferencesJson\")<=10 AND NOT jsonb_path_exists(\"ReferencesJson\", '$[*] ? (@.type() != \"string\")')");
            t.HasCheckConstraint("ck_regulatory_followup_hash", "\"InputSha256\" ~ '^[0-9a-f]{64}$' AND \"EventSha256\" ~ '^[0-9a-f]{64}$'");
            t.HasCheckConstraint("ck_regulatory_followup_status", "\"Status\"='Recorded'");
            t.HasCheckConstraint("ck_regulatory_followup_revision", "(\"Revision\"=1 AND \"PreviousEventId\" IS NULL) OR (\"Revision\">1 AND \"PreviousEventId\" IS NOT NULL)");
            t.HasCheckConstraint("ck_regulatory_followup_rule", "length(\"RuleCode\")>0 AND length(\"RuleVersion\")>0");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.Id, x.EvaluationId, x.RuleCode });
        b.HasIndex(x => new { x.ActorId, x.SubmissionKey }).IsUnique();
        b.HasIndex(x => new { x.EvaluationId, x.RuleCode, x.Revision }).IsUnique();
        b.HasIndex(x => new { x.EvaluationId, x.RuleCode }).IsUnique().HasDatabaseName("ux_regulatory_finding_closure").HasFilter("\"Operation\"='FindingVerifiedClosed'");
        b.HasIndex(x => x.PreviousEventId).IsUnique();
        b.HasIndex(x => new { x.EvaluationId, x.CreatedAtUtc });
        b.Property(x => x.RuleCode).HasMaxLength(100); b.Property(x => x.RuleVersion).HasMaxLength(100);
        b.Property(x => x.Operation).HasMaxLength(32); b.Property(x => x.ReviewOutcome).HasMaxLength(48);
        b.Property(x => x.MissingInformation).HasMaxLength(2000); b.Property(x => x.Remarks).HasMaxLength(2000);
        b.Property(x => x.Status).HasMaxLength(16); b.Property(x => x.ReferencesJson).HasColumnType("jsonb");
        b.Property(x => x.InputSha256).HasMaxLength(64); b.Property(x => x.EventSha256).HasMaxLength(64);
        b.HasOne<ComplianceEvaluationEntity>().WithMany().HasForeignKey(x => x.EvaluationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RegulatoryFollowupEntity>().WithMany().HasForeignKey(x => new { x.PreviousEventId, x.EvaluationId, x.RuleCode })
            .HasPrincipalKey(x => new { x.Id, x.EvaluationId, x.RuleCode }).OnDelete(DeleteBehavior.Restrict);
    }
}
