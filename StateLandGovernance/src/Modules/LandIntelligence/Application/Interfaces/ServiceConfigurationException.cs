namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Raised when a required external dependency is not configured or unavailable at runtime.
/// </summary>
public sealed class ServiceConfigurationException : Exception
{
    public ServiceConfigurationException(string message)
        : base(message)
    {
    }
}
