using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class LeaseInterestStatusHistoryEntityConfiguration
    : IEntityTypeConfiguration<LeaseInterestStatusHistoryEntity>
{
    public void Configure(EntityTypeBuilder<LeaseInterestStatusHistoryEntity> builder)
    {
        builder.ToTable("lease_interest_status_history");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.LeaseInterestRequestId).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.ChangedAtUtc).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.ActorKind).HasDefaultValue(LeaseInterestStatusActorKind.System);
        builder.Property(x => x.VisibleToApplicant).HasDefaultValue(true);

        builder.HasIndex(x => x.LeaseInterestRequestId);
    }
}
