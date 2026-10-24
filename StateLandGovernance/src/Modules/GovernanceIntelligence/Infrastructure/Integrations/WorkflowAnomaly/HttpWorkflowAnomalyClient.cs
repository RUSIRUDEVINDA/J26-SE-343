using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.WorkflowAnomaly;

public sealed class HttpWorkflowAnomalyClient : IWorkflowAnomalyClient
{
    private static readonly Uri PredictRoute = new("workflow-anomaly/predict", UriKind.Relative);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNameCaseInsensitive = false
    };

    public static readonly ImmutableArray<string> RequiredFeatureKeys = ImmutableArray.Create(
        "event_count",
        "elapsed_days",
        "max_gap_hours",
        "mean_gap_hours",
        "unique_activities",
        "repeated_activity_count",
        "resource_handoffs",
        "institution_switches",
        "distinct_resources");

    private static readonly HashSet<string> RequiredFeatureKeysSet = new(RequiredFeatureKeys, StringComparer.Ordinal);

    private static readonly ImmutableArray<string> CountFeatureKeys = ImmutableArray.Create(
        "event_count",
        "unique_activities",
        "repeated_activity_count",
        "resource_handoffs",
        "institution_switches",
        "distinct_resources");

    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpWorkflowAnomalyClient> _logger;
    private readonly int _maxEvents;

    public HttpWorkflowAnomalyClient(
        HttpClient httpClient,
        ILogger<HttpWorkflowAnomalyClient> logger,
        IOptions<WorkflowAnomalyOptions>? options = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxEvents = options?.Value?.MaxEvents > 0
            ? options.Value.MaxEvents
            : WorkflowAnomalyConstraints.MaximumEvents;
    }

    public async Task<WorkflowAnomalyResult> PredictAnomalyAsync(
        string caseId,
        IReadOnlyList<WorkflowAnomalyEvent> events,
        CancellationToken cancellationToken = default)
    {
        var inputError = ValidateInput(caseId, events, _maxEvents);
        if (inputError is not null)
        {
            return inputError;
        }

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            var wireEvents = new WorkflowEventTransportRequest[events.Count];
            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                wireEvents[i] = new WorkflowEventTransportRequest(
                    evt.EventSeq,
                    evt.Activity,
                    evt.Institution,
                    evt.Resource,
                    evt.Timestamp);
            }

            var wirePayload = new WorkflowAnomalyTransportRequest(caseId, wireEvents);
            var json = JsonSerializer.Serialize(wirePayload, JsonOptions);
            using var request = new HttpRequestMessage(HttpMethod.Post, PredictRoute)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                LogFailure("validation_rejected", response.StatusCode, startedAt);
                return WorkflowAnomalyResult.Failure(
                    WorkflowAnomalyErrorCode.UpstreamValidationRejected,
                    "The workflow anomaly service rejected the request payload.");
            }

            if (!response.IsSuccessStatusCode)
            {
                LogFailure("non_success", response.StatusCode, startedAt);
                return WorkflowAnomalyResult.Failure(
                    WorkflowAnomalyErrorCode.UpstreamFailure,
                    "The workflow anomaly service returned an unsuccessful response.");
            }

            WorkflowAnomalyTransportResponse? transportResponse;
            try
            {
                await using var responseStream = await response.Content
                    .ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);
                transportResponse = await JsonSerializer.DeserializeAsync<WorkflowAnomalyTransportResponse>(
                    responseStream,
                    JsonOptions,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                _logger.LogWarning(
                    "Workflow anomaly service returned malformed JSON after {ElapsedMilliseconds} ms.",
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                return WorkflowAnomalyResult.Failure(
                    WorkflowAnomalyErrorCode.InvalidResponse,
                    "The workflow anomaly service returned malformed JSON.");
            }

            var validationCategory = ValidateResponse(transportResponse, caseId, events.Count);
            if (validationCategory is not null)
            {
                _logger.LogWarning(
                    "Workflow anomaly service returned an incompatible response after {ElapsedMilliseconds} ms: {ValidationCategory}.",
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                    validationCategory);
                return WorkflowAnomalyResult.Failure(
                    WorkflowAnomalyErrorCode.InvalidResponse,
                    "The workflow anomaly service returned an incompatible response.");
            }

            var rawFeatures = transportResponse!.FeatureValues!;
            var featureValues = new WorkflowFeatureValues(
                EventCount: Convert.ToInt32(rawFeatures["event_count"]),
                ElapsedDays: rawFeatures["elapsed_days"],
                MaxGapHours: rawFeatures["max_gap_hours"],
                MeanGapHours: rawFeatures["mean_gap_hours"],
                UniqueActivities: Convert.ToInt32(rawFeatures["unique_activities"]),
                RepeatedActivityCount: Convert.ToInt32(rawFeatures["repeated_activity_count"]),
                ResourceHandoffs: Convert.ToInt32(rawFeatures["resource_handoffs"]),
                InstitutionSwitches: Convert.ToInt32(rawFeatures["institution_switches"]),
                DistinctResources: Convert.ToInt32(rawFeatures["distinct_resources"]));

            var prediction = new WorkflowAnomalyPrediction(
                CaseId: transportResponse.CaseId!,
                ModelVersion: transportResponse.ModelVersion!,
                AnomalyScore: transportResponse.AnomalyScore!.Value,
                Threshold: transportResponse.Threshold!.Value,
                Flagged: transportResponse.Flagged!.Value,
                FeatureValues: featureValues,
                AdvisoryNote: transportResponse.AdvisoryNote!);

            _logger.LogInformation(
                "Workflow anomaly detection completed in {ElapsedMilliseconds} ms using model version {ModelVersion}.",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                prediction.ModelVersion);

            return WorkflowAnomalyResult.Success(prediction);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Workflow anomaly detection request timed out after {ElapsedMilliseconds} ms.",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return WorkflowAnomalyResult.Failure(
                WorkflowAnomalyErrorCode.Timeout,
                "The workflow anomaly detection request timed out.");
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning(
                "Workflow anomaly service is unavailable after {ElapsedMilliseconds} ms.",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return WorkflowAnomalyResult.Failure(
                WorkflowAnomalyErrorCode.ServiceUnavailable,
                "The workflow anomaly service is unavailable.");
        }
    }

    private static WorkflowAnomalyResult? ValidateInput(
        string? caseId,
        IReadOnlyList<WorkflowAnomalyEvent>? events,
        int maxEvents)
    {
        if (string.IsNullOrWhiteSpace(caseId))
        {
            return WorkflowAnomalyResult.Failure(
                WorkflowAnomalyErrorCode.InvalidInput,
                "Case ID must be a nonblank string.");
        }

        if (caseId.Length > WorkflowAnomalyConstraints.MaximumCaseIdLength)
        {
            return WorkflowAnomalyResult.Failure(
                WorkflowAnomalyErrorCode.InvalidInput,
                $"Case ID exceeds the operational limit of {WorkflowAnomalyConstraints.MaximumCaseIdLength} characters.");
        }

        if (events is null || events.Count == 0)
        {
            return WorkflowAnomalyResult.Failure(
                WorkflowAnomalyErrorCode.InvalidInput,
                "Events collection must not be null or empty.");
        }

        if (events.Count > maxEvents)
        {
            return WorkflowAnomalyResult.Failure(
                WorkflowAnomalyErrorCode.InvalidInput,
                $"Events count {events.Count} exceeds operational limit of {maxEvents}.");
        }

        for (int i = 0; i < events.Count; i++)
        {
            var evt = events[i];
            if (evt is null)
            {
                return WorkflowAnomalyResult.Failure(
                    WorkflowAnomalyErrorCode.InvalidInput,
                    $"Event at index {i} must not be null.");
            }

            if (evt.EventSeq <= 0)
            {
                return WorkflowAnomalyResult.Failure(
                    WorkflowAnomalyErrorCode.InvalidInput,
                    $"Event sequence at index {i} must be a positive integer.");
            }

            if (string.IsNullOrWhiteSpace(evt.Activity))
            {
                return WorkflowAnomalyResult.Failure(
                    WorkflowAnomalyErrorCode.InvalidInput,
                    $"Activity at index {i} must be a nonblank string.");
            }

            if (string.IsNullOrWhiteSpace(evt.Institution))
            {
                return WorkflowAnomalyResult.Failure(
                    WorkflowAnomalyErrorCode.InvalidInput,
                    $"Institution at index {i} must be a nonblank string.");
            }

            if (string.IsNullOrWhiteSpace(evt.Resource))
            {
                return WorkflowAnomalyResult.Failure(
                    WorkflowAnomalyErrorCode.InvalidInput,
                    $"Resource at index {i} must be a nonblank string.");
            }

            var timestampError = ValidateNaiveTimestamp(evt.Timestamp, i);
            if (timestampError is not null)
            {
                return WorkflowAnomalyResult.Failure(
                    WorkflowAnomalyErrorCode.InvalidInput,
                    timestampError);
            }
        }

        return null;
    }

    private static string? ValidateNaiveTimestamp(string? timestamp, int index)
    {
        if (string.IsNullOrWhiteSpace(timestamp))
        {
            return $"Timestamp at index {index} must be a nonblank string.";
        }

        // Must not contain 'Z' UTC indicator
        if (timestamp.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
        {
            return $"Timestamp at index {index} must remain naive and must not contain a UTC 'Z' indicator.";
        }

        // Must not contain timezone offset in the time portion (after YYYY-MM-DD)
        if (timestamp.Length > 10 && (timestamp[10..].Contains('+') || timestamp[10..].Contains('-')))
        {
            return $"Timestamp at index {index} must remain naive and must not contain a timezone offset.";
        }

        if (!DateTime.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return $"Timestamp at index {index} is not a valid ISO-8601 date-time string.";
        }

        return null;
    }

    private static string? ValidateResponse(
        WorkflowAnomalyTransportResponse? response,
        string submittedCaseId,
        int submittedEventCount)
    {
        if (response is null)
        {
            return "null_response";
        }

        if (!string.Equals(response.CaseId, submittedCaseId, StringComparison.Ordinal))
        {
            return "case_id_mismatch";
        }

        if (string.IsNullOrWhiteSpace(response.ModelVersion))
        {
            return "blank_model_version";
        }

        if (string.IsNullOrWhiteSpace(response.AdvisoryNote))
        {
            return "blank_advisory_note";
        }

        if (response.AnomalyScore is null || !double.IsFinite(response.AnomalyScore.Value))
        {
            return "non_finite_anomaly_score";
        }

        if (response.Threshold is null || !double.IsFinite(response.Threshold.Value))
        {
            return "non_finite_threshold";
        }

        if (response.Flagged is null)
        {
            return "null_flagged";
        }

        // flagged equals the strict comparison anomaly_score > threshold
        bool expectedFlagged = response.AnomalyScore.Value > response.Threshold.Value;
        if (response.Flagged.Value != expectedFlagged)
        {
            return "inconsistent_flagged";
        }

        var features = response.FeatureValues;
        if (features is null)
        {
            return "null_feature_values";
        }

        if (features.Count != RequiredFeatureKeys.Length)
        {
            return "feature_count_mismatch";
        }

        foreach (var key in RequiredFeatureKeys)
        {
            if (!features.TryGetValue(key, out var value))
            {
                return $"missing_feature_{key}";
            }

            if (!double.IsFinite(value))
            {
                return $"non_finite_feature_{key}";
            }

            if (value < 0d)
            {
                return $"negative_feature_{key}";
            }
        }

        foreach (var key in features.Keys)
        {
            if (!RequiredFeatureKeysSet.Contains(key))
            {
                return $"extra_feature_{key}";
            }
        }

        // Count features must be non-negative integer-valued numbers
        foreach (var countKey in CountFeatureKeys)
        {
            var countVal = features[countKey];
            if (countVal != Math.Floor(countVal) || countVal > int.MaxValue)
            {
                return $"non_integer_count_feature_{countKey}";
            }
        }

        // event_count equals the submitted event count
        var reportedEventCount = Convert.ToInt32(features["event_count"]);
        if (reportedEventCount != submittedEventCount)
        {
            return "event_count_mismatch";
        }

        return null;
    }

    private void LogFailure(string category, HttpStatusCode statusCode, long startedAt) =>
        _logger.LogWarning(
            "Workflow anomaly request failed after {ElapsedMilliseconds} ms with category {FailureCategory} and upstream status {StatusCode}.",
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            category,
            (int)statusCode);
}
