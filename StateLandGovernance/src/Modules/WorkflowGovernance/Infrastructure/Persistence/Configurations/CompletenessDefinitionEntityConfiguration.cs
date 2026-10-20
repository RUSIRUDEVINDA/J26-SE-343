namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

public sealed class CompletenessDefinitionEntityConfiguration : IEntityTypeConfiguration<CompletenessDefinitionEntity>
{
    public void Configure(EntityTypeBuilder<CompletenessDefinitionEntity> builder)
    {
        builder.ToTable("completeness_definitions");
        builder.HasKey(x => new { x.Kind, x.Identifier, x.Version });
        builder.Property(x => x.Kind).HasMaxLength(20);
        builder.Property(x => x.Identifier).HasMaxLength(150);
        builder.Property(x => x.Version).HasMaxLength(50);
        builder.Property(x => x.SourceReference).IsRequired();
        builder.Property(x => x.DefinitionJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
