using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Configurations;

internal static class GisReferenceEntityConfigurationExtensions
{
    private const string SourceIdentityCheckConstraintSql =
        "\"SourceFeatureId\" IS NOT NULL OR \"SourceFingerprint\" IS NOT NULL";

    private const string SourceFeatureIdUniqueIndexFilter = "\"SourceFeatureId\" IS NOT NULL";

    private const string SourceFingerprintUniqueIndexFilter =
        "\"SourceFeatureId\" IS NULL AND \"SourceFingerprint\" IS NOT NULL";

    public static EntityTypeBuilder<TEntity> ConfigureGisReferenceTable<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        string tableName)
        where TEntity : GisReferenceEntityBase =>
        builder.ToTable(tableName, table =>
        {
            table.HasCheckConstraint(
                $"CK_{tableName}_source_identity",
                SourceIdentityCheckConstraintSql);
        });

    public static void ConfigureGisReferenceProvenance<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : GisReferenceEntityBase
    {
        builder.Property(entity => entity.SourceName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entity => entity.SourceLayer)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(entity => entity.SourceFeatureId)
            .HasMaxLength(GisReferenceEntityBase.SourceFeatureIdMaxLength);

        builder.Property(entity => entity.SourceFingerprint)
            .HasMaxLength(GisReferenceEntityBase.SourceFingerprintMaxLength);

        builder.Property(entity => entity.ImportedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(entity => new { entity.SourceName, entity.SourceLayer, entity.SourceFeatureId })
            .IsUnique()
            .HasFilter(SourceFeatureIdUniqueIndexFilter);

        builder.HasIndex(entity => new { entity.SourceName, entity.SourceLayer, entity.SourceFingerprint })
            .IsUnique()
            .HasFilter(SourceFingerprintUniqueIndexFilter);
    }

    public static IndexBuilder ConfigureGistGeometryIndex<TEntity>(
        this IndexBuilder indexBuilder) =>
        indexBuilder.HasMethod("GIST");
}
