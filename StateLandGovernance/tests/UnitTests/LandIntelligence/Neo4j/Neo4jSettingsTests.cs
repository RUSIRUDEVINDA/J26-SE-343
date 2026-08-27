using StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Configuration;

namespace StateLandGovernance.UnitTests.LandIntelligence.Neo4j;

public sealed class Neo4jSettingsTests
{
    [Fact]
    public void FromEnvironment_throws_when_connection_is_missing()
    {
        RestoreEnvironment(
            (Neo4jSettings.ConnectionEnvironmentVariable, null),
            (Neo4jSettings.UsernameEnvironmentVariable, "neo4j"),
            (Neo4jSettings.PasswordEnvironmentVariable, "password"));

        var exception = Assert.Throws<InvalidOperationException>(Neo4jSettings.FromEnvironment);
        Assert.Contains(Neo4jSettings.ConnectionEnvironmentVariable, exception.Message);
    }

    [Fact]
    public void FromEnvironment_returns_settings_when_all_variables_are_set()
    {
        RestoreEnvironment(
            (Neo4jSettings.ConnectionEnvironmentVariable, "bolt://localhost:7687"),
            (Neo4jSettings.UsernameEnvironmentVariable, "neo4j"),
            (Neo4jSettings.PasswordEnvironmentVariable, "synthetic-password"));

        var settings = Neo4jSettings.FromEnvironment();

        Assert.Equal("bolt://localhost:7687", settings.Uri);
        Assert.Equal("neo4j", settings.Username);
        Assert.Equal("synthetic-password", settings.Password);
    }

    [Fact]
    public void IsConfigured_returns_false_when_any_variable_is_missing()
    {
        RestoreEnvironment(
            (Neo4jSettings.ConnectionEnvironmentVariable, "bolt://localhost:7687"),
            (Neo4jSettings.UsernameEnvironmentVariable, null),
            (Neo4jSettings.PasswordEnvironmentVariable, "password"));

        Assert.False(Neo4jSettings.IsConfigured());
    }

    private static void RestoreEnvironment(params (string Key, string? Value)[] values)
    {
        foreach (var (key, value) in values)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
