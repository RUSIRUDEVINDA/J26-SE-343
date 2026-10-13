using Microsoft.Extensions.Options;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.Explainability;

public sealed class GroqExplainabilityOptions
{
    public const string SectionName = "GroqExplainability";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1/";
    public string Model { get; set; } = "qwen/qwen3.8-27b";
    public string Token { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 15;
    public int MaxInputCharacters { get; set; } = 12000;
    public int MaxOutputCharacters { get; set; } = 12000;

    internal static bool TryCreateBaseUri(string? value, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed))
            return false;

        var isHttps = string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        var isLoopbackHttp = parsed.IsLoopback &&
            string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
        if ((!isHttps && !isLoopbackHttp) ||
            !string.IsNullOrEmpty(parsed.UserInfo) || !string.IsNullOrEmpty(parsed.Query) || !string.IsNullOrEmpty(parsed.Fragment))
        {
            return false;
        }

        var builder = new UriBuilder(parsed);
        if (!builder.Path.EndsWith('/')) builder.Path += '/';
        uri = builder.Uri;
        return true;
    }
}

internal sealed class GroqExplainabilityOptionsValidator : IValidateOptions<GroqExplainabilityOptions>
{
    public ValidateOptionsResult Validate(string? name, GroqExplainabilityOptions options)
    {
        var failures = new List<string>();
        if (!GroqExplainabilityOptions.TryCreateBaseUri(options.BaseUrl, out _))
            failures.Add($"{GroqExplainabilityOptions.SectionName}:BaseUrl must be an HTTPS URL without credentials, query, or fragment.");
        if (options.Enabled && string.IsNullOrWhiteSpace(options.Model))
            failures.Add($"{GroqExplainabilityOptions.SectionName}:Model is required when enabled.");
        if (options.Enabled && string.IsNullOrWhiteSpace(options.Token))
            failures.Add($"{GroqExplainabilityOptions.SectionName}:Token is required when enabled.");
        if (options.TimeoutSeconds is < 1 or > 120)
            failures.Add($"{GroqExplainabilityOptions.SectionName}:TimeoutSeconds must be between 1 and 120.");
        if (options.MaxInputCharacters is < 1000 or > 50000)
            failures.Add($"{GroqExplainabilityOptions.SectionName}:MaxInputCharacters must be between 1000 and 50000.");
        if (options.MaxOutputCharacters is < 1000 or > 50000)
            failures.Add($"{GroqExplainabilityOptions.SectionName}:MaxOutputCharacters must be between 1000 and 50000.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
