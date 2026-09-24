using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Integrations;

public sealed class ExperimentalColomboMlEvidenceService : IExperimentalColomboMlEvidenceService
{
    private readonly ExperimentalColomboMlOptions _options;
    private readonly IExperimentalColomboMlFeatureAdapter _adapter;
    private readonly IExperimentalColomboMlClient _client;
    private readonly ILandParcelGisEnrichmentService _gisEnrichment;
    private readonly ILogger<ExperimentalColomboMlEvidenceService> _logger;

    public ExperimentalColomboMlEvidenceService(
        IOptions<ExperimentalColomboMlOptions> options,
        IExperimentalColomboMlFeatureAdapter adapter,
        IExperimentalColomboMlClient client,
        ILandParcelGisEnrichmentService gisEnrichment,
        ILogger<ExperimentalColomboMlEvidenceService> logger)
    {
        _options = options.Value;
        _adapter = adapter;
        _client = client;
        _gisEnrichment = gisEnrichment;
        _logger = logger;
    }

    public async Task<ExperimentalColomboMlEvidenceResult> CollectAsync(
        LandParcel parcel,
        LandUseType requestedPurpose,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new ExperimentalColomboMlEvidenceResult
            {
                Attempted = false,
                PredictionSupported = false,
                Evidence = []
            };
        }

        var overall = await _gisEnrichment.EnrichAsync(parcel.Id, cancellationToken);
        var payload = _adapter.BuildPayload(
            parcel,
            requestedPurpose,
            overall.RoadAccessibility,
            overall.WaterProximity,
            overall.Soil,
            overall.Environmental,
            overall.OverallStatus);

        if (!payload.ExperimentalPredictionSupported)
        {
            var reason = payload.ExperimentalPredictionAbstentionReason
                ?? "Experimental prediction abstained: required GIS evidence unavailable.";
            return new ExperimentalColomboMlEvidenceResult
            {
                Attempted = true,
                PredictionSupported = false,
                AbstentionReason = reason,
                FeaturePayload = payload,
                Evidence =
                [
                    new RecommendationEvidenceDto(
                        Source: "ExperimentalColomboOsmRf",
                        Description:
                            $"Experimental Colombo ML abstained ({_options.ExpectedCandidateId}). {reason} "
                            + "Conservation nulls were not coerced to false. Rule-based score unchanged.",
                        RelatedCriterionName: "ExperimentalColomboMlAbstention")
                ]
            };
        }

        ExperimentalColomboMlPrediction? prediction;
        try
        {
            prediction = await _client.PredictAsync(payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Experimental Colombo ML predict failed unexpectedly.");
            prediction = null;
        }

        if (prediction is null)
        {
            const string failure =
                "Experimental Colombo ML service unavailable, timed out, returned a malformed response, "
                + "or failed schema/candidate validation. Rule-based recommendation preserved; "
                + "no experimental prediction attached.";
            return new ExperimentalColomboMlEvidenceResult
            {
                Attempted = true,
                PredictionSupported = true,
                FeaturePayload = payload,
                FailureReason = failure,
                Evidence =
                [
                    new RecommendationEvidenceDto(
                        Source: "ExperimentalColomboOsmRf",
                        Description: failure,
                        RelatedCriterionName: "ExperimentalColomboMlUnavailable")
                ]
            };
        }

        var confidence = prediction.Probabilities.TryGetValue(prediction.PredictedLabel, out var p)
            ? p
            : 0m;
        var limitations = string.Join(" ", prediction.Limitations.Take(3));
        return new ExperimentalColomboMlEvidenceResult
        {
            Attempted = true,
            PredictionSupported = true,
            FeaturePayload = payload,
            Prediction = prediction,
            Evidence =
            [
                new RecommendationEvidenceDto(
                    Source: "ExperimentalColomboOsmRf",
                    Description:
                        $"Experimental candidate '{prediction.CandidateId}' predicts '{prediction.PredictedLabel}' "
                        + $"({confidence:P0}). Model dir: {prediction.ModelDir}. "
                        + "Supplementary evidence only — does not change rule-based suitability score or hard constraints. "
                        + limitations,
                    RelatedCriterionName: "ExperimentalColomboMlPrediction")
            ]
        };
    }
}
