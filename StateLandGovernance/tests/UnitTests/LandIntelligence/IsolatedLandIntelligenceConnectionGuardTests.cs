using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.Safety;

namespace StateLandGovernance.UnitTests.LandIntelligence;

public sealed class IsolatedLandIntelligenceConnectionGuardTests
{
    [Fact]
    public void Validate_rejects_application_database_name()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            IsolatedLandIntelligenceConnectionGuard.Validate(
                "Host=localhost;Port=5432;Database=state_land_governance;Username=postgres;Password=x"));

        Assert.Contains("protected database", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_requires_isolated_token_in_database_name()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            IsolatedLandIntelligenceConnectionGuard.Validate(
                "Host=localhost;Port=5432;Database=random_test_db;Username=postgres;Password=x"));

        Assert.Contains(GisEnrichmentCoverageDefaults.IsolatedDatabaseNameToken, ex.Message);
    }

    [Fact]
    public void Validate_accepts_disposable_isolated_database()
    {
        IsolatedLandIntelligenceConnectionGuard.Validate(
            "Host=localhost;Port=5432;Database=land_intel_colombo_exp_iso;Username=postgres;Password=x");
    }
}
