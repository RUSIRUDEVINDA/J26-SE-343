using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Integrations;

/// <summary>
/// HTTP client for experimental_ml_service.py (Colombo backend-compatible candidate only).
/// Soft-fails on transport/protocol errors so rule-based recommendations remain usable.
/// </summary>
public sealed class HttpExperimentalColomboMlClient : IExperimentalColomboMlClient
{
    private readonly HttpClient _httpClient;
    private readonly ExperimentalColomboMlOptions _options;
    private readonly ILogger<HttpExperimentalColomboMlClient> _logger;

    public HttpExperimentalColomboMlClient(
        HttpClient httpClient,
        IOptions<ExperimentalColomboMlOptions> options,
        ILogger<HttpExperimentalColomboMlClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/health", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var health = await response.Content.ReadFromJsonAsync<HealthResponse>(cancellationToken: cancellationToken);
            var candidate = health?.ModelIdentity?.Candidate;
            return string.Equals(candidate, _options.ExpectedCandidateId, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Experimental Colombo ML health check failed.");
            return false;
        }
    }

    public async Task<ExperimentalColomboMlPrediction?> PredictAsync(
        ExperimentalColomboMlFeaturePayload payload,
        CancellationToken cancellationToken = default)
    {
        ExperimentalColomboMlCallLog.RecordPredictAttempt();
        try
        {
            var body = new PredictRequest(
                payload.RequestedPurpose,
                payload.LandCategory,
                payload.AreaHectares,
                payload.DistanceToRoadM,
                payload.DistanceToWaterM,
                payload.GisEnrichmentStatus,
                payload.DerivedSoilGroup,
                payload.SpatialConstraintPresent);

            using var response = await _httpClient.PostAsJsonAsync("/predict", body, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Experimental Colombo ML returned {StatusCode}.",
                    response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<PredictResponse>(cancellationToken: cancellationToken);
            if (result is null)
            {
                return null;
            }

            var candidate = result.ModelIdentity?.Candidate ?? string.Empty;
            if (!string.Equals(candidate, _options.ExpectedCandidateId, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Experimental Colombo ML schema/candidate mismatch: got {Candidate}, expected {Expected}.",
                    candidate,
                    _options.ExpectedCandidateId);
                return null;
            }

            return new ExperimentalColomboMlPrediction
            {
                PredictedLabel = result.PredictedLabel,
                Probabilities = result.Probabilities.ToDictionary(
                    kv => kv.Key,
                    kv => (decimal)kv.Value),
                CandidateId = candidate,
                ModelDir = result.ModelIdentity?.ModelDir ?? string.Empty,
                Limitations = result.Limitations ?? []
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Experimental Colombo ML unreachable; continuing without experimental prediction.");
            return null;
        }
    }

    private sealed record PredictRequest(
        [property: JsonPropertyName("requested_purpose")] string RequestedPurpose,
        [property: JsonPropertyName("land_category")] string LandCategory,
        [property: JsonPropertyName("area_hectares")] double AreaHectares,
        [property: JsonPropertyName("distance_to_road_m")] double? DistanceToRoadM,
        [property: JsonPropertyName("distance_to_water_m")] double? DistanceToWaterM,
        [property: JsonPropertyName("gis_enrichment_status")] string GisEnrichmentStatus,
        [property: JsonPropertyName("derived_soil_group")] string DerivedSoilGroup,
        [property: JsonPropertyName("spatial_constraint_present")] bool? SpatialConstraintPresent);

    private sealed record PredictResponse(
        [property: JsonPropertyName("predicted_label")] string PredictedLabel,
        [property: JsonPropertyName("probabilities")] Dictionary<string, double> Probabilities,
        [property: JsonPropertyName("model_identity")] ModelIdentityDto? ModelIdentity,
        [property: JsonPropertyName("limitations")] List<string>? Limitations);

    private sealed record HealthResponse(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("model_identity")] ModelIdentityDto? ModelIdentity);

    private sealed record ModelIdentityDto(
        [property: JsonPropertyName("candidate")] string? Candidate,
        [property: JsonPropertyName("model_dir")] string? ModelDir);
}

public sealed class NullExperimentalColomboMlClient : IExperimentalColomboMlClient
{
    public Task<ExperimentalColomboMlPrediction?> PredictAsync(
        ExperimentalColomboMlFeaturePayload payload,
        CancellationToken cancellationToken = default)
    {
        ExperimentalColomboMlCallLog.RecordPredictAttempt();
        return Task.FromResult<ExperimentalColomboMlPrediction?>(null);
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
