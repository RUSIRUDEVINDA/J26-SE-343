using System.Text.RegularExpressions;

namespace StateLandGovernance.LandIntelligence.Application.LeaseInterest;

/// <summary>
/// Normalization and validation for Sri Lankan old (9 digits + V/X) and new (12 digit) NIC formats.
/// </summary>
public static partial class SriLankanNic
{
    private static readonly Regex OldNicPattern = OldNicRegex();
    private static readonly Regex NewNicPattern = NewNicRegex();

    /// <summary>
    /// Strips whitespace and hyphens, uppercases trailing V/X. Returns false when nothing remains.
    /// </summary>
    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var compact = Regex.Replace(input.Trim(), @"[\s\-]", string.Empty);
        if (compact.Length == 0)
        {
            return false;
        }

        if (compact.Length == 10 && compact[^1] is 'v' or 'x')
        {
            compact = compact[..9] + char.ToUpperInvariant(compact[^1]);
        }

        normalized = compact;
        return true;
    }

    public static bool IsValidNormalized(string normalized) =>
        OldNicPattern.IsMatch(normalized) || NewNicPattern.IsMatch(normalized);

    public static bool TryValidate(string? input, out string normalized, out string? error)
    {
        error = null;
        if (!TryNormalize(input, out normalized))
        {
            error = "National Identity Card (NIC) number is required.";
            return false;
        }

        if (!IsValidNormalized(normalized))
        {
            error =
                "Enter a valid Sri Lankan NIC (9 digits followed by V or X, or a 12-digit new NIC).";
            return false;
        }

        return true;
    }

    /// <summary>Mask for display in review and officer lists (never log the full value).</summary>
    public static string Mask(string? normalizedOrDisplay)
    {
        if (string.IsNullOrWhiteSpace(normalizedOrDisplay))
        {
            return "—";
        }

        var value = normalizedOrDisplay.Trim();
        if (value.Length <= 4)
        {
            return new string('*', value.Length);
        }

        return new string('*', value.Length - 4) + value[^4..];
    }

    [GeneratedRegex(@"^\d{9}[VX]$", RegexOptions.CultureInvariant)]
    private static partial Regex OldNicRegex();

    [GeneratedRegex(@"^\d{12}$", RegexOptions.CultureInvariant)]
    private static partial Regex NewNicRegex();
}
