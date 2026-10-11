namespace StateLandGovernance.LandIntelligence.Application.Configuration;

public sealed class LeaseInterestOptions
{
    public const string SectionName = "LandIntelligence:LeaseInterest";

    /// <summary>Maximum requested term (years) accepted on interest forms — not an approved lease length.</summary>
    public int MaxRequestedDurationYears { get; set; } = 30;

    public int MinRequestedDurationYears { get; set; } = 1;
}
