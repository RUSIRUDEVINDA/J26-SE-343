namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Repositories;

using System.Collections.Concurrent;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

/// <summary>Development/Testing only; process-local.</summary>
public sealed class InMemoryDocumentCompletenessAssessmentRepository : IDocumentCompletenessAssessmentRepository
{
    private readonly ConcurrentDictionary<Guid, DocumentCompletenessAssessment> _items = new();
    public Task<DocumentCompletenessAssessment?> GetByIdAsync(DocumentCompletenessAssessmentId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_items.GetValueOrDefault(id.Value));
    public Task<IReadOnlyList<DocumentCompletenessAssessment>> GetByLeaseCaseIdAsync(LeaseCaseId id, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DocumentCompletenessAssessment>>(_items.Values.Where(x => x.LeaseCaseId == id).ToList());
    public Task AddAsync(DocumentCompletenessAssessment assessment, CancellationToken cancellationToken = default)
    {
        _items[assessment.Id.Value] = assessment;
        return Task.CompletedTask;
    }
}
