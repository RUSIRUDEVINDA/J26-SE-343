namespace StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;

public static class AdministrativeNameNormalizer
{
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var normalized = name.Trim();

        if (normalized.EndsWith(" Province", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^9].TrimEnd();
        }

        if (normalized.EndsWith(" District", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^9].TrimEnd();
        }

        normalized = string.Join(
            ' ',
            normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return normalized.ToUpperInvariant();
    }

    public static bool NamesMatch(string? stored, string? detected)
    {
        var normalizedStored = Normalize(stored);
        var normalizedDetected = Normalize(detected);

        return normalizedStored.Length > 0
            && normalizedDetected.Length > 0
            && normalizedStored == normalizedDetected;
    }
}
