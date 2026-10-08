using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;

public class RuleParameterEntityConfiguration : IEntityTypeConfiguration<RuleParameterEntity>
{
    public void Configure(EntityTypeBuilder<RuleParameterEntity> builder)
    {
        builder.ToTable("rule_parameters", "governance_intelligence");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.ParameterName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(p => p.ParameterValue)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.Unit)
            .HasMaxLength(50);

        builder.Property(p => p.EffectiveFrom)
            .HasColumnType("timestamp with time zone");

        builder.Property(p => p.EffectiveTo)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(p => p.RuleId)
            .HasDatabaseName("idx_rule_param_rule_id");
    }
}
