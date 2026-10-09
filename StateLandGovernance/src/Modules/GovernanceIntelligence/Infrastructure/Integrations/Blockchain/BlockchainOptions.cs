using System;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.Blockchain;

/// <summary>
/// Options for configuring the HTTP connection to the Governance Blockchain Microservice.
/// </summary>
public sealed class BlockchainOptions
{
    public const string SectionName = "GovernanceIntelligence:Blockchain";

    public string BaseUrl { get; set; } = "http://localhost:8550";
    public int TimeoutSeconds { get; set; } = 5;
    public int MaxRetries { get; set; } = 3;
    public string? ApiKey { get; set; }

    public static bool TryCreateBaseUri(string? baseUrl, out Uri? baseUri, out string? errorMessage)
    {
        baseUri = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            errorMessage = "Blockchain service BaseUrl cannot be empty.";
            return false;
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            errorMessage = $"Blockchain service BaseUrl '{baseUrl}' must be a valid absolute HTTP/HTTPS URI.";
            return false;
        }

        return true;
    }
}
