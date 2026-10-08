using System.Text.Json.Serialization;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.WorkflowAnomaly;

internal sealed record WorkflowEventTransportRequest(
    [property: JsonPropertyName("event_seq")] int EventSeq,
    [property: JsonPropertyName("activity")] string Activity,
    [property: JsonPropertyName("institution")] string Institution,
    [property: JsonPropertyName("resource")] string Resource,
    [property: JsonPropertyName("timestamp")] string Timestamp);

internal sealed record WorkflowAnomalyTransportRequest(
    [property: JsonPropertyName("case_id")] string CaseId,
    [property: JsonPropertyName("events")] IReadOnlyList<WorkflowEventTransportRequest> Events);

internal sealed record WorkflowAnomalyTransportResponse(
    [property: JsonPropertyName("case_id"), JsonRequired] string? CaseId,
    [property: JsonPropertyName("model_version"), JsonRequired] string? ModelVersion,
    [property: JsonPropertyName("anomaly_score"), JsonRequired] double? AnomalyScore,
    [property: JsonPropertyName("threshold"), JsonRequired] double? Threshold,
    [property: JsonPropertyName("flagged"), JsonRequired] bool? Flagged,
    [property: JsonPropertyName("feature_values"), JsonRequired] Dictionary<string, double>? FeatureValues,
    [property: JsonPropertyName("advisory_note"), JsonRequired] string? AdvisoryNote);
