using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

public class LandIntelligenceDbContext : DbContext
{
    public const string SchemaName = "land_intelligence";

    public LandIntelligenceDbContext(DbContextOptions<LandIntelligenceDbContext> options)
        : base(options)
    {
    }

    public DbSet<LandCategoryEntity> LandCategories => Set<LandCategoryEntity>();

    public DbSet<LandUseEntity> LandUses => Set<LandUseEntity>();

    public DbSet<LandParcelEntity> LandParcels => Set<LandParcelEntity>();

    public DbSet<SpatialConstraintEntity> SpatialConstraints => Set<SpatialConstraintEntity>();

    public DbSet<InfrastructureFeatureEntity> InfrastructureFeatures => Set<InfrastructureFeatureEntity>();

    public DbSet<EnvironmentalRestrictionEntity> EnvironmentalRestrictions => Set<EnvironmentalRestrictionEntity>();

    public DbSet<LandRecommendationEntity> LandRecommendations => Set<LandRecommendationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LandIntelligenceDbContext).Assembly);
    }
}
