namespace StateLandGovernance.Shared.Infrastructure.Configuration;

public static class EnvFileLoader
{
    public static void LoadFromRepositoryRoot(string? startDirectory = null)
    {
        var directory = startDirectory ?? Directory.GetCurrentDirectory();

        while (!string.IsNullOrEmpty(directory))
        {
            var envPath = Path.Combine(directory, ".env");
            if (File.Exists(envPath))
            {
                Load(envPath);
                return;
            }

            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }
    }

    public static void Load(string envFilePath)
    {
        if (!File.Exists(envFilePath))
        {
            return;
        }

        foreach (var rawLine in File.ReadAllLines(envFilePath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"');

            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
