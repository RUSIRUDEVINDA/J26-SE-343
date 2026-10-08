using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;

namespace StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

/// <summary>
/// Application boundary for advisory workflow anomaly detection on complete traces.
/// </summary>
public interface IWorkflowAnomalyClient
{
    Task<WorkflowAnomalyResult> PredictAnomalyAsync(
        string caseId,
        IReadOnlyList<WorkflowAnomalyEvent> events,
        CancellationToken cancellationToken = default);
}
