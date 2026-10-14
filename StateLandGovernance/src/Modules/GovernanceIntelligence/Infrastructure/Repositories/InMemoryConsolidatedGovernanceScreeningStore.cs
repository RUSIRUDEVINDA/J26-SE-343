using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Contracts;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Interfaces;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

/// <summary>
/// Thread-safe in-memory store for consolidated governance screening assessments.
/// Enables deterministic unit testing and standalone operation without database locks.
/// </summary>
public sealed class InMemoryConsolidatedGovernanceScreeningStore : IConsolidatedGovernanceScreeningStore
{
    private readonly ConcurrentDictionary<Guid, ConsolidatedGovernanceScreeningResultDto> _byId = new();
    private readonly ConcurrentDictionary<string, List<ConsolidatedGovernanceScreeningResultDto>> _byCaseId = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveAsync(
        ConsolidatedGovernanceScreeningResultDto assessment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        cancellationToken.ThrowIfCancellationRequested();

        _byId[assessment.AssessmentId] = assessment;

        _byCaseId.AddOrUpdate(
            assessment.CaseId,
            _ => new List<ConsolidatedGovernanceScreeningResultDto> { assessment },
            (_, list) =>
            {
                lock (list)
                {
                    // Remove existing entry with same assessment ID if updating
                    list.RemoveAll(x => x.AssessmentId == assessment.AssessmentId);
                    list.Insert(0, assessment);
                }
                return list;
            });

        return Task.CompletedTask;
    }

    public Task<ConsolidatedGovernanceScreeningResultDto?> GetByIdAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _byId.TryGetValue(assessmentId, out var item);
        return Task.FromResult(item);
    }

    public Task<ConsolidatedGovernanceScreeningResultDto?> GetLatestByCaseIdAsync(
        string caseId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caseId);
        cancellationToken.ThrowIfCancellationRequested();

        if (_byCaseId.TryGetValue(caseId.Trim(), out var list))
        {
            lock (list)
            {
                return Task.FromResult(list.FirstOrDefault());
            }
        }

        return Task.FromResult<ConsolidatedGovernanceScreeningResultDto?>(null);
    }

    public Task<ConsolidatedGovernanceScreeningResultDto?> FindByIdempotencyKeyAsync(
        string caseId,
        string proposalVersion,
        string snapshotSha256,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caseId);
        ArgumentNullException.ThrowIfNull(snapshotSha256);
        cancellationToken.ThrowIfCancellationRequested();

        if (_byCaseId.TryGetValue(caseId.Trim(), out var list))
        {
            lock (list)
            {
                var match = list.FirstOrDefault(x =>
                    string.Equals(x.ProposalVersion, proposalVersion, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.SnapshotSha256, snapshotSha256, StringComparison.Ordinal));
                return Task.FromResult(match);
            }
        }

        return Task.FromResult<ConsolidatedGovernanceScreeningResultDto?>(null);
    }

    public Task<IReadOnlyList<ConsolidatedGovernanceScreeningResultDto>> GetHistoryByCaseIdAsync(
        string caseId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caseId);
        cancellationToken.ThrowIfCancellationRequested();

        if (_byCaseId.TryGetValue(caseId.Trim(), out var list))
        {
            lock (list)
            {
                var result = list.Take(Math.Max(1, limit)).ToList();
                return Task.FromResult<IReadOnlyList<ConsolidatedGovernanceScreeningResultDto>>(result);
            }
        }

        return Task.FromResult<IReadOnlyList<ConsolidatedGovernanceScreeningResultDto>>(Array.Empty<ConsolidatedGovernanceScreeningResultDto>());
    }
}

