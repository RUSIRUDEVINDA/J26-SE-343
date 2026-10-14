namespace StateLandGovernance.LandIntelligence.Application.Configuration;

/// <summary>
/// Opt-in local wiring for the Colombo backend-compatible experimental ML candidate.
/// Default disabled — production ML path and live model remain unchanged.
/// </summary>
public sealed class ExperimentalColomboMlOptions
{
    public const string SectionName = "LandIntelligence:ExperimentalColomboMl";

    /// <summary>
    /// When false (default), experimental ML is never called and production ML path is used.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Base URL of experimental_ml_service.py (default http://127.0.0.1:8501).
    /// Must not point at the production ml_service port unless explicitly intended for local tests.
    /// </summary>
    public string ServiceBaseUrl { get; set; } = "http://127.0.0.1:8501";

    /// <summary>
    /// Expected candidate id from feature_schema.json.
    /// </summary>
    public string ExpectedCandidateId { get; set; } = "colombo_osm_backend_compatible";

    /// <summary>
    /// HTTP timeout for experimental predict calls.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// When Enabled, skip the production IMlSuitabilityClient to avoid dual-model calls.
    /// </summary>
    public bool SuppressProductionMlWhenEnabled { get; set; } = true;
}
