using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

namespace StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

internal static class SyntheticRecommendationParcelFactory
{
    public static LandParcel CreateSuitableParcel(string cadastralNumber = "SYNTH-SUITABLE-001") =>
        CreateBaseParcel(
            cadastralNumber,
            LandCategoryType.StateLand,
            LandUseType.Agricultural,
            areaHectares: 5m,
            province: "Western",
            district: "Colombo");

    public static LandParcel CreateRestrictedParcel(string cadastralNumber = "SYNTH-RESTRICTED-001")
    {
        var parcel = CreateBaseParcel(
            cadastralNumber,
            LandCategoryType.ReservedLand,
            LandUseType.Conservation,
            areaHectares: 2m,
            province: "Central",
            district: "Kandy");

        parcel.AddSpatialConstraint(new SpatialConstraint(
            SpatialConstraintType.BufferZone,
            "[SYNTHETIC] Adjacent to restricted zone",
            RestrictionSeverity.High));

        parcel.AddEnvironmentalRestriction(new EnvironmentalRestriction(
            EnvironmentalRestrictionType.Wetland,
            "[SYNTHETIC] Wetland buffer applies",
            RestrictionSeverity.Prohibitive));

        parcel.AddRegulatoryReference(new RegulatoryReference(
            "SYNTH-GZ-001",
            "[SYNTHETIC] Reference A",
            new DateOnly(2026, 1, 1)));

        parcel.AddRegulatoryReference(new RegulatoryReference(
            "SYNTH-GZ-002",
            "[SYNTHETIC] Reference B",
            new DateOnly(2026, 1, 2)));

        return parcel;
    }

    public static LandParcel CreateModerateParcel(string cadastralNumber = "SYNTH-MODERATE-001")
    {
        var parcel = CreateBaseParcel(
            cadastralNumber,
            LandCategoryType.StateLand,
            LandUseType.Agricultural,
            areaHectares: 4.5m,
            province: "Western",
            district: "Gampaha");

        parcel.AddInfrastructureFeature(new InfrastructureFeature(
            InfrastructureFeatureType.Road,
            "[SYNTHETIC] B Road",
            1200m,
            "[SYNTHETIC] Moderate distance from road"));

        parcel.AddSpatialConstraint(new SpatialConstraint(
            SpatialConstraintType.Setback,
            "[SYNTHETIC] Setback requirement",
            RestrictionSeverity.Medium));

        return parcel;
    }

    public static LandParcel CreateParcelWithArea(
        decimal areaHectares,
        string cadastralNumber = "SYNTH-AREA-001") =>
        CreateBaseParcel(
            cadastralNumber,
            LandCategoryType.StateLand,
            LandUseType.Agricultural,
            areaHectares,
            province: "Western",
            district: "Colombo");

    public static LandParcel CreateParcelWithRoadDistance(
        decimal? roadDistanceMeters,
        string cadastralNumber = "SYNTH-ROAD-001")
    {
        var parcel = new LandParcel(
            new ParcelIdentifier(cadastralNumber, "SYNTHETIC-PLAN"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC]"),
            new LandArea(5m, AreaUnit.Hectares),
            new AdministrativeLocation("Western", "Colombo", "Colombo DS"),
            new SpatialReference(6.9271, 79.8612, "EPSG:4326"),
            new LandUse(LandUseType.Agricultural, "[SYNTHETIC]"),
            new LandCharacteristics("Loam", "Gently sloping", 25m));

        parcel.AddInfrastructureFeature(new InfrastructureFeature(
            InfrastructureFeatureType.Road,
            "[SYNTHETIC] Access Road",
            roadDistanceMeters));

        return parcel;
    }

    public static LandParcel CreateParcelWithInfrastructureFeatures(
        string cadastralNumber,
        params (InfrastructureFeatureType Type, string Name, decimal? DistanceMeters)[] features)
    {
        var parcel = new LandParcel(
            new ParcelIdentifier(cadastralNumber, "SYNTHETIC-PLAN"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC]"),
            new LandArea(5m, AreaUnit.Hectares),
            new AdministrativeLocation("Western", "Colombo", "Colombo DS"),
            new SpatialReference(6.9271, 79.8612, "EPSG:4326"),
            new LandUse(LandUseType.Agricultural, "[SYNTHETIC]"),
            new LandCharacteristics("Loam", "Gently sloping", 25m));

        foreach (var (type, name, distanceMeters) in features)
        {
            parcel.AddInfrastructureFeature(new InfrastructureFeature(type, name, distanceMeters));
        }

        return parcel;
    }

