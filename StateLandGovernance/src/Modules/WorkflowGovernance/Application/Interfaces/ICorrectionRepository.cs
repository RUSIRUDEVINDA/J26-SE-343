namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using StateLandGovernance.WorkflowGovernance.Application.DTOs;

public interface ICorrectionRepository
{
    Task<CorrectionRecord?> GetAsync(Guid caseId, Guid requestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CorrectionRecord>> ListAsync(Guid caseId, CancellationToken cancellationToken = default);
    Task SaveAsync(CorrectionRecord record, CancellationToken cancellationToken = default);
}
