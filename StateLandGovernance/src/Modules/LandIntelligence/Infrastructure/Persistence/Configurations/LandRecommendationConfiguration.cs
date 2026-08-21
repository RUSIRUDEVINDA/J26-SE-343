using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class LandRecommendationConfiguration : IEntityTypeConfiguration<LandRecommendationEntity>
{
    public void Configure(EntityTypeBuilder<LandRecommendationEntity> builder)
    {
        builder.ToTable("land_recommendations");

        builder.HasKey(recommendation => recommendation.Id);

        builder.Property(recommendation => recommendation.SuitabilityScore)
            .HasPrecision(5, 2);

        builder.Property(recommendation => recommendation.RecommendedUseDescription)
            .HasMaxLength(500);

        builder.Property(recommendation => recommendation.CriteriaJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(recommendation => recommendation.EvidenceJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasIndex(recommendation => recommendation.LandParcelId);
        builder.HasIndex(recommendation => recommendation.GeneratedAt);
    }
}
