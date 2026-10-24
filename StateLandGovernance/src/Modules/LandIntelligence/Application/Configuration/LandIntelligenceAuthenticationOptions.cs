namespace StateLandGovernance.LandIntelligence.Application.Configuration;

public sealed class LandIntelligenceAuthenticationOptions
{
    public const string SectionName = "LandIntelligence:Authentication";

    /// <summary>Base64-encoded symmetric key (at least 32 bytes) for signing applicant/officer JWTs.</summary>
    public string? JwtSigningKeyBase64 { get; set; }

    /// <summary>When true in Development only, accepts X-Land-Intelligence-Owner-Id as a demo applicant principal.</summary>
    public bool AllowDemoOwnerHeader { get; set; }

    /// <summary>When true in Development only, exposes POST /api/v1/land/auth/session to mint JWTs for local testing.</summary>
    public bool AllowDevelopmentSessionEndpoint { get; set; }

    public string Issuer { get; set; } = "state-land-governance";

    public string Audience { get; set; } = "land-intelligence";

    public int AccessTokenLifetimeMinutes { get; set; } = 480;
}
