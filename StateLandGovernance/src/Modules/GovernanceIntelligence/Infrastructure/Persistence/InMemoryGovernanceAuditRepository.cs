using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;

/// <summary>
/// Thread-safe in-memory implementation of the IGovernanceAuditRepository for testing and mock environments.
/// </summary>
public sealed class InMemoryGovernanceAuditRepository : IGovernanceAuditRepository
{
    private readonly ConcurrentDictionary<Guid, GovernanceAuditRecord> _records = new();

    public Task<GovernanceAuditRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _records.TryGetValue(id, out var record);
        return Task.FromResult(record);
    }

    public Task AddAsync(GovernanceAuditRecord record, CancellationToken cancellationToken = default)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        _records[record.Id] = record;
        return Task.CompletedTask;
    }
}
