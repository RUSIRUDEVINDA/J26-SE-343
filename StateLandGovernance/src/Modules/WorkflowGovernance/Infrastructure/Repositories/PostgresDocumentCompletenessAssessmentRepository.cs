namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Persistence.Mappings;

public sealed class PostgresDocumentCompletenessAssessmentRepository : IDocumentCompletenessAssessmentRepository
{
    private readonly WorkflowGovernanceDbContext _dbContext;
    private readonly WorkflowGovernanceAggregateTracker _tracker;

    internal PostgresDocumentCompletenessAssessmentRepository(
        WorkflowGovernanceDbContext dbContext,
        WorkflowGovernanceAggregateTracker tracker)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
    }

    public async Task<DocumentCompletenessAssessment?> GetByIdAsync(DocumentCompletenessAssessmentId id, CancellationToken cancellationToken = default)
    {
        if (_tracker.TryGetDocumentCompletenessAssessment(id, out var existing))
        {
            return existing;
        }

        var entity = await _dbContext.DocumentCompletenessAssessments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var domain = DocumentCompletenessAssessmentPersistenceMapper.ToDomain(entity);
        _tracker.TrackLoaded(domain, domain.Revision);
        return domain;
    }

    public async Task<IReadOnlyList<DocumentCompletenessAssessment>> GetByLeaseCaseIdAsync(LeaseCaseId leaseCaseId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.DocumentCompletenessAssessments
            .AsNoTracking()
            .Where(x => x.LeaseCaseId == leaseCaseId.Value)
            .ToListAsync(cancellationToken);

        var result = new List<DocumentCompletenessAssessment>();
        var seenIds = new HashSet<Guid>();

        foreach (var entity in entities)
        {
            if (_tracker.TryGetDocumentCompletenessAssessment(new DocumentCompletenessAssessmentId(entity.Id), out var tracked) && tracked is not null)
            {
                result.Add(tracked);
                seenIds.Add(tracked.Id.Value);
            }
            else
            {
                var domain = DocumentCompletenessAssessmentPersistenceMapper.ToDomain(entity);
                _tracker.TrackLoaded(domain, domain.Revision);
                result.Add(domain);
                seenIds.Add(domain.Id.Value);
            }
        }

        // Include any pending new assessments in session
        foreach (var entry in _tracker.GetTrackedDocumentCompletenessAssessments())
        {
            if (entry.Aggregate.LeaseCaseId == leaseCaseId && !seenIds.Contains(entry.Aggregate.Id.Value))
            {
                result.Add(entry.Aggregate);
            }
        }

        return result;
    }

    public Task AddAsync(DocumentCompletenessAssessment assessment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        _tracker.TrackNew(assessment);
        return Task.CompletedTask;
    }
}

