using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

public class LandParcelEntity
{
    public Guid Id { get; set; }

    public string CadastralNumber { get; set; } = null!;

    public string? SurveyPlanReference { get; set; }

    public Guid LandCategoryId { get; set; }

    public LandCategoryEntity LandCategory { get; set; } = null!;

    public Guid? CurrentLandUseId { get; set; }

    public LandUseEntity? CurrentLandUse { get; set; }

    public decimal AreaValue { get; set; }

    public AreaUnit AreaUnit { get; set; }

    public string Province { get; set; } = null!;

    public string District { get; set; } = null!;

    public string DivisionalSecretariat { get; set; } = null!;

    public string? GramaNiladhariDivision { get; set; }

    /// <summary>Parcel centroid (WGS 84).</summary>
    public Point Centroid { get; set; } = null!;

    /// <summary>Parcel boundary geometry when available.</summary>
    public MultiPolygon? Boundary { get; set; }

    public int SpatialReferenceSystemId { get; set; } = 4326;

    public string? SoilType { get; set; }

    public string? TerrainDescription { get; set; }

    public decimal? ElevationMeters { get; set; }

    public string? CharacteristicsProvenanceJson { get; set; }

    public ICollection<SpatialConstraintEntity> SpatialConstraints { get; set; } = [];

    public ICollection<InfrastructureFeatureEntity> InfrastructureFeatures { get; set; } = [];

    public ICollection<EnvironmentalRestrictionEntity> EnvironmentalRestrictions { get; set; } = [];

    public ICollection<RegulatoryReferenceEntity> RegulatoryReferences { get; set; } = [];

    public ParcelDerivedSoilGroupEntity? DerivedSoilGroup { get; set; }

    public LandParcelGisEnrichmentSnapshotEntity? GisEnrichmentSnapshot { get; set; }
}
