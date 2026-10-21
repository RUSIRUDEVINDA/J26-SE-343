namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;

public sealed class PostgresLeaseCaseRepository : ILeaseCaseRepository
{
    private readonly WorkflowGovernanceDbContext _dbContext;
    private readonly WorkflowGovernanceAggregateTracker _tracker;

    internal PostgresLeaseCaseRepository(
        WorkflowGovernanceDbContext dbContext,
        WorkflowGovernanceAggregateTracker tracker)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
    }

    public async Task<LeaseCase?> GetByIdAsync(LeaseCaseId id, CancellationToken cancellationToken = default)
    {
        if (_tracker.TryGetLeaseCase(id, out var existing))
        {
            return existing;
        }

        var entity = await _dbContext.LeaseCases
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var domain = LeaseCasePersistenceMapper.ToDomain(entity);
        _tracker.TrackLoaded(domain, domain.Revision);
        return domain;
    }

    public async Task<LeaseCase?> GetByApplicationReferenceAsync(string applicationReference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationReference);

        var tracked = _tracker.GetTrackedLeaseCases()
            .FirstOrDefault(e => string.Equals(e.Aggregate.ApplicationReference, applicationReference, StringComparison.OrdinalIgnoreCase));

        if (tracked is not null)
        {
            return tracked.Aggregate;
        }

        var entity = await _dbContext.LeaseCases
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ApplicationReference == applicationReference, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var domain = LeaseCasePersistenceMapper.ToDomain(entity);
        _tracker.TrackLoaded(domain, domain.Revision);
        return domain;
    }

    public Task AddAsync(LeaseCase leaseCase, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(leaseCase);
        _tracker.TrackNew(leaseCase);
        return Task.CompletedTask;
    }
}