    public static LandParcel CreateParcelWithEnvironmentalRestriction(
        EnvironmentalRestrictionType type,
        RestrictionSeverity severity,
        string cadastralNumber = "SYNTH-ENV-001")
    {
        var parcel = CreateSuitableParcel(cadastralNumber);
        parcel.AddEnvironmentalRestriction(new EnvironmentalRestriction(
            type,
            "[SYNTHETIC] Environmental restriction for recommendation test",
            severity));
        return parcel;
    }

    public static LandParcel CreateParcelWithRegulatoryReferenceCount(
        int referenceCount,
        string cadastralNumber = "SYNTH-REG-COUNT-001")
    {
        var parcel = CreateSuitableParcel(cadastralNumber);

        for (var index = 1; index <= referenceCount; index++)
        {
            parcel.AddRegulatoryReference(new RegulatoryReference(
                $"SYNTH-GZ-{index:000}",
                $"[SYNTHETIC] Regulatory reference {index}",
                new DateOnly(2026, 1, index)));
        }

        return parcel;
    }

    public static LandParcel CreateParcelWithGisDerivedRoad(
        decimal roadDistanceMeters,
        string cadastralNumber = "SYNTH-GIS-ROAD")
    {
        var parcel = CreateParcelWithRoadDistance(roadDistanceMeters, cadastralNumber);
        parcel.ReplaceInfrastructureFeatures([
            new InfrastructureFeature(
                InfrastructureFeatureType.Road,
                "[GIS-DERIVED] Mapped Access Road",
                roadDistanceMeters,
                "[GIS-DERIVED] Nearest mapped road from pilot GIS layer.",
                AttributeProvenance.Derived(GisDerivedIntelligenceOwnership.SourceName))
        ]);
        parcel.AttachGisDerivedIntelligence(new ParcelGisDerivedIntelligence(
            GisEnrichmentOverallStatus.Partial,
            null));
        return parcel;
    }

    public static LandParcel CreateParcelWithGisDerivedNaturalWaterOnly(
        decimal waterDistanceMeters,
        string cadastralNumber = "SYNTH-GIS-WATER")
    {
        var parcel = new LandParcel(
            new ParcelIdentifier(cadastralNumber, "SYNTHETIC-PLAN"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC]"),
            new LandArea(5m, AreaUnit.Hectares),
            new AdministrativeLocation("Western", "Colombo", "Colombo DS"),
            new SpatialReference(6.9271, 79.8612, "EPSG:4326"),
            new LandUse(LandUseType.Agricultural, "[SYNTHETIC]"),
            new LandCharacteristics("Loam", "Gently sloping", 25m));

        parcel.AddInfrastructureFeature(new InfrastructureFeature(
            InfrastructureFeatureType.Other,
            "[GIS-DERIVED] Canal",
            waterDistanceMeters,
            "[GIS-DERIVED] Natural water proximity. This is not utility water supply.",
            AttributeProvenance.Derived(GisDerivedIntelligenceOwnership.SourceName)));

        parcel.AttachGisDerivedIntelligence(new ParcelGisDerivedIntelligence(
            GisEnrichmentOverallStatus.Partial,
            null));

        return parcel;
    }

    public static LandParcel CreateParcelWithGisDerivedSoil(
        string officialSoilType,
        string gisSoilGroupName,
        string cadastralNumber = "SYNTH-GIS-SOIL")
    {
        var parcel = CreateSuitableParcel(cadastralNumber);
        parcel.UpdateCharacteristics(new LandCharacteristics(
            officialSoilType,
            "Gently sloping",
            25m,
            AttributeProvenance.Official("Land Commissioner"),
            null,
            null));

        parcel.AttachGisDerivedIntelligence(new ParcelGisDerivedIntelligence(
            GisEnrichmentOverallStatus.Partial,
            new ParcelDerivedSoilGroupEvidence(
                gisSoilGroupName,
                92.5m,
                AttributeProvenance.Derived(GisDerivedIntelligenceOwnership.SourceName))));

        return parcel;
    }

