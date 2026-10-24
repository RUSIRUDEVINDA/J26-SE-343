namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using StateLandGovernance.WorkflowGovernance.Application.DTOs;

public interface ICorrectionSubmissionTransaction
{
    Task<CorrectionRecord> ExecuteAsync(Func<Task<CorrectionRecord>> action, CancellationToken cancellationToken);
}
