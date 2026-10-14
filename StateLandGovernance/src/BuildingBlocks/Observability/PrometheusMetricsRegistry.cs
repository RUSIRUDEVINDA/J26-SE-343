using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace StateLandGovernance.BuildingBlocks.Observability;

/// <summary>
/// Small dependency-free metrics registry for the local research demonstrator.
/// Labels are deliberately bounded and must never contain case identifiers or payload data.
/// </summary>
public sealed class PrometheusMetricsRegistry
{
    private readonly ConcurrentDictionary<HttpMetricKey, RequestMeasurement> _http = new();
    private readonly ConcurrentDictionary<ExplainabilityMetricKey, RequestMeasurement> _explainability = new();
    private readonly ConcurrentDictionary<string, long> _anchorOutcomes = new(StringComparer.Ordinal);
    private long _outboxReady;

    public void RecordHttpRequest(string method, string route, int statusCode, TimeSpan elapsed)
    {
        var key = new HttpMetricKey(
            Normalize(method, "UNKNOWN"),
            NormalizeRoute(route),
            statusCode.ToString(CultureInfo.InvariantCulture));
        _http.GetOrAdd(key, static _ => new RequestMeasurement()).Observe(elapsed);
    }

    public void RecordExplainabilityRequest(
        string provider,
        string model,
        string status,
        bool applied,
        TimeSpan elapsed)
    {
        var key = new ExplainabilityMetricKey(
            Normalize(provider, "NotConfigured"),
            Normalize(model, "NotConfigured"),
            Normalize(status, "Unknown"),
            applied ? "true" : "false");
        _explainability.GetOrAdd(key, static _ => new RequestMeasurement()).Observe(elapsed);
    }

    public void SetOutboxReadyCount(int count) =>
        Interlocked.Exchange(ref _outboxReady, Math.Max(0, count));

    public void RecordAnchorOutcome(string status) =>
        _anchorOutcomes.AddOrUpdate(Normalize(status, "Unknown"), 1, static (_, current) => current + 1);

    public string Render()
    {
        var output = new StringBuilder(4096);
        output.AppendLine("# HELP state_land_governance_api_info Static information about the running API.");
        output.AppendLine("# TYPE state_land_governance_api_info gauge");
        output.AppendLine("state_land_governance_api_info{service=\"aspnet-api\",data_mode=\"live\"} 1");

        output.AppendLine("# HELP state_land_governance_http_requests_total HTTP requests processed by the ASP.NET API.");
        output.AppendLine("# TYPE state_land_governance_http_requests_total counter");
        output.AppendLine("# HELP state_land_governance_http_request_duration_seconds HTTP request duration in seconds.");
        output.AppendLine("# TYPE state_land_governance_http_request_duration_seconds summary");
        foreach (var pair in _http.OrderBy(item => item.Key.Route, StringComparer.Ordinal))
        {
            var labels = $"method=\"{Escape(pair.Key.Method)}\",route=\"{Escape(pair.Key.Route)}\",status=\"{Escape(pair.Key.Status)}\"";
            AppendMeasurement(output, "state_land_governance_http_request", labels, pair.Value);
        }

        output.AppendLine("# HELP state_land_governance_explainability_requests_total Plain-language enhancement attempts by bounded outcome.");
        output.AppendLine("# TYPE state_land_governance_explainability_requests_total counter");
        output.AppendLine("# HELP state_land_governance_explainability_request_duration_seconds Hosted explainability request duration in seconds.");
        output.AppendLine("# TYPE state_land_governance_explainability_request_duration_seconds summary");
        foreach (var pair in _explainability.OrderBy(item => item.Key.Status, StringComparer.Ordinal))
        {
            var labels = $"provider=\"{Escape(pair.Key.Provider)}\",model=\"{Escape(pair.Key.Model)}\",status=\"{Escape(pair.Key.Status)}\",applied=\"{pair.Key.Applied}\"";
            AppendMeasurement(output, "state_land_governance_explainability_request", labels, pair.Value);
        }

        output.AppendLine("# HELP state_land_governance_anchor_outbox_ready Number of anchor outbox records found ready in the latest polling batch.");
        output.AppendLine("# TYPE state_land_governance_anchor_outbox_ready gauge");
        output.Append("state_land_governance_anchor_outbox_ready ")
            .AppendLine(Interlocked.Read(ref _outboxReady).ToString(CultureInfo.InvariantCulture));
        output.AppendLine("# HELP state_land_governance_anchor_outcomes_total Blockchain anchor dispatch outcomes.");
        output.AppendLine("# TYPE state_land_governance_anchor_outcomes_total counter");
        foreach (var pair in _anchorOutcomes.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            output.Append("state_land_governance_anchor_outcomes_total{status=\"")
                .Append(Escape(pair.Key)).Append("\"} ")
                .AppendLine(pair.Value.ToString(CultureInfo.InvariantCulture));
        }

        // Prometheus 3 parses metadata strictly and treats a Windows CR as part of
        // the metric type. Always emit the canonical LF-delimited text format.
        return output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static void AppendMeasurement(
        StringBuilder output,
        string prefix,
        string labels,
        RequestMeasurement measurement)
    {
        var snapshot = measurement.Snapshot();
        output.Append(prefix).Append("s_total{").Append(labels).Append("} ")
            .AppendLine(snapshot.Count.ToString(CultureInfo.InvariantCulture));
        output.Append(prefix).Append("_duration_seconds_sum{").Append(labels).Append("} ")
            .AppendLine(snapshot.TotalSeconds.ToString("0.000000", CultureInfo.InvariantCulture));
        output.Append(prefix).Append("_duration_seconds_count{").Append(labels).Append("} ")
            .AppendLine(snapshot.Count.ToString(CultureInfo.InvariantCulture));
    }

    private static string Normalize(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string NormalizeRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route)) return "unmatched";
        return route.Length <= 160 ? route : route[..160];
    }

    private static string Escape(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal);

    private sealed class RequestMeasurement
    {
        private long _count;
        private long _elapsedTicks;

        public void Observe(TimeSpan elapsed)
        {
            Interlocked.Increment(ref _count);
            Interlocked.Add(ref _elapsedTicks, Math.Max(0, elapsed.Ticks));
        }

        public (long Count, double TotalSeconds) Snapshot() =>
            (Interlocked.Read(ref _count), TimeSpan.FromTicks(Interlocked.Read(ref _elapsedTicks)).TotalSeconds);
    }

    private sealed record HttpMetricKey(string Method, string Route, string Status);
    private sealed record ExplainabilityMetricKey(string Provider, string Model, string Status, string Applied);
}
