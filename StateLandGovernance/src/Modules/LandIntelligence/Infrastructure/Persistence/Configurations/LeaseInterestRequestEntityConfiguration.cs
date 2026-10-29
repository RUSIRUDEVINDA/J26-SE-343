using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class LeaseInterestRequestEntityConfiguration
    : IEntityTypeConfiguration<LeaseInterestRequestEntity>
{
    public void Configure(EntityTypeBuilder<LeaseInterestRequestEntity> builder)
    {
        builder.ToTable("lease_interest_requests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OwnerId).IsRequired();
        builder.Property(x => x.LandParcelId).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.AcknowledgementReference).HasMaxLength(64);
        builder.Property(x => x.SubmitIdempotencyKey).HasMaxLength(128);
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.RequestedPurpose).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired().IsConcurrencyToken();
        builder.Property(x => x.ApplicantNicCiphertext).HasColumnType("text");
        builder.Property(x => x.ApplicantNicLookupDigest).HasMaxLength(64);
        builder.Property(x => x.ApplicantBrnLookupDigest).HasMaxLength(64);
        builder.Property(x => x.VerificationCompletedByOfficerId);

        builder.HasIndex(x => x.OwnerId);
        builder.HasIndex(x => x.ApplicantNicLookupDigest);
        builder.HasIndex(x => x.ApplicantBrnLookupDigest);
        builder.HasIndex(x => x.LandParcelId);
        builder.HasIndex(x => x.LandParcelId)
            .IsUnique()
            .HasDatabaseName("IX_lease_interest_requests_one_approved_in_principle_per_parcel")
            .HasFilter("\"Status\" = 7");
        builder.HasIndex(x => x.AcknowledgementReference).IsUnique()
            .HasFilter("\"AcknowledgementReference\" IS NOT NULL");
        builder.HasIndex(x => new { x.OwnerId, x.SubmitIdempotencyKey }).IsUnique()
            .HasFilter("\"SubmitIdempotencyKey\" IS NOT NULL");

        builder.HasMany(x => x.StatusHistory)
            .WithOne(x => x.Request!)
            .HasForeignKey(x => x.LeaseInterestRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
