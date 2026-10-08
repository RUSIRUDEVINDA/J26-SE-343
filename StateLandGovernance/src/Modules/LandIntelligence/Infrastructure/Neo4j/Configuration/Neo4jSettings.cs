namespace StateLandGovernance.LandIntelligence.Infrastructure.Neo4j.Configuration;

/// <summary>
/// Neo4j connection settings loaded from environment variables.
/// </summary>
public sealed class Neo4jSettings
{
    public const string ConnectionEnvironmentVariable = "NEO4J_CONNECTION";
    public const string UsernameEnvironmentVariable = "NEO4J_USERNAME";
    public const string PasswordEnvironmentVariable = "NEO4J_PASSWORD";

    public string Uri { get; }
    public string Username { get; }
    public string Password { get; }

    public Neo4jSettings(string uri, string username, string password)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            throw new ArgumentException("Neo4j URI is required.", nameof(uri));
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Neo4j username is required.", nameof(username));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Neo4j password is required.", nameof(password));
        }

        Uri = uri.Trim();
        Username = username.Trim();
        Password = password;
    }

    public static Neo4jSettings FromEnvironment()
    {
        var uri = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        var username = Environment.GetEnvironmentVariable(UsernameEnvironmentVariable);
        var password = Environment.GetEnvironmentVariable(PasswordEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(uri))
        {
            throw new InvalidOperationException(
                $"Set {ConnectionEnvironmentVariable} in .env at the repository root.");
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new InvalidOperationException(
                $"Set {UsernameEnvironmentVariable} in .env at the repository root.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                $"Set {PasswordEnvironmentVariable} in .env at the repository root.");
        }

        return new Neo4jSettings(uri, username, password);
    }

    public static bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable))
            && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(UsernameEnvironmentVariable))
            && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PasswordEnvironmentVariable));
    }
}
