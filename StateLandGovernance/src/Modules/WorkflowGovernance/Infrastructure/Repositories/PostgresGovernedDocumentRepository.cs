namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;

public sealed class PostgresGovernedDocumentRepository : IGovernedDocumentRepository
{
    private readonly WorkflowGovernanceDbContext _dbContext;
    private readonly WorkflowGovernanceAggregateTracker _tracker;

    internal PostgresGovernedDocumentRepository(
        WorkflowGovernanceDbContext dbContext,
        WorkflowGovernanceAggregateTracker tracker)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
    }

    public async Task<GovernedDocument?> GetByIdAsync(GovernedDocumentId id, CancellationToken cancellationToken = default)
    {
        if (_tracker.TryGetGovernedDocument(id, out var existing))
        {
            return existing;
        }

        var entity = await _dbContext.GovernedDocuments
            .AsNoTracking()
            .Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var domain = GovernedDocumentPersistenceMapper.ToDomain(entity);
        _tracker.TrackLoaded(domain, domain.Revision);
        return domain;
    }

    public async Task<IReadOnlyList<GovernedDocument>> GetByLeaseCaseIdAsync(LeaseCaseId leaseCaseId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.GovernedDocuments
            .AsNoTracking()
            .Include(x => x.Versions)
            .Where(x => x.LeaseCaseId == leaseCaseId.Value)
            .ToListAsync(cancellationToken);

        var result = new List<GovernedDocument>();
        var seenIds = new HashSet<Guid>();

        foreach (var entity in entities)
        {
            if (_tracker.TryGetGovernedDocument(new GovernedDocumentId(entity.Id), out var tracked) && tracked is not null)
            {
                result.Add(tracked);
                seenIds.Add(tracked.Id.Value);
            }
            else
            {
                var domain = GovernedDocumentPersistenceMapper.ToDomain(entity);
                _tracker.TrackLoaded(domain, domain.Revision);
                result.Add(domain);
                seenIds.Add(domain.Id.Value);
            }
        }

        // Include any pending new documents tracked in session
        foreach (var entry in _tracker.GetTrackedGovernedDocuments())
        {
            if (entry.Aggregate.LeaseCaseId == leaseCaseId && !seenIds.Contains(entry.Aggregate.Id.Value))
            {
                result.Add(entry.Aggregate);
            }
        }

        return result;
    }

    public Task AddAsync(GovernedDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        _tracker.TrackNew(document);
        return Task.CompletedTask;
    }
}

