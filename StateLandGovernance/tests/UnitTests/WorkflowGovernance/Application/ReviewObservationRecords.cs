namespace StateLandGovernance.UnitTests.WorkflowGovernance.Application;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
internal sealed class ReviewObservationRecords : ICorrectionRepository {
 public List<CorrectionRecord> Saved {get;} = new();
 public Task<CorrectionRecord?> GetAsync(Guid caseId, Guid requestId, CancellationToken cancellationToken = default) => Task.FromResult(Saved.SingleOrDefault(r=>r.LeaseCaseId==caseId && r.Id==requestId));
 public Task<IReadOnlyList<CorrectionRecord>> ListAsync(Guid caseId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CorrectionRecord>>(Saved.Where(r=>r.LeaseCaseId==caseId).ToList());
 public Task SaveAsync(CorrectionRecord record, CancellationToken cancellationToken = default) { Saved.Add(record);return Task.CompletedTask; }
}
