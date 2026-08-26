using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities;

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

    public DbSet<RegulatoryReferenceEntity> RegulatoryReferences => Set<RegulatoryReferenceEntity>();

    public DbSet<LandRecommendationEntity> LandRecommendations => Set<LandRecommendationEntity>();

    public DbSet<GisAdministrativeBoundaryEntity> GisAdministrativeBoundaries =>
        Set<GisAdministrativeBoundaryEntity>();

    public DbSet<GisRoadEntity> GisRoads => Set<GisRoadEntity>();

    public DbSet<GisWaterFeatureEntity> GisWaterFeatures => Set<GisWaterFeatureEntity>();

    public DbSet<GisSoilGroupEntity> GisSoilGroups => Set<GisSoilGroupEntity>();

    public DbSet<GisSoilConservationAreaEntity> GisSoilConservationAreas =>
        Set<GisSoilConservationAreaEntity>();

    public DbSet<GisSoilErosionObservationEntity> GisSoilErosionObservations =>
        Set<GisSoilErosionObservationEntity>();

    public DbSet<LandParcelGisEnrichmentSnapshotEntity> LandParcelGisEnrichmentSnapshots =>
        Set<LandParcelGisEnrichmentSnapshotEntity>();

    public DbSet<ParcelDerivedSoilGroupEntity> ParcelDerivedSoilGroups =>
        Set<ParcelDerivedSoilGroupEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LandIntelligenceDbContext).Assembly);
    }
}
