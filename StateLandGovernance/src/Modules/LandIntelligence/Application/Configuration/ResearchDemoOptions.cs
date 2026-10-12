namespace StateLandGovernance.LandIntelligence.Application.Configuration;

/// <summary>
/// Opt-in flag for exposing simulated lease workflow statuses from the isolated research dataset.
/// Disabled by default; never affects recommendations or legal eligibility.
/// </summary>
public sealed class ResearchDemoOptions
{
    public const string SectionName = "LandIntelligence:ResearchDemo";

    public bool Enabled { get; set; }
}
