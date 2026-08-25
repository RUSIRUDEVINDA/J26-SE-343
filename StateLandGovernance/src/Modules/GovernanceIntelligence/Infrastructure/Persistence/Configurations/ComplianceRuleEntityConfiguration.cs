using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;

public class ComplianceRuleEntityConfiguration : IEntityTypeConfiguration<ComplianceRuleEntity>
{
    public void Configure(EntityTypeBuilder<ComplianceRuleEntity> builder)
    {
        builder.ToTable("compliance_rules", "governance_intelligence");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        builder.Property(r => r.RuleCode)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.RuleVersion)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.SourceSection)
            .HasMaxLength(200);

        builder.Property(r => r.Category)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.RuleType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.CalculationKey)
            .HasMaxLength(100);

        builder.Property(r => r.Severity)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.IsBlocking)
            .IsRequired();

        builder.Property(r => r.Enabled)
            .IsRequired();

        builder.Property(r => r.EffectiveFrom)
            .HasColumnType("timestamp with time zone");

        builder.Property(r => r.EffectiveTo)
            .HasColumnType("timestamp with time zone");

        // Composite Unique Index on RuleCode + RuleVersion
        builder.HasIndex(r => new { r.RuleCode, r.RuleVersion })
            .IsUnique()
            .HasDatabaseName("idx_uniq_rule_code_version");

        builder.HasOne(r => r.Source)
            .WithMany()
            .HasForeignKey(r => r.SourceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(r => r.Parameters)
            .WithOne(p => p.Rule)
            .HasForeignKey(p => p.RuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
