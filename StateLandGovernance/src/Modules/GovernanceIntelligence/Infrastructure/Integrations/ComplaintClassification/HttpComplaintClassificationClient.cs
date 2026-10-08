using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.ComplaintClassification;

public sealed class HttpComplaintClassificationClient : IComplaintClassificationClient
{
    public const int MaximumComplaintTextLength = ComplaintClassificationConstraints.MaximumComplaintTextLength;

    // Allows harmless floating-point serialization drift while remaining strict
    // enough to reject materially invalid probability distributions.
    internal const double ProbabilitySumTolerance = 1e-6;

    private static readonly Uri PredictRoute = new("predict", UriKind.Relative);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNameCaseInsensitive = false
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpComplaintClassificationClient> _logger;

    public HttpComplaintClassificationClient(
        HttpClient httpClient,
        ILogger<HttpComplaintClassificationClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ComplaintClassificationResult> ClassifyAsync(
        string complaintText,
        string? caseId = null,
        CancellationToken cancellationToken = default)
    {
        var inputError = ValidateInput(complaintText);
        if (inputError is not null)
        {
            return inputError;
        }

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            var payload = new ComplaintClassifierRequest(complaintText, caseId);
            var json = JsonSerializer.Serialize(payload, JsonOptions);
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
                return ComplaintClassificationResult.Failure(
                    ComplaintClassificationErrorCode.UpstreamValidationRejected,
                    "The complaint classifier rejected the request payload.");
            }

            if (!response.IsSuccessStatusCode)
            {
                LogFailure("non_success", response.StatusCode, startedAt);
                return ComplaintClassificationResult.Failure(
                    ComplaintClassificationErrorCode.UpstreamFailure,
                    "The complaint classifier returned an unsuccessful response.");
            }

            ComplaintClassifierResponse? transportResponse;
            try
            {
                await using var responseStream = await response.Content
                    .ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);
                transportResponse = await JsonSerializer.DeserializeAsync<ComplaintClassifierResponse>(
                    responseStream,
                    JsonOptions,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                _logger.LogWarning(
                    "Complaint classifier returned malformed JSON after {ElapsedMilliseconds} ms.",
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                return InvalidResponse("The complaint classifier returned malformed JSON.");
            }

            var validationError = ValidateResponse(transportResponse, caseId);
            if (validationError is not null)
            {
                _logger.LogWarning(
                    "Complaint classifier returned an incompatible response after {ElapsedMilliseconds} ms: {ValidationCategory}.",
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                    validationError);
                return InvalidResponse("The complaint classifier returned an incompatible response.");
            }

            var probabilities = new ReadOnlyDictionary<string, double>(
                new Dictionary<string, double>(transportResponse!.ClassProbabilities!, StringComparer.Ordinal));
            var prediction = new ComplaintClassificationPrediction(
                transportResponse.ModelVersion!,
                transportResponse.PredictedCategory!,
                probabilities,
                transportResponse.CaseId,
                transportResponse.AdvisoryNote!,
                transportResponse.ClosedSetNote!);

            _logger.LogInformation(
                "Complaint classification completed in {ElapsedMilliseconds} ms using model version {ModelVersion}.",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                prediction.ModelVersion);
            return ComplaintClassificationResult.Success(prediction);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Complaint classifier request timed out after {ElapsedMilliseconds} ms.",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return ComplaintClassificationResult.Failure(
                ComplaintClassificationErrorCode.Timeout,
                "The complaint classifier request timed out.");
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning(
                "Complaint classifier is unavailable after {ElapsedMilliseconds} ms.",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return ComplaintClassificationResult.Failure(
                ComplaintClassificationErrorCode.ServiceUnavailable,
                "The complaint classifier is unavailable.");
        }
    }

    private static ComplaintClassificationResult? ValidateInput(string? complaintText)
    {
        if (string.IsNullOrWhiteSpace(complaintText))
        {
            return ComplaintClassificationResult.Failure(
                ComplaintClassificationErrorCode.InvalidInput,
                "Complaint text must be a nonblank string.");
        }

        // Python len() and Rune enumeration both count Unicode scalar values,
        // avoiding a byte-based limit and UTF-16 surrogate-pair disagreement.
        if (complaintText.EnumerateRunes().Count() > MaximumComplaintTextLength)
        {
            return ComplaintClassificationResult.Failure(
                ComplaintClassificationErrorCode.InvalidInput,
                $"Complaint text exceeds the operational limit of {MaximumComplaintTextLength} characters.");
        }

        return null;
    }

    private static string? ValidateResponse(
        ComplaintClassifierResponse? response,
        string? submittedCaseId)
    {
        if (response is null)
        {
            return "null_response";
        }

        if (string.IsNullOrWhiteSpace(response.ModelVersion))
        {
            return "blank_model_version";
        }

        if (!ComplaintClassificationCategories.Contains(response.PredictedCategory))
        {
            return "unknown_predicted_category";
        }

        var probabilities = response.ClassProbabilities;
        if (probabilities is null || probabilities.Count != ComplaintClassificationCategories.All.Count)
        {
            return "probability_category_count";
        }

        foreach (var category in ComplaintClassificationCategories.All)
        {
            if (!probabilities.TryGetValue(category, out var probability))
            {
                return "missing_probability_category";
            }

            if (!double.IsFinite(probability) || probability is < 0d or > 1d)
            {
                return "invalid_probability_value";
            }
        }

        if (probabilities.Keys.Any(key => !ComplaintClassificationCategories.Contains(key)))
        {
            return "extra_probability_category";
        }

        var probabilitySum = probabilities.Values.Sum();
        if (Math.Abs(probabilitySum - 1d) > ProbabilitySumTolerance)
        {
            return "invalid_probability_sum";
        }

        if (!probabilities.ContainsKey(response.PredictedCategory!))
        {
            return "predicted_category_missing_from_probabilities";
        }

        if (!string.Equals(response.CaseId, submittedCaseId, StringComparison.Ordinal))
        {
            return "case_id_mismatch";
        }

        if (string.IsNullOrWhiteSpace(response.AdvisoryNote))
        {
            return "blank_advisory_note";
        }

        if (string.IsNullOrWhiteSpace(response.ClosedSetNote))
        {
            return "blank_closed_set_note";
        }

        return null;
    }

    private static ComplaintClassificationResult InvalidResponse(string message) =>
        ComplaintClassificationResult.Failure(
            ComplaintClassificationErrorCode.InvalidResponse,
            message);

    private void LogFailure(string category, HttpStatusCode statusCode, long startedAt) =>
        _logger.LogWarning(
            "Complaint classifier request failed after {ElapsedMilliseconds} ms with category {FailureCategory} and upstream status {StatusCode}.",
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            category,
            (int)statusCode);
}
