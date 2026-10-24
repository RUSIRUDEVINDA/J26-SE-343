using Microsoft.Extensions.Options;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.WorkflowAnomaly;

public sealed class WorkflowAnomalyOptions
{
    public const string SectionName = "WorkflowAnomaly";
    public const int MaximumTimeoutSeconds = int.MaxValue / 1000;

    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; }
    public int MaxEvents { get; set; } = WorkflowAnomalyConstraints.MaximumEvents;

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

internal sealed class WorkflowAnomalyOptionsValidator : IValidateOptions<WorkflowAnomalyOptions>
{
    public ValidateOptionsResult Validate(string? name, WorkflowAnomalyOptions options)
    {
        var failures = new List<string>();

        if (!WorkflowAnomalyOptions.TryCreateBaseUri(
                options.BaseUrl,
                out _,
                out var baseUrlValidationError))
        {
            failures.Add(baseUrlValidationError!);
        }

        if (options.TimeoutSeconds is <= 0 or > WorkflowAnomalyOptions.MaximumTimeoutSeconds)
        {
            failures.Add(
                $"{WorkflowAnomalyOptions.SectionName}:TimeoutSeconds must be between 1 and " +
                $"{WorkflowAnomalyOptions.MaximumTimeoutSeconds}.");
        }

        if (options.MaxEvents <= 0)
        {
            failures.Add(
                $"{WorkflowAnomalyOptions.SectionName}:MaxEvents must be a positive integer.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
