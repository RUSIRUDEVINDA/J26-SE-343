using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class LeaseInterestValuationEntityConfiguration : IEntityTypeConfiguration<LeaseInterestValuationEntity>
{
    public void Configure(EntityTypeBuilder<LeaseInterestValuationEntity> builder)
    {
        builder.ToTable("lease_interest_valuations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FormType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.AssessedLandValueLkr).HasPrecision(18, 2);
        builder.Property(x => x.AssessedAreaHectares).HasPrecision(18, 6);
        builder.Property(x => x.ValuationReference).HasMaxLength(128);
        builder.Property(x => x.IssuingOffice).HasMaxLength(200);
        builder.Property(x => x.SiteCondition).HasMaxLength(2000);
        builder.Property(x => x.CultivationDetails).HasMaxLength(2000);
        builder.Property(x => x.ResidentialImprovements).HasMaxLength(2000);
        builder.Property(x => x.ProjectUseDetails).HasMaxLength(2000);
        builder.Property(x => x.OtherTypeDetails).HasMaxLength(2000);
        builder.Property(x => x.OfficerNotes).HasMaxLength(4000);
        builder.Property(x => x.WorksheetJson).HasColumnType("jsonb");
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.LeaseInterestRequestId, x.Revision }).IsUnique();
        builder.HasIndex(x => new { x.LeaseInterestRequestId, x.IdempotencyKey }).IsUnique();
        builder.HasOne<LeaseInterestRequestEntity>()
            .WithMany()
            .HasForeignKey(x => x.LeaseInterestRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
