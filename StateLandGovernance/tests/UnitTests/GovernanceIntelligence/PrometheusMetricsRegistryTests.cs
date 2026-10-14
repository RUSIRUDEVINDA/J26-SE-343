using StateLandGovernance.BuildingBlocks.Observability;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public sealed class PrometheusMetricsRegistryTests
{
    [Fact]
    public void Render_WhenMeasurementsRecorded_EmitsDashboardMetricSeries()
    {
        var registry = new PrometheusMetricsRegistry();

        registry.RecordHttpRequest("POST", "/api/governance-intelligence/explain", 200, TimeSpan.FromMilliseconds(125));
        registry.RecordExplainabilityRequest("Groq", "qwen/test", "Applied", true, TimeSpan.FromMilliseconds(600));
        registry.SetOutboxReadyCount(2);
        registry.RecordAnchorOutcome("Anchored");

        var metrics = registry.Render();

        Assert.Contains("state_land_governance_http_requests_total{method=\"POST\",route=\"/api/governance-intelligence/explain\",status=\"200\"} 1", metrics);
        Assert.Contains("state_land_governance_explainability_requests_total{provider=\"Groq\",model=\"qwen/test\",status=\"Applied\",applied=\"true\"} 1", metrics);
        Assert.Contains("state_land_governance_explainability_request_duration_seconds_sum{provider=\"Groq\",model=\"qwen/test\",status=\"Applied\",applied=\"true\"} 0.600000", metrics);
        Assert.Contains("state_land_governance_anchor_outbox_ready 2", metrics);
        Assert.Contains("state_land_governance_anchor_outcomes_total{status=\"Anchored\"} 1", metrics);
        Assert.DoesNotContain('\r', metrics);
    }
}