    public static LandParcel CreateParcelWithGisConservationRestriction(
        string cadastralNumber = "SYNTH-GIS-CONSERVATION")
    {
        var parcel = CreateSuitableParcel(cadastralNumber);
        parcel.AddEnvironmentalRestriction(new EnvironmentalRestriction(
            EnvironmentalRestrictionType.ProtectedArea,
            "[GIS-DERIVED] Intersects soil conservation area 'Pilot Reserve'.",
            RestrictionSeverity.Low,
            AttributeProvenance.Derived(GisDerivedIntelligenceOwnership.SourceName)));

        parcel.AttachGisDerivedIntelligence(new ParcelGisDerivedIntelligence(
            GisEnrichmentOverallStatus.Partial,
            null));

        return parcel;
    }

    public static LandParcel CreateParcelWithUnavailableGisEnrichment(string cadastralNumber = "SYNTH-GIS-UNAVAIL")
    {
        var parcel = CreateSuitableParcel(cadastralNumber);
        parcel.AttachGisDerivedIntelligence(new ParcelGisDerivedIntelligence(
            GisEnrichmentOverallStatus.Unavailable,
            null));
        return parcel;
    }

    private static LandParcel CreateBaseParcel(
        string cadastralNumber,
        LandCategoryType category,
        LandUseType use,
        decimal areaHectares,
        string province,
        string district)
    {
        var parcel = new LandParcel(
            new ParcelIdentifier(cadastralNumber, "SYNTHETIC-PLAN"),
            new LandCategory(category, "[SYNTHETIC]"),
            new LandArea(areaHectares, AreaUnit.Hectares),
            new AdministrativeLocation(province, district, $"{district} DS"),
            new SpatialReference(6.9271, 79.8612, "EPSG:4326"),
            new LandUse(use, "[SYNTHETIC]"),
            new LandCharacteristics("Loam", "Gently sloping", 25m));

        parcel.AddInfrastructureFeature(new InfrastructureFeature(
            InfrastructureFeatureType.Road,
            "[SYNTHETIC] Access Road",
            250m));

        return parcel;
    }
}

internal sealed class FakeLandParcelRepository : ILandParcelRepository
{
    private readonly IReadOnlyList<LandParcel> _parcels;

    public FakeLandParcelRepository(params LandParcel[] parcels) => _parcels = parcels;

