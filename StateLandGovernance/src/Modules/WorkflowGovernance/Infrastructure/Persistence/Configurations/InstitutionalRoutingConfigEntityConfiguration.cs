namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

public sealed class InstitutionalRoutingConfigEntityConfiguration : IEntityTypeConfiguration<InstitutionalRoutingConfigEntity>
{
    public void Configure(EntityTypeBuilder<InstitutionalRoutingConfigEntity> builder)
    {
        builder.ToTable("institutional_routing_configs", WorkflowGovernanceDbContext.SchemaName);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.PolicyJson).HasColumnName("policy_json").HasColumnType("jsonb");

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.RuleSetName)
            .HasColumnName("rule_set_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.RuleSetVersion)
            .HasColumnName("rule_set_version")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.StandardStageDurationDays)
            .HasColumnName("standard_stage_duration_days")
            .IsRequired();

        builder.Property(e => e.DivisionalSecretariatDurationDays)
            .HasColumnName("divisional_secretariat_duration_days")
            .IsRequired();

        builder.Property(e => e.CommissionerDurationDays)
            .HasColumnName("commissioner_duration_days")
            .IsRequired();

        builder.Property(e => e.StandardCapability)
            .HasColumnName("standard_capability")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.DivisionalSecretaryCapability)
            .HasColumnName("divisional_secretary_capability")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CommissionerCapability)
            .HasColumnName("commissioner_capability")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(e => new { e.RuleSetName, e.RuleSetVersion })
            .IsUnique()
            .HasDatabaseName("ix_institutional_routing_configs_rule");
    }
}
