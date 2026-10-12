namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.Mappers;

using System;
using System.Text.RegularExpressions;

/// <summary>
/// Deterministic parser for lease period strings into integer years.
/// Supports clean forms such as '30 years', '30 Years', '30', '99 yrs'.
/// Ambiguous, ranges, or non-numeric text returns fallback (default 0) — never guessed.
/// </summary>
public static class LeasePeriodNormalizer
{
    private static readonly Regex SimpleYearsRegex = new(
        @"^\s*(?<years>\d+)\s*(?:years?|yrs?)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parses integer lease duration in years from verified text.
    /// Returns null if missing, ambiguous, non-numeric, or <= 0 — never guesses.
    /// </summary>
    public static int? TryParseYears(string? periodText)
    {
        if (string.IsNullOrWhiteSpace(periodText))
        {
            return null;
        }

        var match = SimpleYearsRegex.Match(periodText.Trim());
        if (match.Success && int.TryParse(match.Groups["years"].Value, out var years) && years > 0)
        {
            return years;
        }

        return null;
    }

    /// <summary>
    /// Backward-compatible overload providing fallback integer when needed.
    /// </summary>
    public static int TryParseYears(string? periodText, int fallback)
    {
        return TryParseYears(periodText) ?? fallback;
    }
}

