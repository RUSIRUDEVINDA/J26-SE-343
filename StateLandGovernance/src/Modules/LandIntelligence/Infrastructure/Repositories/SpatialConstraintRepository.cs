using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Mappings;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Repositories;

public sealed class SpatialConstraintRepository : ISpatialConstraintRepository
{
    private readonly LandIntelligenceDbContext _dbContext;

    public SpatialConstraintRepository(LandIntelligenceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SpatialConstraint>> GetByParcelIdAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.SpatialConstraints
            .AsNoTracking()
            .Where(constraint => constraint.LandParcelId == parcelId)
            .OrderBy(constraint => constraint.Type)
            .ToListAsync(cancellationToken);

        return entities.Select(LandParcelPersistenceMapper.ToDomain).ToList();
    }
}
