using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;

public sealed class EarlyGovernanceReferralEntityConfiguration : IEntityTypeConfiguration<EarlyGovernanceReferralEntity>
{
    public void Configure(EntityTypeBuilder<EarlyGovernanceReferralEntity> builder)
    {
        builder.ToTable("early_governance_referrals", GovernanceIntelligenceDbContext.SchemaName);
        builder.HasKey(entity => entity.ReferralId);

        builder.Property(entity => entity.ReferralId)
            .HasColumnName("referral_id")
            .ValueGeneratedNever();
        builder.Property(entity => entity.CorrelationId)
            .HasColumnName("correlation_id")
            .ValueGeneratedNever();
        builder.Property(entity => entity.AssessmentId)
            .HasColumnName("assessment_id")
            .ValueGeneratedNever();
        builder.Property(entity => entity.CaseId)
            .HasColumnName("case_id")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(entity => entity.WorkflowRunId)
            .HasColumnName("workflow_run_id")
            .ValueGeneratedNever();
        builder.Property(entity => entity.ReasonCode)
            .HasColumnName("reason_code")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(entity => entity.Reason)
            .HasColumnName("reason")
            .HasMaxLength(2000)
            .IsRequired();
        builder.Property(entity => entity.EvidenceReferencesJson)
            .HasColumnName("evidence_references_json")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(entity => entity.RequestedAtUtc)
            .HasColumnName("requested_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(entity => entity.DeliveryState)
            .HasColumnName("delivery_state")
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(entity => entity.DeliveryAttemptCount)
            .HasColumnName("delivery_attempt_count")
            .IsRequired();
        builder.Property(entity => entity.LastAttemptAtUtc)
            .HasColumnName("last_attempt_at_utc")
            .HasColumnType("timestamp with time zone");
        builder.Property(entity => entity.AcknowledgedAtUtc)
            .HasColumnName("acknowledged_at_utc")
            .HasColumnType("timestamp with time zone");
        builder.Property(entity => entity.CommissionerReviewProcessReference)
            .HasColumnName("commissioner_review_process_reference")
            .HasMaxLength(200);
        builder.Property(entity => entity.LastFailureCode)
            .HasColumnName("last_failure_code")
            .HasMaxLength(100);

        builder.HasIndex(entity => entity.CorrelationId).IsUnique();
        builder.HasIndex(entity => entity.AssessmentId).IsUnique();
        builder.HasIndex(entity => new { entity.CaseId, entity.RequestedAtUtc });
        builder.HasIndex(entity => entity.DeliveryState);

        builder.HasOne(entity => entity.Assessment)
            .WithOne(assessment => assessment.Referral)
            .HasForeignKey<EarlyGovernanceReferralEntity>(entity => entity.AssessmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_early_governance_referrals_delivery_state",
                "delivery_state IN ('Pending', 'Delivering', 'DeliveryFailed', 'Acknowledged')");
            table.HasCheckConstraint(
                "CK_early_governance_referrals_delivery_attempt_count",
                "delivery_attempt_count >= 0");
        });
    }
}
