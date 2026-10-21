using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Integrations;

/// <summary>
/// Opt-in research model for the frozen, expert-reviewed Colombo 400 dataset.
/// The separate Python service owns the exact training feature snapshot; this
/// adapter never substitutes live GIS values or changes rule-based ranking.
/// </summary>
public sealed class HttpColombo400ResearchMlClient : IMlSuitabilityClient
{
    private readonly HttpClient _httpClient;
    private readonly IRecommendationParcelContext _parcelContext;
    private readonly ILogger<HttpColombo400ResearchMlClient> _logger;

    public HttpColombo400ResearchMlClient(
        HttpClient httpClient,
        IRecommendationParcelContext parcelContext,
        ILogger<HttpColombo400ResearchMlClient> logger)
    {
        _httpClient = httpClient;
        _parcelContext = parcelContext;
        _logger = logger;
    }

    public async Task<MlSuitabilityPrediction?> PredictAsync(
        LandParcel parcel,
        LandUseType requestedPurpose,
        CancellationToken cancellationToken = default)
    {
        var source = _parcelContext.GetSource(parcel.Id);
        if (!string.Equals(parcel.Location.District, "Colombo", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(source?.ResearchRecordId)
            || requestedPurpose is not (LandUseType.Agricultural or LandUseType.Residential or LandUseType.Commercial))
        {
            return null;
        }

        try
        {
            var payload = new PredictRequest(
                parcel.Identifier.CadastralNumber,
                requestedPurpose.ToString(),
                (double)parcel.Area.ToHectares());
            using var response = await _httpClient.PostAsJsonAsync("/predict", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Colombo 400 research ML returned {StatusCode} for cadastral parcel {CadastralNumber}",
                    response.StatusCode,
                    parcel.Identifier.CadastralNumber);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<PredictResponse>(cancellationToken: cancellationToken);
            if (result is null || string.IsNullOrWhiteSpace(result.PredictedLabel))
                return null;
            if (requestedPurpose == LandUseType.Commercial
                && string.Equals(result.PredictedLabel, "Suitable", StringComparison.Ordinal))
            {
                _logger.LogError("Commercial Suitable policy violation for parcel {CadastralNumber}",
                    parcel.Identifier.CadastralNumber);
                return null;
            }

            return new MlSuitabilityPrediction(
                result.PredictedLabel,
                result.Probabilities.ToDictionary(kv => kv.Key, kv => (decimal)kv.Value),
                "Colombo research model (expert-labelled 400)");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Colombo 400 research ML is unavailable; rule-based recommendation continues.");
            return null;
        }
    }

    private sealed record PredictRequest(
        [property: JsonPropertyName("cadastral_number")] string CadastralNumber,
        [property: JsonPropertyName("requested_purpose")] string RequestedPurpose,
        [property: JsonPropertyName("area_hectares")] double AreaHectares);

    private sealed record PredictResponse(
        [property: JsonPropertyName("predicted_label")] string PredictedLabel,
        [property: JsonPropertyName("probabilities")] Dictionary<string, double> Probabilities);
}
