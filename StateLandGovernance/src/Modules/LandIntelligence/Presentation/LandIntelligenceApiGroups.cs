namespace StateLandGovernance.LandIntelligence.Presentation;

/// <summary>
/// OpenAPI document groups for Component 1 HTTP surfaces.
/// </summary>
public static class LandIntelligenceApiGroups
{
    /// <summary>
    /// Read-only contract consumed by external platform modules.
    /// </summary>
    public const string External = "land-intelligence-external-v1";

    /// <summary>
    /// Component 1 maintenance and persistence operations (not for external modules).
    /// </summary>
    public const string Internal = "land-intelligence-internal-v1";
}
