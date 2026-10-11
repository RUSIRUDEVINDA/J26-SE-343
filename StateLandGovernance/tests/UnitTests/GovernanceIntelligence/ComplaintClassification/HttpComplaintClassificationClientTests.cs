using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.ComplaintClassification;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence.ComplaintClassification;

public sealed class HttpComplaintClassificationClientTests
{
    private const string CaseId = "CASE-2042";
    private const string ComplaintText = "The lessee has not paid rent for three months.";

    [Fact]
    public async Task ClassifyAsync_ValidRequest_PostsExactContractAndPreservesInput()
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

        var result = await client.ClassifyAsync(ComplaintText, CaseId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.Equal("http://127.0.0.1:8104/predict", captured.Uri!.AbsoluteUri);

        using var document = JsonDocument.Parse(captured.Body);
        var root = document.RootElement;
        Assert.Equal(2, root.EnumerateObject().Count());
        Assert.Equal(ComplaintText, root.GetProperty("complaint_text").GetString());
        Assert.Equal(CaseId, root.GetProperty("case_id").GetString());
        Assert.False(root.TryGetProperty("complaintText", out _));
        Assert.False(root.TryGetProperty("caseId", out _));
    }

    [Fact]
    public async Task ClassifyAsync_NullCaseId_SerializesExplicitJsonNull()
    {
        string? requestBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse(ValidResponseJson(null));
        });
        var client = CreateClient(handler);

        var result = await client.ClassifyAsync(ComplaintText);

        Assert.True(result.IsSuccess);
        using var document = JsonDocument.Parse(requestBody!);
        Assert.True(document.RootElement.TryGetProperty("case_id", out var caseId));
        Assert.Equal(JsonValueKind.Null, caseId.ValueKind);
    }

    [Fact]
    public async Task ClassifyAsync_ValidShuffledResponse_MapsEveryApplicationField()
    {
        var probabilities = ValidProbabilities();
        var payload = new Dictionary<string, object?>
        {
            ["closed_set_note"] = "Only the configured four categories are considered.",
            ["case_id"] = CaseId,
            ["class_probabilities"] = probabilities,
            ["advisory_note"] = "Advisory output; officer review is required.",
            ["predicted_category"] = ComplaintClassificationCategories.LeaseRevenuePaymentEnforcement,
            ["model_version"] = "v1"
        };
        var handler = HandlerReturning(JsonSerializer.Serialize(payload));
        var client = CreateClient(handler);

        var result = await client.ClassifyAsync(ComplaintText, CaseId);

        Assert.True(result.IsSuccess);
        var prediction = Assert.IsType<ComplaintClassificationPrediction>(result.Prediction);
        Assert.Equal("v1", prediction.ModelVersion);
        Assert.Equal(ComplaintClassificationCategories.LeaseRevenuePaymentEnforcement, prediction.PredictedCategory);
        Assert.Equal(CaseId, prediction.CaseId);
        Assert.Equal("Advisory output; officer review is required.", prediction.AdvisoryNote);
        Assert.Equal("Only the configured four categories are considered.", prediction.ClosedSetNote);
        Assert.Equal(4, prediction.ClassProbabilities.Count);
        Assert.Equal(0.55, prediction.ClassProbabilities[ComplaintClassificationCategories.LeaseRevenuePaymentEnforcement]);
    }

    [Fact]
    public async Task ClassifyAsync_ProbabilitySumWithinTolerance_AcceptsResponse()
    {
        var probabilities = ValidProbabilities();
        probabilities[ComplaintClassificationCategories.AdministrativeProceduralIntegrity] += 0.0000005;
        var client = CreateClient(HandlerReturning(ValidResponseJson(CaseId, probabilities)));

        var result = await client.ClassifyAsync(ComplaintText, CaseId);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Prediction!.ClassProbabilities.Count);
    }

    [Theory]
    [MemberData(nameof(ExactCategories))]
    public async Task ClassifyAsync_EachExactPredictedCategory_AcceptsResponse(string predictedCategory)
    {
        var client = CreateClient(HandlerReturning(
            ValidResponseJson(CaseId, predictedCategory: predictedCategory)));

        var result = await client.ClassifyAsync(ComplaintText, CaseId);

        Assert.True(result.IsSuccess);
        Assert.Equal(predictedCategory, result.Prediction!.PredictedCategory);
    }

    public static TheoryData<string> ExactCategories => new()
    {
        ComplaintClassificationCategories.AdministrativeProceduralIntegrity,
        ComplaintClassificationCategories.LeaseRevenuePaymentEnforcement,
        ComplaintClassificationCategories.UnauthorizedAllocationTransferUse,
        ComplaintClassificationCategories.ProtectedEnvironmentalLeaseMisuse
    };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ClassifyAsync_NullOrBlankComplaint_ReturnsInvalidInputWithoutHttp(string? complaintText)
    {
        var handler = HandlerReturning(ValidResponseJson(null));
        var client = CreateClient(handler);

        var result = await client.ClassifyAsync(complaintText!);

        AssertFailure(result, ComplaintClassificationErrorCode.InvalidInput);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task ClassifyAsync_ComplaintOverScalarLimit_ReturnsInvalidInputWithoutHttp()
    {
        var handler = HandlerReturning(ValidResponseJson(null));
        var client = CreateClient(handler);

        var result = await client.ClassifyAsync(new string('x', 10_001));

        AssertFailure(result, ComplaintClassificationErrorCode.InvalidInput);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task ClassifyAsync_TenThousandNonBmpCharacters_UsesPythonCompatibleScalarLimit()
    {
        var handler = HandlerReturning(ValidResponseJson(null));
        var client = CreateClient(handler);
        var complaint = string.Concat(Enumerable.Repeat("😀", 10_000));

        var result = await client.ClassifyAsync(complaint);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, handler.CallCount);
    }

    public static TheoryData<string> IncompatibleResponseBodies => new()
    {
        { ValidResponseJson(CaseId, modelVersion: " ") },
        { ValidResponseJson(CaseId, predictedCategory: "Unknown") },
        { ValidResponseJson(CaseId, probabilities: ProbabilitiesWithout(ComplaintClassificationCategories.AdministrativeProceduralIntegrity)) },
        { ValidResponseJson(CaseId, probabilities: ProbabilitiesWithExtraCategory()) },
        { ValidResponseJson(CaseId, probabilities: ProbabilitiesWithValue(ComplaintClassificationCategories.LeaseRevenuePaymentEnforcement, 1.1)) },
        { ValidResponseJson(CaseId, probabilities: ProbabilitiesWithValue(ComplaintClassificationCategories.LeaseRevenuePaymentEnforcement, 0.9)) },
        { ValidResponseJson(CaseId, advisoryNote: "") },
        { ValidResponseJson(CaseId, closedSetNote: " ") },
        { ResponseWithoutField("advisory_note") },
        { ResponseWithoutField("closed_set_note") },
        { ValidResponseJson("DIFFERENT-CASE") },
        { "{not-json" },
        { ResponseWithNonFiniteProbability("1e400") },
        { ResponseWithNonFiniteProbability("\"NaN\"") }
    };

    [Theory]
    [MemberData(nameof(IncompatibleResponseBodies))]
    public async Task ClassifyAsync_IncompatibleResponse_ReturnsInvalidResponse(string responseBody)
    {
        var client = CreateClient(HandlerReturning(responseBody));

        var result = await client.ClassifyAsync(ComplaintText, CaseId);

        AssertFailure(result, ComplaintClassificationErrorCode.InvalidResponse);
    }

    [Fact]
    public async Task ClassifyAsync_Http422_ReturnsUpstreamValidationRejected()
    {
        var client = CreateClient(HandlerReturning("sensitive upstream body", HttpStatusCode.UnprocessableEntity));

        var result = await client.ClassifyAsync(ComplaintText, CaseId);

        AssertFailure(result, ComplaintClassificationErrorCode.UpstreamValidationRejected);
        Assert.DoesNotContain("sensitive upstream body", result.Error!.Message);
    }

    [Fact]
    public async Task ClassifyAsync_Http500_ReturnsUpstreamFailure()
    {
        var client = CreateClient(HandlerReturning("sensitive upstream body", HttpStatusCode.InternalServerError));

        var result = await client.ClassifyAsync(ComplaintText, CaseId);

        AssertFailure(result, ComplaintClassificationErrorCode.UpstreamFailure);
        Assert.DoesNotContain("sensitive upstream body", result.Error!.Message);
    }

    [Fact]
    public async Task ClassifyAsync_ConnectionFailure_ReturnsServiceUnavailable()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException("Local path and service details that must not escape."));
        var client = CreateClient(handler);

        var result = await client.ClassifyAsync(ComplaintText, CaseId);

        AssertFailure(result, ComplaintClassificationErrorCode.ServiceUnavailable);
        Assert.DoesNotContain("Local path", result.Error!.Message);
    }

    [Fact]
    public async Task ClassifyAsync_HttpClientTimeout_ReturnsTimeout()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        });
        var client = CreateClient(handler, TimeSpan.FromMilliseconds(20));

        var result = await client.ClassifyAsync(ComplaintText, CaseId);

        AssertFailure(result, ComplaintClassificationErrorCode.Timeout);
    }

    [Fact]
    public async Task ClassifyAsync_CallerCancellation_RemainsCancellationAndReachesHandler()
    {
        var handlerObservedCancellation = false;
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                handlerObservedCancellation = cancellationToken.IsCancellationRequested;
                throw;
            }

            throw new InvalidOperationException("Unreachable.");
        });
        var client = CreateClient(handler, TimeSpan.FromSeconds(10));
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.CancelAfter(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ClassifyAsync(ComplaintText, CaseId, cancellationSource.Token));

        Assert.True(handlerObservedCancellation);
    }

    private static HttpComplaintClassificationClient CreateClient(
        HttpMessageHandler handler,
        TimeSpan? timeout = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:8104/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(5)
        };

        return new HttpComplaintClassificationClient(
            httpClient,
            NullLogger<HttpComplaintClassificationClient>.Instance);
    }

    private static StubHttpMessageHandler HandlerReturning(
        string body,
        HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(JsonResponse(body, statusCode)));

    private static HttpResponseMessage JsonResponse(
        string body,
        HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    private static string ValidResponseJson(
        string? caseId,
        Dictionary<string, double>? probabilities = null,
        string modelVersion = "v1",
        string predictedCategory = ComplaintClassificationCategories.LeaseRevenuePaymentEnforcement,
        string advisoryNote = "Advisory output; officer review is required.",
        string closedSetNote = "Only the configured four categories are considered.")
    {
        var payload = new Dictionary<string, object?>
        {
            ["model_version"] = modelVersion,
            ["predicted_category"] = predictedCategory,
            ["class_probabilities"] = probabilities ?? ValidProbabilities(),
            ["case_id"] = caseId,
            ["advisory_note"] = advisoryNote,
            ["closed_set_note"] = closedSetNote
        };

        return JsonSerializer.Serialize(payload);
    }

    private static Dictionary<string, double> ValidProbabilities() => new(StringComparer.Ordinal)
    {
        [ComplaintClassificationCategories.AdministrativeProceduralIntegrity] = 0.15,
        [ComplaintClassificationCategories.LeaseRevenuePaymentEnforcement] = 0.55,
        [ComplaintClassificationCategories.UnauthorizedAllocationTransferUse] = 0.20,
        [ComplaintClassificationCategories.ProtectedEnvironmentalLeaseMisuse] = 0.10
    };

    private static Dictionary<string, double> ProbabilitiesWithout(string category)
    {
        var probabilities = ValidProbabilities();
        probabilities.Remove(category);
        return probabilities;
    }

    private static Dictionary<string, double> ProbabilitiesWithExtraCategory()
    {
        var probabilities = ValidProbabilities();
        probabilities["Other"] = 0;
        return probabilities;
    }

    private static Dictionary<string, double> ProbabilitiesWithValue(string category, double value)
    {
        var probabilities = ValidProbabilities();
        probabilities[category] = value;
        return probabilities;
    }

    private static string ResponseWithNonFiniteProbability(string rawValue)
    {
        var finiteEntries = string.Join(",", new[]
        {
            $"{JsonSerializer.Serialize(ComplaintClassificationCategories.AdministrativeProceduralIntegrity)}:0.15",
            $"{JsonSerializer.Serialize(ComplaintClassificationCategories.LeaseRevenuePaymentEnforcement)}:{rawValue}",
            $"{JsonSerializer.Serialize(ComplaintClassificationCategories.UnauthorizedAllocationTransferUse)}:0.2",
            $"{JsonSerializer.Serialize(ComplaintClassificationCategories.ProtectedEnvironmentalLeaseMisuse)}:0.1"
        });

        return $$"""
            {
              "model_version": "v1",
              "predicted_category": "{{ComplaintClassificationCategories.LeaseRevenuePaymentEnforcement}}",
              "class_probabilities": { {{finiteEntries}} },
              "case_id": "{{CaseId}}",
              "advisory_note": "Advisory output; officer review is required.",
              "closed_set_note": "Only the configured four categories are considered."
            }
            """;
    }

    private static string ResponseWithoutField(string fieldName)
    {
        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            ValidResponseJson(CaseId))!;
        payload.Remove(fieldName);
        return JsonSerializer.Serialize(payload);
    }

    private static void AssertFailure(
        ComplaintClassificationResult result,
        ComplaintClassificationErrorCode expectedCode)
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
