namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Entities;
public sealed class CaseAssignmentConfiguration : IEntityTypeConfiguration<CaseAssignmentEntity>
{
    public void Configure(EntityTypeBuilder<CaseAssignmentEntity> b)
    {
        b.ToTable("case_assignments"); b.HasKey(x => x.CaseId);
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.StateJson).HasColumnType("jsonb").IsRequired();
        b.HasOne<LeaseCaseEntity>().WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class IntakeJurisdictionPolicyConfiguration : IEntityTypeConfiguration<IntakeJurisdictionPolicyEntity>
{
    public void Configure(EntityTypeBuilder<IntakeJurisdictionPolicyEntity> b)
    {
        b.ToTable("intake_jurisdiction_policies"); b.HasKey(x => x.Id);
        b.Property(x => x.Identifier).HasMaxLength(150).IsRequired();
        b.Property(x => x.Version).HasMaxLength(80).IsRequired();
        b.HasIndex(x => new { x.Identifier, x.Version }).IsUnique();
        b.Property(x => x.PolicyJson).HasColumnType("jsonb").IsRequired();
    }
}
