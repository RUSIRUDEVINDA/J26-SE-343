using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Integrations;

/// <summary>
/// Calls the standalone Python ML service (see src/Modules/LandIntelligence/ML/ml_service.py)
/// which serves the trained Random Forest suitability model. This is an additional evidence
/// source only: it never overrides the rule-based hard constraints evaluated
/// by RuleBasedLandRecommendationEngine.
/// </summary>
public sealed class HttpMlSuitabilityClient : IMlSuitabilityClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpMlSuitabilityClient> _logger;

    public HttpMlSuitabilityClient(HttpClient httpClient, ILogger<HttpMlSuitabilityClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<MlSuitabilityPrediction?> PredictAsync(
        LandParcel parcel,
        LandUseType requestedPurpose,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = BuildRequestPayload(parcel, requestedPurpose);

            using var response = await _httpClient.PostAsJsonAsync("/predict", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ML suitability service returned {StatusCode} for parcel {CadastralNumber}",
                    response.StatusCode,
                    parcel.Identifier.CadastralNumber);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<MlPredictResponse>(cancellationToken: cancellationToken);
            if (result is null)
            {
                return null;
            }

            var probabilities = result.Probabilities.ToDictionary(
                kv => kv.Key,
                kv => (decimal)kv.Value);

            return new MlSuitabilityPrediction(result.PredictedLabel, probabilities);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(
                ex,
                "ML suitability service unreachable for parcel {CadastralNumber}; continuing without ML evidence.",
                parcel.Identifier.CadastralNumber);
            return null;
        }
    }

    internal static MlPredictRequest BuildRequestPayload(LandParcel parcel, LandUseType requestedPurpose)
    {
        var gisIntelligence = parcel.GisDerivedIntelligence;
        var gisStatus = gisIntelligence?.EnrichmentStatus ?? GisEnrichmentOverallStatus.Unavailable;
        var gisUnavailable = gisStatus == GisEnrichmentOverallStatus.Unavailable;

        var gisRoadFeature = parcel.InfrastructureFeatures
            .Where(f => f.Type is InfrastructureFeatureType.Road or InfrastructureFeatureType.Railway)
            .Where(GisDerivedIntelligenceDetector.IsGisDerivedInfrastructure)
            .OrderBy(f => f.DistanceMeters)
            .FirstOrDefault();

        var gisWaterFeature = parcel.InfrastructureFeatures
            .Where(GisDerivedIntelligenceDetector.IsGisDerivedNaturalWater)
            .OrderBy(f => f.DistanceMeters)
            .FirstOrDefault();

        var derivedSoilGroupName = gisIntelligence?.DerivedSoilGroup?.SoilGroupName;

        var worstRestriction = parcel.EnvironmentalRestrictions
            .OrderByDescending(r => r.Severity)
            .FirstOrDefault();

        return new MlPredictRequest(
            RequestedPurpose: requestedPurpose.ToString(),
            LandCategory: parcel.Category.Type.ToString(),
            AreaHectares: (double)parcel.Area.Value,
            Province: parcel.Location.Province,
            District: parcel.Location.District,
            ElevationMeters: ToNullableDouble(parcel.Characteristics?.ElevationMeters),
            DistanceToRoadM: gisUnavailable ? null : ToNullableDouble(gisRoadFeature?.DistanceMeters),
            DistanceToWaterM: gisUnavailable ? null : ToNullableDouble(gisWaterFeature?.DistanceMeters),
            GisEnrichmentStatus: gisStatus.ToString(),
            DerivedSoilGroup: gisUnavailable || string.IsNullOrWhiteSpace(derivedSoilGroupName)
                ? "Unknown"
                : derivedSoilGroupName,
            SoilOverlapPercentage: gisUnavailable || string.IsNullOrWhiteSpace(derivedSoilGroupName)
                ? null
                : ToNullableDouble(gisIntelligence?.DerivedSoilGroup?.OverlapPercentage),
            TerrainDescription: gisUnavailable
                ? "Unknown"
                : ResolveUnknownString(parcel.Characteristics?.TerrainDescription),
            EnvironmentalRestrictionType: worstRestriction?.Type.ToString() ?? "None",
            EnvironmentalRestrictionSeverity: worstRestriction?.Severity.ToString() ?? "None",
            SpatialConstraintPresent: parcel.SpatialConstraints.Count > 0);
    }

    private static double? ToNullableDouble(decimal? value) =>
        value.HasValue ? (double)value.Value : null;

    private static string ResolveUnknownString(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Unknown" : value;

    internal sealed record MlPredictRequest(
        [property: JsonPropertyName("requested_purpose")] string RequestedPurpose,
        [property: JsonPropertyName("land_category")] string LandCategory,
        [property: JsonPropertyName("area_hectares")] double AreaHectares,
        [property: JsonPropertyName("province")] string Province,
        [property: JsonPropertyName("district")] string District,
        [property: JsonPropertyName("elevation_meters")] double? ElevationMeters,
        [property: JsonPropertyName("distance_to_road_m")] double? DistanceToRoadM,
        [property: JsonPropertyName("distance_to_water_m")] double? DistanceToWaterM,
        [property: JsonPropertyName("gis_enrichment_status")] string GisEnrichmentStatus,
        [property: JsonPropertyName("derived_soil_group")] string DerivedSoilGroup,
        [property: JsonPropertyName("soil_overlap_percentage")] double? SoilOverlapPercentage,
        [property: JsonPropertyName("terrain_description")] string TerrainDescription,
        [property: JsonPropertyName("environmental_restriction_type")] string EnvironmentalRestrictionType,
        [property: JsonPropertyName("environmental_restriction_severity")] string EnvironmentalRestrictionSeverity,
        [property: JsonPropertyName("spatial_constraint_present")] bool SpatialConstraintPresent);

    private sealed record MlPredictResponse(
        [property: JsonPropertyName("predicted_label")] string PredictedLabel,
        [property: JsonPropertyName("probabilities")] Dictionary<string, double> Probabilities);
}