    public Task<LandParcel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_parcels.FirstOrDefault(p => p.Id == id));

    public Task<LandParcel?> GetByCadastralNumberAsync(string cadastralNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(_parcels.FirstOrDefault(p =>
            p.Identifier.CadastralNumber.Equals(cadastralNumber, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<LandParcel>> SearchAsync(LandSearchRequest request, CancellationToken cancellationToken = default)
    {
        IEnumerable<LandParcel> query = _parcels;

        if (!string.IsNullOrWhiteSpace(request.Province))
        {
            query = query.Where(p => p.Location.Province.Equals(request.Province, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.District))
        {
            query = query.Where(p => p.Location.District.Equals(request.District, StringComparison.OrdinalIgnoreCase));
        }

        if (request.CategoryType is not null)
        {
            query = query.Where(p => p.Category.Type == request.CategoryType);
        }

        if (request.CurrentUseType is not null)
        {
            query = query.Where(p => p.CurrentUse?.Type == request.CurrentUseType);
        }

        if (request.MinArea.HasValue)
        {
            query = query.Where(p => p.Area.Value >= request.MinArea.Value);
        }

        if (request.MaxArea.HasValue)
        {
            query = query.Where(p => p.Area.Value <= request.MaxArea.Value);
        }

        return Task.FromResult<IReadOnlyList<LandParcel>>(query.ToList());
    }

    public Task<int> CountSearchAsync(LandSearchRequest request, CancellationToken cancellationToken = default)
    {
        IEnumerable<LandParcel> query = _parcels;

        if (!string.IsNullOrWhiteSpace(request.Province))
        {
            query = query.Where(p => p.Location.Province.Equals(request.Province, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.District))
        {
            query = query.Where(p => p.Location.District.Equals(request.District, StringComparison.OrdinalIgnoreCase));
        }

        if (request.CategoryType is not null)
        {
            query = query.Where(p => p.Category.Type == request.CategoryType);
        }

        if (request.CurrentUseType is not null)
        {
            query = query.Where(p => p.CurrentUse?.Type == request.CurrentUseType);
        }

        if (request.MinArea.HasValue)
        {
            query = query.Where(p => p.Area.Value >= request.MinArea.Value);
        }

        if (request.MaxArea.HasValue)
        {
            query = query.Where(p => p.Area.Value <= request.MaxArea.Value);
        }

        return Task.FromResult(query.Count());
    }

    public Task AddAsync(LandParcel parcel, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpdateAsync(LandParcel parcel, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeSpatialAnalysisService : ISpatialAnalysisService
{
    private readonly IReadOnlyList<LandParcel> _parcels;

    public FakeSpatialAnalysisService(IReadOnlyList<LandParcel>? parcels = null) =>
        _parcels = parcels ?? Array.Empty<LandParcel>();

    public Task<PointInPolygonResultDto> IsPointInPolygonAsync(PointInPolygonRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<ParcelIntersectionResultDto>> FindParcelsIntersectingConstraintAsync(Guid spatialConstraintId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<GeometryIntersectionResultDto> CheckGeometriesIntersectAsync(GeometryIntersectionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<ProximityResultDto>> FindParcelsNearPointAsync(ProximitySearchRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<DistanceResultDto> CalculateDistanceBetweenParcelAndInfrastructureAsync(
        Guid landParcelId,
        Guid infrastructureFeatureId,
        CancellationToken cancellationToken = default)
    {
        var parcel = _parcels.FirstOrDefault(p => p.Id == landParcelId);
        var feature = parcel?.InfrastructureFeatures.FirstOrDefault(f => f.Id == infrastructureFeatureId);

        if (feature?.DistanceMeters is not { } storedDistance)
        {
            throw new ValidationException([
                "Parcel and infrastructure feature with a known location are required for distance analysis."
            ]);
        }

        return Task.FromResult(new DistanceResultDto(
            landParcelId,
            infrastructureFeatureId,
            feature.Name,
            feature.Type,
            (double)storedDistance,
            true));
    }

    public Task<IReadOnlyList<SpatialFilterResultDto>> FilterParcelsAsync(SpatialFilterRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<AreaCalculationResultDto> CalculateParcelAreaAsync(Guid landParcelId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<SpatialConstraintDetectionDto>> DetectSpatialConstraintsForParcelAsync(Guid landParcelId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<SpatialConstraintDto>> AnalyzeConstraintsAsync(Guid parcelId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}

internal static class RecommendationEngineTestFactory
{
    public static IReadOnlyList<IRecommendationCriterionEvaluator> CreateStandardEvaluators() =>
    [
        new PurposeAlignmentCriterionEvaluator(),
        new RequiredAreaCriterionEvaluator(),
        new LandCategoryCriterionEvaluator(),
        new LandUseCriterionEvaluator(),
        new LocationPreferenceCriterionEvaluator(),
        new AccessibilityCriterionEvaluator(),
        new EnvironmentalCriterionEvaluator(),
        new RegulatoryCriterionEvaluator(),
        new SpatialConstraintCriterionEvaluator(),
        new CustomCriteriaEvaluator()
    ];

    public static RuleBasedLandRecommendationEngine Create(params LandParcel[] parcels)
    {
        return CreateWithEvaluators(CreateStandardEvaluators(), parcels);
    }

    public static RuleBasedLandRecommendationEngine CreateWithEvaluators(
        IReadOnlyList<IRecommendationCriterionEvaluator> evaluators,
        params LandParcel[] parcels)
    {
        return new RuleBasedLandRecommendationEngine(
            new FakeLandParcelRepository(parcels),
            new FakeSpatialAnalysisService(parcels),
            evaluators);
    }
}
