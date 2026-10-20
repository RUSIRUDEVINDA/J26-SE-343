namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using System.Text.Json;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

public interface IConsolidatedCaseScreeningAdapter
{
    Task EnsureCanEvaluateAsync(PreparedCaseScreening prepared, CancellationToken ct) => Task.CompletedTask;
    Task<PreparedCaseScreening> PrepareAsync(CaseScreeningSource source, CancellationToken cancellationToken);
    Task<JsonElement> EvaluateAsync(PreparedCaseScreening prepared, CancellationToken cancellationToken);
}
