using System.Text.Json;
using System.Text.Json.Serialization;
using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Infrastructure.PilotValidation;

public static class HambantotaPilotValidationReportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ToJson(HambantotaPilotValidationReport report) =>
        JsonSerializer.Serialize(report, SerializerOptions);

    public static string ToHumanReadableSummary(HambantotaPilotValidationReport report)
    {
        var lines = new List<string>
        {
            "H13 — Hambantota Pilot Validation Report",
            $"Generated: {report.GeneratedAt:O}",
            $"Pilot district: {report.PilotDistrict}",
            $"Neo4j available: {report.Neo4jAvailable}",
            $"Scenarios: {report.Summary.ScenarioCount}",
            $"GIS available/partial/unavailable: {report.Summary.ScenariosWithAvailableGis}/" +
                $"{report.Summary.ScenariosWithPartialGis}/{report.Summary.ScenariosWithUnavailableGis}",
            $"Recommendations produced: {report.Summary.RecommendationsProduced}/{report.Summary.RecommendationRuns}",
            $"Behaviour checks passed: {report.Summary.BehaviourChecksPassed}/{report.Summary.BehaviourChecksTotal}",
            string.Empty,
            "Scenarios:"
        };

        foreach (var scenario in report.Scenarios)
        {
            lines.Add($"- {scenario.Metrics.ScenarioKey}: {scenario.Metrics.GisEnrichmentOverallStatus}" +
                      $" | road={FormatNullableDistance(scenario.Metrics.MappedRoadDistanceMeters)}" +
                      $" | water={FormatNullableDistance(scenario.Metrics.MappedWaterDistanceMeters)}" +
                      $" | conservation={scenario.Metrics.ConservationIntersection?.ToString() ?? "n/a"}" +
                      $" | erosion={scenario.Metrics.ErosionDataAvailability}" +
                      $" | synthetic={scenario.Metrics.UsesSyntheticGisData}");

            foreach (var recommendation in scenario.Recommendations)
            {
                lines.Add(
                    $"    {recommendation.RequestedPurpose}: rank={recommendation.FinalRank?.ToString() ?? "n/a"}" +
                    $", score={recommendation.SuitabilityScore?.ToString("F2") ?? "n/a"}" +
                    $", recommended={recommendation.IsRecommended}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatNullableDistance(decimal? meters) =>
        meters.HasValue ? $"{meters.Value:F2}m" : "unavailable";
}
