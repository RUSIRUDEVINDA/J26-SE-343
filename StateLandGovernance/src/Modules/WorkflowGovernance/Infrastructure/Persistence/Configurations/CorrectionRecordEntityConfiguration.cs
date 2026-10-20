namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;

public sealed class CorrectionRecordEntityConfiguration : IEntityTypeConfiguration<CorrectionRecordEntity>
{
    public void Configure(EntityTypeBuilder<CorrectionRecordEntity> builder)
    {
        builder.ToTable("correction_records");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Revision).IsConcurrencyToken();
        builder.Property(x => x.RecordJson).HasColumnType("jsonb").IsRequired();
        builder.HasOne<LeaseCaseEntity>().WithMany().HasForeignKey(x => x.LeaseCaseId).OnDelete(DeleteBehavior.Restrict);
    }
}
