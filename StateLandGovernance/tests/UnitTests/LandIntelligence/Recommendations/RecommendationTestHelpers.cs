using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
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

        return Task.FromResult<IReadOnlyList<LandParcel>>(query.ToList());
    }

    public Task AddAsync(LandParcel parcel, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpdateAsync(LandParcel parcel, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeSpatialAnalysisService : ISpatialAnalysisService
{
    public Task<PointInPolygonResultDto> IsPointInPolygonAsync(PointInPolygonRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<ParcelIntersectionResultDto>> FindParcelsIntersectingConstraintAsync(Guid spatialConstraintId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<GeometryIntersectionResultDto> CheckGeometriesIntersectAsync(GeometryIntersectionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<ProximityResultDto>> FindParcelsNearPointAsync(ProximitySearchRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<DistanceResultDto> CalculateDistanceBetweenParcelAndInfrastructureAsync(Guid landParcelId, Guid infrastructureFeatureId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new DistanceResultDto(
            landParcelId,
            infrastructureFeatureId,
            "[SYNTHETIC]",
            InfrastructureFeatureType.Road,
            250,
            true));

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
    public static RuleBasedLandRecommendationEngine Create(params LandParcel[] parcels)
    {
        IRecommendationCriterionEvaluator[] evaluators =
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

        return new RuleBasedLandRecommendationEngine(
            new FakeLandParcelRepository(parcels),
            new FakeSpatialAnalysisService(),
            evaluators);
    }
}
