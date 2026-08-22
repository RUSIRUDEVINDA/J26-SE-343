using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Mappings;
using StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Repositories;

public sealed class LandParcelRepository : ILandParcelRepository
{
    private static readonly GeometryFactory GeometryFactory = NtsGeometryServices.Instance
        .CreateGeometryFactory(PostGisConfiguration.DefaultSpatialReferenceSystemId);

    private readonly LandIntelligenceDbContext _dbContext;

    public LandParcelRepository(LandIntelligenceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LandParcel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await BaseQuery()
            .FirstOrDefaultAsync(parcel => parcel.Id == id, cancellationToken);

        return entity is null ? null : LandParcelPersistenceMapper.ToDomain(entity);
    }

    public async Task<LandParcel?> GetByCadastralNumberAsync(
        string cadastralNumber,
        CancellationToken cancellationToken = default)
    {
        var entity = await BaseQuery()
            .FirstOrDefaultAsync(
                parcel => parcel.CadastralNumber == cadastralNumber,
                cancellationToken);

        return entity is null ? null : LandParcelPersistenceMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<LandParcel>> SearchAsync(
        LandSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var entities = await BuildSearchQuery(request)
            .OrderBy(parcel => parcel.CadastralNumber)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return entities.Select(LandParcelPersistenceMapper.ToDomain).ToList();
    }

    public Task<int> CountSearchAsync(
        LandSearchRequest request,
        CancellationToken cancellationToken = default) =>
        BuildSearchQuery(request).CountAsync(cancellationToken);

    public async Task AddAsync(LandParcel parcel, CancellationToken cancellationToken = default)
    {
        var entity = LandParcelPersistenceMapper.ToPersistence(parcel);
        await _dbContext.LandParcels.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(LandParcel parcel, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.LandParcels
            .FirstOrDefaultAsync(p => p.Id == parcel.Id, cancellationToken);

        if (entity is null)
        {
            return;
        }

        LandParcelPersistenceMapper.ApplyUpdates(entity, parcel);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<LandParcelEntity> BaseQuery() =>
        _dbContext.LandParcels
            .AsNoTracking()
            .Include(parcel => parcel.LandCategory)
            .Include(parcel => parcel.CurrentLandUse)
            .Include(parcel => parcel.SpatialConstraints)
            .Include(parcel => parcel.InfrastructureFeatures)
            .Include(parcel => parcel.EnvironmentalRestrictions)
            .Include(parcel => parcel.RegulatoryReferences);

    private IQueryable<LandParcelEntity> BuildSearchQuery(LandSearchRequest request)
    {
        var query = BaseQuery();

        if (!string.IsNullOrWhiteSpace(request.Province))
        {
            query = query.Where(parcel => parcel.Province == request.Province);
        }

        if (!string.IsNullOrWhiteSpace(request.District))
        {
            query = query.Where(parcel => parcel.District == request.District);
        }

        if (!string.IsNullOrWhiteSpace(request.DivisionalSecretariat))
        {
            query = query.Where(parcel => parcel.DivisionalSecretariat == request.DivisionalSecretariat);
        }

        if (request.CategoryType.HasValue)
        {
            query = query.Where(parcel => parcel.LandCategory.Type == request.CategoryType.Value);
        }

        if (request.CurrentUseType.HasValue)
        {
            query = query.Where(parcel => parcel.CurrentLandUse!.Type == request.CurrentUseType.Value);
        }

        if (request.MinArea.HasValue)
        {
            query = query.Where(parcel => parcel.AreaValue >= request.MinArea.Value);
        }

        if (request.MaxArea.HasValue)
        {
            query = query.Where(parcel => parcel.AreaValue <= request.MaxArea.Value);
        }

        if (request.MinLatitude.HasValue
            && request.MaxLatitude.HasValue
            && request.MinLongitude.HasValue
            && request.MaxLongitude.HasValue)
        {
            var envelope = new Envelope(
                request.MinLongitude.Value,
                request.MaxLongitude.Value,
                request.MinLatitude.Value,
                request.MaxLatitude.Value);

            var boundingBox = GeometryFactory.ToGeometry(envelope);
            query = query.Where(parcel => parcel.Centroid.Intersects(boundingBox));
        }

        return query;
    }
}
