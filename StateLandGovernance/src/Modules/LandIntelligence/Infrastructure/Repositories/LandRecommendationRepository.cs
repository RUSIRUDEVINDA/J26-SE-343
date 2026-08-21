using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Mappings;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Repositories;

public sealed class LandRecommendationRepository : ILandRecommendationRepository
{
    private readonly LandIntelligenceDbContext _dbContext;

    public LandRecommendationRepository(LandIntelligenceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LandRecommendation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.LandRecommendations
            .AsNoTracking()
            .FirstOrDefaultAsync(recommendation => recommendation.Id == id, cancellationToken);

        return entity is null ? null : LandRecommendationPersistenceMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<LandRecommendation>> GetByParcelIdAsync(
        Guid parcelId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.LandRecommendations
            .AsNoTracking()
            .Where(recommendation => recommendation.LandParcelId == parcelId)
            .OrderByDescending(recommendation => recommendation.GeneratedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(LandRecommendationPersistenceMapper.ToDomain).ToList();
    }

    public async Task AddAsync(LandRecommendation recommendation, CancellationToken cancellationToken = default)
    {
        _dbContext.LandRecommendations.Add(LandRecommendationPersistenceMapper.ToPersistence(recommendation));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(LandRecommendation recommendation, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.LandRecommendations
            .FirstOrDefaultAsync(item => item.Id == recommendation.Id, cancellationToken);

        if (entity is null)
        {
            _dbContext.LandRecommendations.Add(LandRecommendationPersistenceMapper.ToPersistence(recommendation));
        }
        else
        {
            var updated = LandRecommendationPersistenceMapper.ToPersistence(recommendation);
            entity.LandParcelId = updated.LandParcelId;
            entity.SuitabilityScore = updated.SuitabilityScore;
            entity.Rank = updated.Rank;
            entity.Status = updated.Status;
            entity.RecommendedUseType = updated.RecommendedUseType;
            entity.RecommendedUseDescription = updated.RecommendedUseDescription;
            entity.GeneratedAt = updated.GeneratedAt;
            entity.CriteriaJson = updated.CriteriaJson;
            entity.EvidenceJson = updated.EvidenceJson;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
