using System.Collections.Concurrent;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;

/// <summary>
/// Process-local lease-case store for Development / Testing only.
/// Data is lost on process exit and must not be registered in Production.
/// </summary>
internal sealed class InMemoryLeaseCaseRepository : ILeaseCaseRepository
{
    private readonly ConcurrentDictionary<Guid, LeaseCase> _byId = new();
    private readonly ConcurrentDictionary<string, LeaseCase> _byReference =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<LeaseCase?> GetByIdAsync(LeaseCaseId id, CancellationToken cancellationToken = default)
    {
        _byId.TryGetValue(id.Value, out var found);
        return Task.FromResult(found);
    }

    public Task<LeaseCase?> GetByApplicationReferenceAsync(
        string applicationReference,
        CancellationToken cancellationToken = default)
    {
        _byReference.TryGetValue(applicationReference, out var found);
        return Task.FromResult(found);
    }

    public Task AddAsync(LeaseCase leaseCase, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(leaseCase);

        if (!_byId.TryAdd(leaseCase.Id.Value, leaseCase))
        {
            throw new InvalidOperationException(
                $"Lease case '{leaseCase.Id.Value}' is already tracked.");
        }

        if (!_byReference.TryAdd(leaseCase.ApplicationReference, leaseCase))
        {
            _byId.TryRemove(leaseCase.Id.Value, out _);
            throw new InvalidOperationException(
                $"Application reference '{leaseCase.ApplicationReference}' is already tracked.");
        }

        return Task.CompletedTask;
    }

    public Task<(System.Collections.Generic.IReadOnlyList<LeaseCase> Items, int TotalCount)> ListAsync(
        int skip,
        int take,
        Guid? ownerActorId = null,
        CancellationToken cancellationToken = default)
    {
        var boundedTake = Math.Clamp(take, 1, 100);
        var boundedSkip = Math.Max(0, skip);

        var query = _byId.Values.AsEnumerable();

        if (ownerActorId.HasValue && ownerActorId.Value != Guid.Empty)
        {
            query = query.Where(x => x.CreatedByActorId == ownerActorId.Value);
        }

        var total = query.Count();
        var items = query
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id.Value)
            .Skip(boundedSkip)
            .Take(boundedTake)
            .ToList();

        return Task.FromResult< (System.Collections.Generic.IReadOnlyList<LeaseCase>, int) >((items, total));
    }
}
