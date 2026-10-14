using Microsoft.Extensions.Options;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.ComplaintClassification;

public sealed class ComplaintClassifierOptions
{
    public const string SectionName = "ComplaintClassifier";
    public const int MaximumTimeoutSeconds = int.MaxValue / 1000;

    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; }

    internal static bool TryCreateBaseUri(
        string? configuredBaseUrl,
        out Uri? baseUri,
        out string? validationError)
    {
        baseUri = null;

        if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var parsedUri) ||
            (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps))
        {
            validationError =
                $"{SectionName}:BaseUrl must be an absolute HTTP or HTTPS URL.";
            return false;
        }

        if (!string.IsNullOrEmpty(parsedUri.UserInfo))
        {
            validationError =
                $"{SectionName}:BaseUrl must not include embedded username or password credentials.";
            return false;
        }

        if (!string.IsNullOrEmpty(parsedUri.Query))
        {
            validationError = $"{SectionName}:BaseUrl must not include a query string.";
            return false;
        }

        if (!string.IsNullOrEmpty(parsedUri.Fragment))
        {
            validationError = $"{SectionName}:BaseUrl must not include a fragment.";
            return false;
        }

        var uriBuilder = new UriBuilder(parsedUri);
        if (!uriBuilder.Path.EndsWith("/", StringComparison.Ordinal))
        {
            uriBuilder.Path += '/';
        }

        baseUri = uriBuilder.Uri;
        validationError = null;
        return true;
    }
}

internal sealed class ComplaintClassifierOptionsValidator : IValidateOptions<ComplaintClassifierOptions>
{
    public ValidateOptionsResult Validate(string? name, ComplaintClassifierOptions options)
    {
        var failures = new List<string>();

        if (!ComplaintClassifierOptions.TryCreateBaseUri(
                options.BaseUrl,
                out _,
                out var baseUrlValidationError))
        {
            failures.Add(baseUrlValidationError!);
        }

        if (options.TimeoutSeconds is <= 0 or > ComplaintClassifierOptions.MaximumTimeoutSeconds)
        {
            failures.Add(
                $"{ComplaintClassifierOptions.SectionName}:TimeoutSeconds must be between 1 and " +
                $"{ComplaintClassifierOptions.MaximumTimeoutSeconds}.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
