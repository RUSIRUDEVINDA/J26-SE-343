using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;

public sealed class ComplaintClassificationAssessmentEntityConfiguration
    : IEntityTypeConfiguration<ComplaintClassificationAssessmentEntity>
{
    public void Configure(EntityTypeBuilder<ComplaintClassificationAssessmentEntity> builder)
    {
        builder.ToTable(
            "complaint_classification_assessments",
            GovernanceIntelligenceDbContext.SchemaName,
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "CK_complaint_classification_assessments_probability_range",
                    "administrative_procedural_integrity_probability >= 0 AND administrative_procedural_integrity_probability <= 1 " +
                    "AND lease_revenue_payment_enforcement_probability >= 0 AND lease_revenue_payment_enforcement_probability <= 1 " +
                    "AND unauthorized_allocation_transfer_use_probability >= 0 AND unauthorized_allocation_transfer_use_probability <= 1 " +
                    "AND protected_environmental_lease_misuse_probability >= 0 AND protected_environmental_lease_misuse_probability <= 1");
                tableBuilder.HasCheckConstraint(
                    "CK_complaint_classification_assessments_probability_sum",
                    "abs(administrative_procedural_integrity_probability + lease_revenue_payment_enforcement_probability + " +
                    "unauthorized_allocation_transfer_use_probability + protected_environmental_lease_misuse_probability - 1.0) <= 0.000001");
            });

        builder.HasKey(entity => entity.AssessmentId);

        builder.Property(entity => entity.AssessmentId)
            .HasColumnName("assessment_id")
            .ValueGeneratedNever();

        builder.Property(entity => entity.CaseId)
            .HasColumnName("case_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(entity => entity.ComplaintText)
            .HasColumnName("complaint_text")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(entity => entity.ModelVersion)
            .HasColumnName("model_version")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(entity => entity.PredictedCategory)
            .HasColumnName("predicted_category")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(entity => entity.AdministrativeProceduralIntegrityProbability)
            .HasColumnName("administrative_procedural_integrity_probability")
            .HasPrecision(18, 17)
            .IsRequired();

        builder.Property(entity => entity.LeaseRevenuePaymentEnforcementProbability)
            .HasColumnName("lease_revenue_payment_enforcement_probability")
            .HasPrecision(18, 17)
            .IsRequired();

        builder.Property(entity => entity.UnauthorizedAllocationTransferUseProbability)
            .HasColumnName("unauthorized_allocation_transfer_use_probability")
            .HasPrecision(18, 17)
            .IsRequired();

        builder.Property(entity => entity.ProtectedEnvironmentalLeaseMisuseProbability)
            .HasColumnName("protected_environmental_lease_misuse_probability")
            .HasPrecision(18, 17)
            .IsRequired();

        builder.Property(entity => entity.AdvisoryNote)
            .HasColumnName("advisory_note")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(entity => entity.ClosedSetNote)
            .HasColumnName("closed_set_note")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(entity => entity.AssessedAtUtc)
            .HasColumnName("assessed_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}
