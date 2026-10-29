using System.Text.RegularExpressions;

namespace StateLandGovernance.LandIntelligence.Application.LeaseInterest;

/// <summary>Normalizes business registration numbers (BRN) for storage and officer lookup.</summary>
public static partial class LeaseInterestBusinessRegistration
{
    private const int MinLength = 3;
    private const int MaxLength = 64;

    public static bool TryNormalize(string? raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var compact = Regex.Replace(raw.Trim(), @"[\s\-_/]", string.Empty).ToUpperInvariant();
        if (compact.Length < MinLength || compact.Length > MaxLength)
        {
            return false;
        }

        if (!BrnBodyRegex().IsMatch(compact))
        {
            return false;
        }

        normalized = compact;
        return true;
    }

    public static bool TryValidate(string? raw, out string normalized, out string? error)
    {
        if (TryNormalize(raw, out normalized))
        {
            error = null;
            return true;
        }

        error = string.IsNullOrWhiteSpace(raw)
            ? "Business registration number (BRN) is required."
            : "Enter a valid business registration number (BRN) using letters and digits.";
        normalized = string.Empty;
        return false;
    }

    [GeneratedRegex("^[A-Z0-9]+$")]
    private static partial Regex BrnBodyRegex();
}
