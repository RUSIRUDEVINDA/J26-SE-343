namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
public interface ICaseScreeningReadiness
{
    Task<CaseScreeningState> GetAsync(Guid caseId, Guid? snapshotId, Guid? parcelId, CancellationToken ct);
}
