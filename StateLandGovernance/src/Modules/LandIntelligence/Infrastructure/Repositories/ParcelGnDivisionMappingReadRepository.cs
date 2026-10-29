using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.RealParcels;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Repositories;

public sealed class ParcelGnDivisionMappingReadRepository : IParcelGnDivisionMappingReadRepository
{
    private readonly LandIntelligenceDbContext _dbContext;

    public ParcelGnDivisionMappingReadRepository(LandIntelligenceDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<ParcelGnDivisionMappingDto?> GetByCadastralNumberAsync(
        string cadastralNumber,
        CancellationToken cancellationToken = default)
    {
        var trimmed = cadastralNumber.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        var entity = await _dbContext.ParcelGnDivisionMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.CadastralNumber == trimmed, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<ParcelGnDivisionMappingDto?> GetByParcelIdAsync(
        Guid landParcelId,
        CancellationToken cancellationToken = default)
    {
        if (landParcelId == Guid.Empty)
        {
            return null;
        }

        var entity = await _dbContext.ParcelGnDivisionMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.LandParcelId == landParcelId, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<IReadOnlyList<string>> ListDistinctGnDivisionNamesAsync(
        string? district = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ParcelGnDivisionMappings
            .AsNoTracking()
            .Where(mapping =>
                mapping.SourceCodeGnName != null
                && mapping.SourceCodeGnName != ""
                && _dbContext.LandParcels.Any(parcel =>
                    !EF.Functions.ILike(parcel.CadastralNumber, "DEMO-%")
                    && (parcel.Id == mapping.LandParcelId
                        || parcel.CadastralNumber == mapping.CadastralNumber)));

        if (!string.IsNullOrWhiteSpace(district))
        {
            var trimmedDistrict = district.Trim();
            query = query.Where(mapping =>
                _dbContext.LandParcels.Any(parcel =>
                    !EF.Functions.ILike(parcel.CadastralNumber, "DEMO-%")
                    && (parcel.Id == mapping.LandParcelId
                        || parcel.CadastralNumber == mapping.CadastralNumber)
                    && EF.Functions.ILike(parcel.District, trimmedDistrict)));
        }

        return await query
            .Select(mapping => mapping.SourceCodeGnName!)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);
    }

    private static ParcelGnDivisionMappingDto ToDto(ParcelGnDivisionMappingEntity entity)
    {
        var record = new ParcelGnDivisionMappingRecord(
            entity.CadastralNumber,
            entity.DatasetKey,
            entity.SourceGnIndex,
            entity.SourceCodeGnName,
            entity.SourceCodeGnNumber,
            entity.SourceCodeDs,
            entity.CodeDistrict,
            entity.MarkerGnName,
            entity.MarkerGnNumber,
            entity.MarkerDs,
            entity.Status,
            entity.WorkbookStatusLabel,
            entity.MarkerLatitude,
            entity.MarkerLongitude,
            entity.ImportedAtUtc);
        return ParcelGnDivisionMappingProjector.ToPublicDto(record);
    }
}
