namespace StateLandGovernance.LandIntelligence.Application.LeaseInterest;

public static class LeaseInterestReferenceGenerator
{
    public static string Create(DateTimeOffset submittedAtUtc)
    {
        var stamp = submittedAtUtc.UtcDateTime.ToString("yyyyMMdd");
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"LIR-{stamp}-{suffix}";
    }
}
