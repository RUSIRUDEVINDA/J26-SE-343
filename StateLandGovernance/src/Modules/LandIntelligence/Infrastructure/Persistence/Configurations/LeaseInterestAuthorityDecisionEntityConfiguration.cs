using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class LeaseInterestAuthorityDecisionEntityConfiguration
    : IEntityTypeConfiguration<LeaseInterestAuthorityDecisionEntity>
{
    public void Configure(EntityTypeBuilder<LeaseInterestAuthorityDecisionEntity> builder)
    {
        builder.ToTable("lease_interest_authority_decisions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.LeaseInterestRequestId).IsRequired();
        builder.Property(x => x.Decision).IsRequired();
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.ReviewedValuationId);
        builder.Property(x => x.ReviewedValuationRevision);
        builder.Property(x => x.ResultingStatus).IsRequired();
        builder.Property(x => x.RecordedByAuthorityId).IsRequired();
        builder.Property(x => x.RecordedAtUtc).IsRequired();

        builder.HasIndex(x => x.LeaseInterestRequestId);
        builder.HasIndex(x => new { x.LeaseInterestRequestId, x.RecordedAtUtc });
        builder.HasIndex(x => x.ReviewedValuationId);

        builder.HasOne(x => x.Request)
            .WithMany()
            .HasForeignKey(x => x.LeaseInterestRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ReviewedValuation)
            .WithMany()
            .HasForeignKey(x => x.ReviewedValuationId)
            .OnDelete(DeleteBehavior.Restrict);

    }
}
