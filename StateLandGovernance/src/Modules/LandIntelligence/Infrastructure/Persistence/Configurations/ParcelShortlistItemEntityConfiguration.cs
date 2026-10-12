using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class ParcelShortlistItemEntityConfiguration
    : IEntityTypeConfiguration<ParcelShortlistItemEntity>
{
    public void Configure(EntityTypeBuilder<ParcelShortlistItemEntity> builder)
    {
        builder.ToTable("parcel_shortlist_items");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OwnerId).IsRequired();
        builder.Property(x => x.LandParcelId).IsRequired();
        builder.Property(x => x.SavedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.OwnerId, x.LandParcelId }).IsUnique();
        builder.HasIndex(x => x.OwnerId);
    }
}
