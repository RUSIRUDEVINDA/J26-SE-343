namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;

public sealed class PostgresDocumentAnalysisRepository : IDocumentAnalysisRepository
{
    private readonly WorkflowGovernanceDbContext _dbContext;
    private readonly WorkflowGovernanceAggregateTracker _tracker;

    internal PostgresDocumentAnalysisRepository(
        WorkflowGovernanceDbContext dbContext,
        WorkflowGovernanceAggregateTracker tracker)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
    }

    public async Task<DocumentAnalysis?> GetByIdAsync(DocumentAnalysisId id, CancellationToken cancellationToken = default)
    {
        if (_tracker.TryGetDocumentAnalysis(id, out var existing))
        {
            return existing;
        }

        var entity = await _dbContext.DocumentAnalyses
            .AsNoTracking()
            .Include(x => x.Runs)
            .Include(x => x.Verifications)
            .Include(x => x.VerifiedFactSnapshots)
            .FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var domain = DocumentAnalysisPersistenceMapper.ToDomain(entity);
        _tracker.TrackLoaded(domain, domain.Revision);
        return domain;
    }

    public async Task<DocumentAnalysis?> GetByDocumentVersionIdAsync(DocumentVersionId documentVersionId, CancellationToken cancellationToken = default)
    {
        if (_tracker.TryGetDocumentAnalysisByVersion(documentVersionId, out var existing))
        {
            return existing;
        }

        var entity = await _dbContext.DocumentAnalyses
            .AsNoTracking()
            .Include(x => x.Runs)
            .Include(x => x.Verifications)
            .Include(x => x.VerifiedFactSnapshots)
            .FirstOrDefaultAsync(x => x.DocumentVersionId == documentVersionId.Value, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var domain = DocumentAnalysisPersistenceMapper.ToDomain(entity);
        _tracker.TrackLoaded(domain, domain.Revision);
        return domain;
    }

    public async Task<VerifiedFactSnapshot?> GetSnapshotByIdAsync(VerifiedFactSnapshotId snapshotId, CancellationToken cancellationToken = default)
    {
        // Check session-tracked analyses first
        foreach (var entry in _tracker.GetTrackedDocumentAnalyses())
        {
            var match = entry.Aggregate.VerifiedFactSnapshots.FirstOrDefault(s => s.Id == snapshotId);
            if (match is not null)
            {
                return match;
            }
        }

        // Query database directly
        var entity = await _dbContext.VerifiedFactSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == snapshotId.Value, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        return DocumentAnalysisPersistenceMapper.ToDomainSnapshot(entity);
    }

    public Task AddAsync(DocumentAnalysis documentAnalysis, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentAnalysis);
        _tracker.TrackNew(documentAnalysis);
        return Task.CompletedTask;
    }
}

