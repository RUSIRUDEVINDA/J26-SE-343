using Microsoft.Extensions.Configuration;

namespace StateLandGovernance.IntegrationTests.LandIntelligence;

internal static class LandIntelligenceIntegrationConfiguration
{
    public static IConfiguration LoadApiConfiguration()
    {
        var apiSettingsDirectory = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Api"));

        if (!Directory.Exists(apiSettingsDirectory))
        {
            throw new InvalidOperationException(
                $"Api configuration directory was not found at '{apiSettingsDirectory}'. " +
                "Ensure src/Api/appsettings.Development.json exists.");
        }

        var developmentSettingsPath = Path.Combine(apiSettingsDirectory, "appsettings.Development.json");
        if (!File.Exists(developmentSettingsPath))
        {
            throw new InvalidOperationException(
                $"Development settings file was not found at '{developmentSettingsPath}'. " +
                "Configure ConnectionStrings:LandIntelligence before running integration tests.");
        }

        return new ConfigurationBuilder()
            .SetBasePath(apiSettingsDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: false)
            .AddEnvironmentVariables()
            .Build();
    }
}
