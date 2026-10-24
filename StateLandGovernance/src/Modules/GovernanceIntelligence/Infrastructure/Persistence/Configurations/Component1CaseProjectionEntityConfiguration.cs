using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="Component1CaseProjectionEntity"/> in schema <c>governance_intelligence</c>.
/// </summary>
public sealed class Component1CaseProjectionEntityConfiguration
    : IEntityTypeConfiguration<Component1CaseProjectionEntity>
{
    public const string TableName = "component1_case_projections";

    public void Configure(EntityTypeBuilder<Component1CaseProjectionEntity> builder)
    {
        builder.ToTable(TableName, GovernanceIntelligenceDbContext.SchemaName, table =>
        {
            table.HasCheckConstraint(
                "ck_c1_case_projections_case_id_not_blank",
                "length(btrim(case_id)) > 0");

            table.HasCheckConstraint(
                "ck_c1_case_projections_land_parcel_id_not_blank",
                "length(btrim(land_parcel_id)) > 0");

            table.HasCheckConstraint(
                "ck_c1_case_projections_extent_ha_positive",
                "extent_ha > 0");

            table.HasCheckConstraint(
                "ck_c1_case_projections_ownership_evidence_status",
                "ownership_evidence_status IN ('EvidenceProvided', 'EvidenceMissing', 'EvidenceConflicting', 'EvidenceIncomplete')");

            table.HasCheckConstraint(
                "ck_c1_case_projections_protected_zone_overlap_state",
                "protected_zone_overlap_state IN ('NoProtectedZoneOverlap', 'ProtectedZoneOverlapDetected', 'ProtectedZoneEvidenceIncomplete', 'ProtectedZoneCheckNotPerformed')");

            table.HasCheckConstraint(
                "ck_c1_case_projections_boundary_confirmation_status",
                "boundary_confirmation_status IN ('BoundaryConfirmed', 'BoundaryPendingSurvey', 'BoundaryDisputed', 'BoundaryEvidenceMissing')");

            table.HasCheckConstraint(
                "ck_c1_case_projections_input_sha256_len",
                "char_length(input_sha256) = 64");

            table.HasCheckConstraint(
                "ck_c1_case_projections_provenance_kind",
                "provenance_kind IN ('RealComponent1', 'RealComponent3', 'SyntheticFixture', 'SyntheticPp1Seed')");

            table.HasCheckConstraint(
                "ck_c1_case_projections_purpose",
                "purpose IN ('PP1Demonstration', 'AutomatedTest', 'LocalDevelopment', 'ProductionPeerIntegration')");

            table.HasCheckConstraint(
                "ck_c1_case_projections_synthetic_consistency",
                "(is_synthetic = true AND provenance_kind IN ('SyntheticFixture', 'SyntheticPp1Seed') AND synthetic_seed_id IS NOT NULL AND length(btrim(synthetic_seed_id)) > 0 AND synthetic_seed_version IS NOT NULL AND length(btrim(synthetic_seed_version)) > 0) " +
                "OR (is_synthetic = false AND provenance_kind NOT IN ('SyntheticFixture', 'SyntheticPp1Seed') AND synthetic_seed_id IS NULL AND synthetic_seed_version IS NULL)");
        });

        builder.HasKey(x => x.ProjectionId);

        builder.Property(x => x.ProjectionId)
            .HasColumnName("projection_id")
            .ValueGeneratedNever();

        builder.Property(x => x.CaseId)
            .HasColumnName("case_id")
            .HasMaxLength(Component1GovernanceCaseProjection.MaxIdentifierLength)
            .IsRequired();

        builder.Property(x => x.LandParcelId)
            .HasColumnName("land_parcel_id")
            .HasMaxLength(Component1GovernanceCaseProjection.MaxIdentifierLength)
            .IsRequired();

        builder.Property(x => x.DivisionCode)
            .HasColumnName("division_code")
            .HasMaxLength(Component1GovernanceCaseProjection.MaxCodeLength)
            .IsRequired();

        builder.Property(x => x.DistrictName)
            .HasColumnName("district_name")
            .HasMaxLength(Component1GovernanceCaseProjection.MaxNameLength)
            .IsRequired();

        builder.Property(x => x.ProvinceName)
            .HasColumnName("province_name")
            .HasMaxLength(Component1GovernanceCaseProjection.MaxNameLength)
            .IsRequired();

        builder.Property(x => x.GnDivisionCode)
            .HasColumnName("gn_division_code")
            .HasMaxLength(Component1GovernanceCaseProjection.MaxCodeLength)
            .IsRequired();

        builder.Property(x => x.RequestedLandUseCategory)
            .HasColumnName("requested_land_use_category")
            .HasMaxLength(Component1GovernanceCaseProjection.MaxCategoryLength)
            .IsRequired();

        builder.Property(x => x.ExtentHa)
            .HasColumnName("extent_ha")
            .HasPrecision(14, 4)
            .IsRequired();

        builder.Property(x => x.OwnershipEvidenceStatus)
            .HasColumnName("ownership_evidence_status")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.ProtectedZoneOverlapState)
            .HasColumnName("protected_zone_overlap_state")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.OverlappingSensitiveZoneCodesJson)
            .HasColumnName("overlapping_sensitive_zone_codes_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.BoundaryConfirmationStatus)
            .HasColumnName("boundary_confirmation_status")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.EncumbranceOrDisputeFlag)
            .HasColumnName("encumbrance_or_dispute_flag")
            .IsRequired();

        builder.Property(x => x.ParcelEvidenceRefsJson)
            .HasColumnName("parcel_evidence_refs_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.InputSha256)
            .HasColumnName("input_sha256")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.SourceSystem)
            .HasColumnName("source_system")
            .HasMaxLength(GovernanceInputProvenance.MaxIdentifierLength)
            .IsRequired();

        builder.Property(x => x.SourceRecordId)
            .HasColumnName("source_record_id")
            .HasMaxLength(GovernanceInputProvenance.MaxIdentifierLength)
            .IsRequired();

        builder.Property(x => x.SourceRecordVersion)
            .HasColumnName("source_record_version")
            .HasMaxLength(GovernanceInputProvenance.MaxVersionLength)
            .IsRequired();

        builder.Property(x => x.SourceObservedAtUtc)
            .HasColumnName("source_observed_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.IsSynthetic)
            .HasColumnName("is_synthetic")
            .IsRequired();

        builder.Property(x => x.ProvenanceKind)
            .HasColumnName("provenance_kind")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.SyntheticSeedId)
            .HasColumnName("synthetic_seed_id")
            .HasMaxLength(GovernanceInputProvenance.MaxIdentifierLength);

        builder.Property(x => x.SyntheticSeedVersion)
            .HasColumnName("synthetic_seed_version")
            .HasMaxLength(GovernanceInputProvenance.MaxVersionLength);

        builder.Property(x => x.GeneratedAtUtc)
            .HasColumnName("generated_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.Purpose)
            .HasColumnName("purpose")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.IngestedAtUtc)
            .HasColumnName("ingested_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(x => new { x.CaseId, x.SourceRecordVersion, x.InputSha256 })
            .IsUnique()
            .HasDatabaseName("ux_c1_case_projections_case_version_sha256");

        builder.HasIndex(x => x.CaseId)
            .HasDatabaseName("ix_c1_case_projections_case_id");

        builder.HasIndex(x => x.LandParcelId)
            .HasDatabaseName("ix_c1_case_projections_land_parcel_id");
    }
}
