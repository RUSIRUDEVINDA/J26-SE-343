using System.Text.Json.Serialization;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.ComplaintClassification;

internal sealed record ComplaintClassifierRequest(
    [property: JsonPropertyName("complaint_text")] string ComplaintText,
    [property: JsonPropertyName("case_id")] string? CaseId);

internal sealed record ComplaintClassifierResponse(
    [property: JsonPropertyName("model_version"), JsonRequired] string? ModelVersion,
    [property: JsonPropertyName("predicted_category"), JsonRequired] string? PredictedCategory,
    [property: JsonPropertyName("class_probabilities"), JsonRequired] Dictionary<string, double>? ClassProbabilities,
    [property: JsonPropertyName("case_id"), JsonRequired] string? CaseId,
    [property: JsonPropertyName("advisory_note"), JsonRequired] string? AdvisoryNote,
    [property: JsonPropertyName("closed_set_note"), JsonRequired] string? ClosedSetNote);
