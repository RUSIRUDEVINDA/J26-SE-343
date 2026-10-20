namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;

using System.Collections.Concurrent;
using System.Text.Json;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

/// <summary>Explicit Development/Testing fallback, not durable.</summary>
public sealed class InMemoryCorrectionRepository : ICorrectionRepository
{
    private readonly ConcurrentDictionary<Guid, string> _records = new();
    public Task<CorrectionRecord?> GetAsync(Guid caseId, Guid requestId, CancellationToken cancellationToken = default)
    {
        var record = _records.TryGetValue(requestId, out var json) ? JsonSerializer.Deserialize<CorrectionRecord>(json) : null;
        return Task.FromResult(record?.LeaseCaseId == caseId ? record : null);
    }
    public Task<IReadOnlyList<CorrectionRecord>> ListAsync(Guid caseId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CorrectionRecord>>(_records.Values.Select(x => JsonSerializer.Deserialize<CorrectionRecord>(x)!)
            .Where(x => x.LeaseCaseId == caseId).ToList());
    public Task SaveAsync(CorrectionRecord record, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(record);
        if (_records.TryGetValue(record.Id, out var original))
        {
            var prior = JsonSerializer.Deserialize<CorrectionRecord>(original)!;
            if (prior.Revision != record.Revision - 1 || !_records.TryUpdate(record.Id, json, original))
                throw new InvalidOperationException("Correction revision conflict.");
        }
        else if (!_records.TryAdd(record.Id, json)) throw new InvalidOperationException("Correction already exists.");
        return Task.CompletedTask;
    }
}
