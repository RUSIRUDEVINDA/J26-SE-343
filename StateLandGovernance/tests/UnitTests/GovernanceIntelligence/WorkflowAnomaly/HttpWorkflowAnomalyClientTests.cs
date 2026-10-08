using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.WorkflowAnomaly;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence.WorkflowAnomaly;

public sealed class HttpWorkflowAnomalyClientTests
{
    private const string CaseId = "CASE-DEMO-001";
    private static readonly Uri DefaultBaseUri = new("http://127.0.0.1:8104/");

    private static IReadOnlyList<WorkflowAnomalyEvent> ValidEvents => new List<WorkflowAnomalyEvent>
    {
        new(1, "DS Application Received", "DS", "Demo Officer 1", "2031-02-03T08:00:00"),
        new(2, "DS Initial Review", "DS", "Demo Officer 1", "2031-02-03T12:00:00"),
        new(3, "PLC/DLC Review", "PLC/DLC", "Demo Reviewer 1", "2031-02-04T10:00:00"),
        new(4, "DS Final Notification", "DS", "Demo Officer 2", "2031-02-05T09:00:00")
    };

    [Fact]
    public async Task PredictAnomalyAsync_ValidRequest_PostsExactContractAndPreservesInput()
    {
        CapturedRequest? captured = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            captured = new CapturedRequest(
                request.Method,
                request.RequestUri,
                await request.Content!.ReadAsStringAsync(cancellationToken));
            return JsonResponse(ValidResponseJson(CaseId));
        });
        var client = CreateClient(handler);

        var result = await client.PredictAnomalyAsync(CaseId, ValidEvents);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.Equal("http://127.0.0.1:8104/workflow-anomaly/predict", captured.Uri!.AbsoluteUri);

        using var document = JsonDocument.Parse(captured.Body);
        var root = document.RootElement;
        Assert.Equal(2, root.EnumerateObject().Count());
        Assert.Equal(CaseId, root.GetProperty("case_id").GetString());
        Assert.False(root.TryGetProperty("caseId", out _));

        var eventsElement = root.GetProperty("events");
        Assert.Equal(JsonValueKind.Array, eventsElement.ValueKind);
        Assert.Equal(4, eventsElement.GetArrayLength());

        var firstEvent = eventsElement[0];
        Assert.Equal(1, firstEvent.GetProperty("event_seq").GetInt32());
        Assert.Equal("DS Application Received", firstEvent.GetProperty("activity").GetString());
        Assert.Equal("DS", firstEvent.GetProperty("institution").GetString());
        Assert.Equal("Demo Officer 1", firstEvent.GetProperty("resource").GetString());
        Assert.Equal("2031-02-03T08:00:00", firstEvent.GetProperty("timestamp").GetString());
        Assert.False(firstEvent.TryGetProperty("eventSeq", out _));
    }

    [Fact]
    public async Task PredictAnomalyAsync_ValidResponse_MapsEveryApplicationField()
    {
        var client = CreateClient(HandlerReturning(ValidResponseJson(CaseId)));

        var result = await client.PredictAnomalyAsync(CaseId, ValidEvents);

        Assert.True(result.IsSuccess);
        var prediction = Assert.IsType<WorkflowAnomalyPrediction>(result.Prediction);
        Assert.Equal(CaseId, prediction.CaseId);
        Assert.Equal("workflow_anomaly_model_v1", prediction.ModelVersion);
        Assert.Equal(0.688338, prediction.AnomalyScore, 5);
        Assert.Equal(0.574369, prediction.Threshold, 5);
        Assert.True(prediction.Flagged);
        Assert.Equal("Advisory output only.", prediction.AdvisoryNote);

        var features = prediction.FeatureValues;
        Assert.Equal(4, features.EventCount);
        Assert.Equal(2.041667, features.ElapsedDays, 5);
        Assert.Equal(23.0, features.MaxGapHours);
        Assert.Equal(16.333333, features.MeanGapHours, 5);
        Assert.Equal(4, features.UniqueActivities);
        Assert.Equal(0, features.RepeatedActivityCount);
        Assert.Equal(2, features.ResourceHandoffs);
        Assert.Equal(2, features.InstitutionSwitches);
        Assert.Equal(3, features.DistinctResources);
    }

    [Fact]
    public async Task PredictAnomalyAsync_StrictThresholdEquality_ScoreEqualsThreshold_FlaggedFalse_Accepted()
    {
        // When score == threshold, score > threshold is FALSE, so flagged must be FALSE
        var json = ValidResponseJson(CaseId, score: 0.574, threshold: 0.574, flagged: false);
        var client = CreateClient(HandlerReturning(json));

        var result = await client.PredictAnomalyAsync(CaseId, ValidEvents);

        Assert.True(result.IsSuccess);
        Assert.False(result.Prediction!.Flagged);
    }

    [Fact]
    public async Task PredictAnomalyAsync_StrictThresholdEquality_ScoreEqualsThreshold_FlaggedTrue_Rejected()
    {
        // When score == threshold, score is NOT strictly greater than threshold, so flagged: true is inconsistent
        var json = ValidResponseJson(CaseId, score: 0.574, threshold: 0.574, flagged: true);
        var client = CreateClient(HandlerReturning(json));

        var result = await client.PredictAnomalyAsync(CaseId, ValidEvents);

        AssertFailure(result, WorkflowAnomalyErrorCode.InvalidResponse);
    }

    [Fact]
    public async Task PredictAnomalyAsync_InconsistentFlag_ScoreGreaterThanThreshold_FlaggedFalse_Rejected()
    {
        var json = ValidResponseJson(CaseId, score: 0.75, threshold: 0.50, flagged: false);
        var client = CreateClient(HandlerReturning(json));

        var result = await client.PredictAnomalyAsync(CaseId, ValidEvents);

        AssertFailure(result, WorkflowAnomalyErrorCode.InvalidResponse);
    }

    [Fact]
    public async Task PredictAnomalyAsync_InconsistentFlag_ScoreLessThanThreshold_FlaggedTrue_Rejected()
    {
        var json = ValidResponseJson(CaseId, score: 0.35, threshold: 0.50, flagged: true);
        var client = CreateClient(HandlerReturning(json));

        var result = await client.PredictAnomalyAsync(CaseId, ValidEvents);

        AssertFailure(result, WorkflowAnomalyErrorCode.InvalidResponse);
    }

    [Fact]
    public async Task PredictAnomalyAsync_CaseIdMismatch_ReturnsInvalidResponse()
    {
        var json = ValidResponseJson("DIFFERENT-CASE-ID");
        var client = CreateClient(HandlerReturning(json));

        var result = await client.PredictAnomalyAsync(CaseId, ValidEvents);

        AssertFailure(result, WorkflowAnomalyErrorCode.InvalidResponse);
    }

    [Fact]
    public async Task PredictAnomalyAsync_MissingFeatureKey_ReturnsInvalidResponse()
    {
        var features = ValidFeatureDictionary();
        features.Remove("distinct_resources");
        var json = ValidResponseJson(CaseId, customFeatures: features);
        var client = CreateClient(HandlerReturning(json));

        var result = await client.PredictAnomalyAsync(CaseId, ValidEvents);

        AssertFailure(result, WorkflowAnomalyErrorCode.InvalidResponse);
    }

    [Fact]
    public async Task PredictAnomalyAsync_ExtraFeatureKey_ReturnsInvalidResponse()
    {
        var features = ValidFeatureDictionary();
        features["extra_unsupported_key"] = 1.0;
        var json = ValidResponseJson(CaseId, customFeatures: features);
        var client = CreateClient(HandlerReturning(json));

        var result = await client.PredictAnomalyAsync(CaseId, ValidEvents);

        AssertFailure(result, WorkflowAnomalyErrorCode.InvalidResponse);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public async Task PredictAnomalyAsync_NonFiniteScoreOrThreshold_ReturnsInvalidResponse(string nonFiniteValue)
    {
        var rawJson = $$"""
            {
              "case_id": "{{CaseId}}",
              "model_version": "v1",
              "anomaly_score": {{nonFiniteValue}},
              "threshold": 0.5,
              "flagged": false,
              "feature_values": {{JsonSerializer.Serialize(ValidFeatureDictionary())}},
              "advisory_note": "Advisory note"
            }
            """;
        var client = CreateClient(HandlerReturning(rawJson));

        var result = await client.PredictAnomalyAsync(CaseId, ValidEvents);

        AssertFailure(result, WorkflowAnomalyErrorCode.InvalidResponse);
    }

    [Fact]
    public async Task PredictAnomalyAsync_NegativeFeatureValue_ReturnsInvalidResponse()
    {
        var features = ValidFeatureDictionary();
        features["elapsed_days"] = -1.0;
        var json = ValidResponseJson(CaseId, customFeatures: features);
        var client = CreateClient(HandlerReturning(json));

        var result = await client.PredictAnomalyAsync(CaseId, ValidEvents);

        AssertFailure(result, WorkflowAnomalyErrorCode.InvalidResponse);
    }

    [Fact]
    public async Task PredictAnomalyAsync_NonIntegerCountFeature_ReturnsInvalidResponse()
    {
        var features = ValidFeatureDictionary();
        features["unique_activities"] = 3.5; // Counts must be integer-valued
        var json = ValidResponseJson(CaseId, customFeatures: features);
        var client = CreateClient(HandlerReturning(json));

        var result = await client.PredictAnomalyAsync(CaseId, ValidEvents);

        AssertFailure(result, WorkflowAnomalyErrorCode.InvalidResponse);
    }

    [Fact]
    public async Task PredictAnomalyAsync_EventCountMismatchWithSubmittedEvents_ReturnsInvalidResponse()
    {
        var features = ValidFeatureDictionary();
        features["event_count"] = 5.0; // Submitted 4 events
        var json = ValidResponseJson(CaseId, customFeatures: features);
        var client = CreateClient(HandlerReturning(json));

        var result = await client.PredictAnomalyAsync(CaseId, ValidEvents);

        AssertFailure(result, WorkflowAnomalyErrorCode.InvalidResponse);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PredictAnomalyAsync_NullOrBlankCaseId_ReturnsInvalidInputWithoutHttp(string? caseId)
    {
        var handler = HandlerReturning(ValidResponseJson(CaseId));
        var client = CreateClient(handler);

        var result = await client.PredictAnomalyAsync(caseId!, ValidEvents);

        AssertFailure(result, WorkflowAnomalyErrorCode.InvalidInput);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task PredictAnomalyAsync_EmptyEvents_ReturnsInvalidInputWithoutHttp()
    {
        var handler = HandlerReturning(ValidResponseJson(CaseId));
        var client = CreateClient(handler);

        var result = await client.PredictAnomalyAsync(CaseId, Array.Empty<WorkflowAnomalyEvent>());

        AssertFailure(result, WorkflowAnomalyErrorCode.InvalidInput);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task PredictAnomalyAsync_EventExceedsOperationalLimit_ReturnsInvalidInputWithoutHttp()
    {
        var handler = HandlerReturning(ValidResponseJson(CaseId));
        var client = CreateClient(handler);
        var oversizedEvents = Enumerable.Range(1, 501)
            .Select(i => new WorkflowAnomalyEvent(i, "Activity", "Inst", "Res", "2031-02-03T08:00:00"))
            .ToList();

        var result = await client.PredictAnomalyAsync(CaseId, oversizedEvents);

        AssertFailure(result, WorkflowAnomalyErrorCode.InvalidInput);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task PredictAnomalyAsync_EventSequenceNonPositive_ReturnsInvalidInputWithoutHttp()
    {
        var handler = HandlerReturning(ValidResponseJson(CaseId));
        var client = CreateClient(handler);
        var invalidSeqEvents = new List<WorkflowAnomalyEvent>
        {
            new(0, "Activity", "Inst", "Res", "2031-02-03T08:00:00"),
            new(2, "Activity", "Inst", "Res", "2031-02-03T09:00:00")
        };

        var result = await client.PredictAnomalyAsync(CaseId, invalidSeqEvents);

        AssertFailure(result, WorkflowAnomalyErrorCode.InvalidInput);
        Assert.Equal(0, handler.CallCount);
    }

    [Theory]
    [InlineData("2031-02-03T08:00:00Z")]
    [InlineData("2031-02-03T08:00:00+05:30")]
    [InlineData("2031-02-03T08:00:00-04:00")]
    [InlineData("2031-02-03T08:00:00+00:00")]
    [InlineData("not-a-valid-date")]
    public async Task PredictAnomalyAsync_OffsetBearingOrInvalidTimestamp_ReturnsInvalidInputWithoutHttp(string timestamp)
    {
        var handler = HandlerReturning(ValidResponseJson(CaseId));
        var client = CreateClient(handler);
        var eventsWithBadTimestamp = new List<WorkflowAnomalyEvent>
        {
            new(1, "Activity", "Inst", "Res", timestamp),
            new(2, "DS Final Notification", "Inst", "Res", "2031-02-03T09:00:00")
        };

        var result = await client.PredictAnomalyAsync(CaseId, eventsWithBadTimestamp);

        AssertFailure(result, WorkflowAnomalyErrorCode.InvalidInput);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task PredictAnomalyAsync_UpstreamValidationError_ReturnsUpstreamValidationRejected()
    {
        var handler = HandlerReturningStatusCode(HttpStatusCode.UnprocessableEntity, "{\"detail\":\"Invalid payload\"}");
        var client = CreateClient(handler);

        var result = await client.PredictAnomalyAsync(CaseId, ValidEvents);

        AssertFailure(result, WorkflowAnomalyErrorCode.UpstreamValidationRejected);
    }

    [Fact]
    public async Task PredictAnomalyAsync_Upstream500_ReturnsUpstreamFailure()
    {
        var handler = HandlerReturningStatusCode(HttpStatusCode.InternalServerError, "Internal Server Error");
        var client = CreateClient(handler);

        var result = await client.PredictAnomalyAsync(CaseId, ValidEvents);

        AssertFailure(result, WorkflowAnomalyErrorCode.UpstreamFailure);
    }

    [Fact]
    public async Task PredictAnomalyAsync_MalformedJson_ReturnsInvalidResponse()
    {
        var handler = HandlerReturning("{ not json }");
        var client = CreateClient(handler);

        var result = await client.PredictAnomalyAsync(CaseId, ValidEvents);

        AssertFailure(result, WorkflowAnomalyErrorCode.InvalidResponse);
    }

    [Fact]
    public async Task PredictAnomalyAsync_NetworkException_ReturnsServiceUnavailable()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("Connection refused"));
        var client = CreateClient(handler);

        var result = await client.PredictAnomalyAsync(CaseId, ValidEvents);

        AssertFailure(result, WorkflowAnomalyErrorCode.ServiceUnavailable);
    }

    [Fact]
    public async Task PredictAnomalyAsync_Timeout_ReturnsTimeout()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new OperationCanceledException());
        var client = CreateClient(handler);

        var result = await client.PredictAnomalyAsync(CaseId, ValidEvents);

        AssertFailure(result, WorkflowAnomalyErrorCode.Timeout);
    }

    [Fact]
    public async Task PredictAnomalyAsync_CallerCancellation_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = new StubHttpMessageHandler((_, _) => throw new OperationCanceledException(cts.Token));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            client.PredictAnomalyAsync(CaseId, ValidEvents, cts.Token));
    }

    private static HttpWorkflowAnomalyClient CreateClient(
        HttpMessageHandler handler,
        Uri? baseAddress = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = baseAddress ?? DefaultBaseUri
        };
        return new HttpWorkflowAnomalyClient(httpClient, NullLogger<HttpWorkflowAnomalyClient>.Instance);
    }

    private static StubHttpMessageHandler HandlerReturning(string json) =>
        new((_, _) => Task.FromResult(JsonResponse(json)));

    private static StubHttpMessageHandler HandlerReturningStatusCode(HttpStatusCode code, string content) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(code)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        }));

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static Dictionary<string, double> ValidFeatureDictionary() => new()
    {
        ["event_count"] = 4.0,
        ["elapsed_days"] = 2.0416666666666665,
        ["max_gap_hours"] = 23.0,
        ["mean_gap_hours"] = 16.333333333333332,
        ["unique_activities"] = 4.0,
        ["repeated_activity_count"] = 0.0,
        ["resource_handoffs"] = 2.0,
        ["institution_switches"] = 2.0,
        ["distinct_resources"] = 3.0
    };

    private static string ValidResponseJson(
        string? caseId,
        double score = 0.688338,
        double threshold = 0.574369,
        bool flagged = true,
        Dictionary<string, double>? customFeatures = null)
    {
        var features = customFeatures ?? ValidFeatureDictionary();
        var payload = new Dictionary<string, object?>
        {
            ["case_id"] = caseId,
            ["model_version"] = "workflow_anomaly_model_v1",
            ["anomaly_score"] = score,
            ["threshold"] = threshold,
            ["flagged"] = flagged,
            ["feature_values"] = features,
            ["advisory_note"] = "Advisory output only."
        };
        return JsonSerializer.Serialize(payload);
    }

    private static void AssertFailure(
        WorkflowAnomalyResult result,
        WorkflowAnomalyErrorCode expectedCode)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Prediction);
        Assert.NotNull(result.Error);
        Assert.Equal(expectedCode, result.Error.Code);
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri? Uri, string Body);

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responseFactory;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return _responseFactory(request, cancellationToken);
        }
    }
}
