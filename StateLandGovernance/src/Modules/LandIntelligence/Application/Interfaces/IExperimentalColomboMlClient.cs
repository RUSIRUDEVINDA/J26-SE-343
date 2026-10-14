using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public sealed class ExperimentalColomboMlPrediction
{
    public required string PredictedLabel { get; init; }

    public required IReadOnlyDictionary<string, decimal> Probabilities { get; init; }

    public required string CandidateId { get; init; }

    public required string ModelDir { get; init; }

    public required IReadOnlyList<string> Limitations { get; init; }
}

public sealed class ExperimentalColomboMlEvidenceResult
{
    public required bool Attempted { get; init; }

    public required bool PredictionSupported { get; init; }

    public string? AbstentionReason { get; init; }

    public ExperimentalColomboMlPrediction? Prediction { get; init; }

    public ExperimentalColomboMlFeaturePayload? FeaturePayload { get; init; }

    public string? FailureReason { get; init; }

    public required IReadOnlyList<RecommendationEvidenceDto> Evidence { get; init; }
}

public interface IExperimentalColomboMlClient
{
    Task<ExperimentalColomboMlPrediction?> PredictAsync(
        ExperimentalColomboMlFeaturePayload payload,
        CancellationToken cancellationToken = default);

    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds experimental feature payload from live enrichment and optionally calls the
/// opt-in experimental ML service. Never changes rule-based scores.
/// </summary>
public interface IExperimentalColomboMlEvidenceService
{
    Task<ExperimentalColomboMlEvidenceResult> CollectAsync(
        LandParcel parcel,
        LandUseType requestedPurpose,
        CancellationToken cancellationToken = default);
}
