using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.BuildingBlocks.Observability;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.Explainability;

/// <summary>
/// Optional, fail-closed plain-language adapter for Groq's OpenAI-compatible chat endpoint.
/// It never receives case identifiers or free-form evidence and cannot alter governance outcomes.
/// </summary>
public sealed class GroqGovernanceExplanationSimplifier : IGovernanceExplanationSimplifier
{
    public const string PromptVersion = "groq-governance-plain-language-v1";
    private static readonly Uri CompletionRoute = new("chat/completions", UriKind.Relative);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly GroqExplainabilityOptions _options;
    private readonly ILogger<GroqGovernanceExplanationSimplifier> _logger;
    private readonly PrometheusMetricsRegistry? _metrics;

    public GroqGovernanceExplanationSimplifier(
        HttpClient httpClient,
        IOptions<GroqExplainabilityOptions> options,
        ILogger<GroqGovernanceExplanationSimplifier> logger,
        PrometheusMetricsRegistry? metrics = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics;
    }

    public async Task<GovernanceExplanationSimplificationResult> SimplifyAsync(
        GovernanceExplanationSimplificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var startedAt = Stopwatch.GetTimestamp();
        if (!_options.Enabled)
            return Observe(GovernanceExplanationSimplificationResult.NotApplied("Disabled"), startedAt);
        if (request.Items.Count == 0)
            return Observe(HostedFallback("NoItems"), startedAt);

        var minimizedItems = request.Items.Select(item => new SafeSimplificationItem(
            item.ItemId,
            item.OutcomeStatus,
            item.Severity,
            item.ReasonCode)).ToList();
        var inputJson = JsonSerializer.Serialize(minimizedItems, JsonOptions);
        if (inputJson.Length > _options.MaxInputCharacters)
            return Observe(HostedFallback("InputLimitExceeded"), startedAt);

        var payload = new ChatCompletionRequest(
            _options.Model,
            new[]
            {
                new ChatMessage("system", "Rewrite each supplied governance finding in plain, neutral English for a non-technical government officer. Use only supplied facts. Do not add legal conclusions, accusations, scores, evidence, rules, or actions. Preserve every itemId. Return only JSON in this shape: {\"explanations\":[{\"itemId\":\"...\",\"plainLanguageExplanation\":\"...\"}]}"),
                new ChatMessage("user", inputJson)
            },
            Temperature: 0,
            MaxTokens: 1400);

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, CompletionRoute)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Groq explanation returned HTTP {StatusCode} after {ElapsedMilliseconds} ms.", (int)response.StatusCode, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                return Observe(HostedFallback("UpstreamFailure"), startedAt);
            }

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (responseText.Length > _options.MaxOutputCharacters)
                return Observe(HostedFallback("OutputLimitExceeded"), startedAt);

            var transport = JsonSerializer.Deserialize<ChatCompletionResponse>(responseText, JsonOptions);
            var content = transport?.Choices?.FirstOrDefault()?.Message?.Content;
            if (!TryParseExplanations(content, request.Items, out var explanations))
                return Observe(HostedFallback("InvalidResponse"), startedAt);

            var canonicalOutput = string.Join("|", explanations.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}:{pair.Value}"));
            var outputHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalOutput))).ToLowerInvariant();
            _logger.LogInformation("Groq plain-language explanation completed in {ElapsedMilliseconds} ms using configured model {Model}.", Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, _options.Model);
            return Observe(new GovernanceExplanationSimplificationResult(true, "Groq", _options.Model, PromptVersion, "Applied", outputHash, explanations), startedAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Groq explanation timed out after {ElapsedMilliseconds} ms.", Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return Observe(HostedFallback("Timeout"), startedAt);
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning("Groq explanation service was unavailable after {ElapsedMilliseconds} ms.", Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return Observe(HostedFallback("Unavailable"), startedAt);
        }
        catch (JsonException)
        {
            return Observe(HostedFallback("InvalidResponse"), startedAt);
        }
    }

    private GovernanceExplanationSimplificationResult Observe(
        GovernanceExplanationSimplificationResult result,
        long startedAt)
    {
        _metrics?.RecordExplainabilityRequest(
            result.Provider,
            result.Model,
            result.Status,
            result.Applied,
            Stopwatch.GetElapsedTime(startedAt));
        return result;
    }

    private GovernanceExplanationSimplificationResult HostedFallback(string status) =>
        GovernanceExplanationSimplificationResult.NotApplied(status, "Groq", _options.Model, PromptVersion);

    private static bool TryParseExplanations(
        string? content,
        IReadOnlyList<GovernanceExplanationSimplificationItem> requestedItems,
        out IReadOnlyDictionary<string, string> explanations)
    {
        explanations = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(content)) return false;
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline < 0 || lastFence <= firstNewline) return false;
            trimmed = trimmed[(firstNewline + 1)..lastFence].Trim();
        }

        var parsed = JsonSerializer.Deserialize<SimplificationEnvelope>(trimmed, JsonOptions);
        if (parsed?.Explanations is null || parsed.Explanations.Count != requestedItems.Count) return false;
        var expectedIds = requestedItems.Select(item => item.ItemId).ToHashSet(StringComparer.Ordinal);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in parsed.Explanations)
        {
            var text = item.PlainLanguageExplanation?.Trim();
            if (!expectedIds.Contains(item.ItemId ?? string.Empty) || string.IsNullOrWhiteSpace(text) || text.Length > 1500 || !result.TryAdd(item.ItemId!, text))
                return false;
        }

        if (result.Count != expectedIds.Count) return false;
        explanations = result;
        return true;
    }

    private sealed record ChatMessage(string Role, string Content);
    private sealed record ChatCompletionRequest(string Model, IReadOnlyList<ChatMessage> Messages, double Temperature, [property: JsonPropertyName("max_tokens")] int MaxTokens);
    private sealed record ChatCompletionResponse(IReadOnlyList<ChatChoice>? Choices);
    private sealed record ChatChoice(ChatResponseMessage? Message);
    private sealed record ChatResponseMessage(string? Content);
    private sealed record SimplificationEnvelope(IReadOnlyList<SimplifiedExplanation>? Explanations);
    private sealed record SimplifiedExplanation(string? ItemId, string? PlainLanguageExplanation);
    private sealed record SafeSimplificationItem(string ItemId, string OutcomeStatus, string Severity, string ReasonCode);
}
