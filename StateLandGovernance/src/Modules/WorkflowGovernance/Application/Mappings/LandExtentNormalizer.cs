namespace StateLandGovernance.WorkflowGovernance.Application.Mappings;

using System;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Deterministic converter from verified land extent text to metric hectares (ha).
/// Supports explicit acres, hectares, roods, perches, square meters, and composite '2A 1R 10P' notation.
/// Unparseable or ambiguous text returns null — never guesses.
/// </summary>
public static class LandExtentNormalizer
{
    private const decimal HectaresPerAcre = 0.40468564224m;
    private const decimal HectaresPerRood = 0.10117141056m;
    private const decimal HectaresPerPerch = 0.002529285264m;
    private const decimal HectaresPerSquareMeter = 0.0001m;

    // Pattern for composite notation e.g. "2A 1R 10P" or "2 acres 1 rood 10 perches" or "2A, 1R, 10P"
    private static readonly Regex CompositeExtentRegex = new(
        @"^(?:(?<acres>\d+(?:\.\d+)?)\s*(?:a|acres?)\b[,\s]*)?(?:(?<roods>\d+(?:\.\d+)?)\s*(?:r|roods?)\b[,\s]*)?(?:(?<perches>\d+(?:\.\d+)?)\s*(?:p|perches?)\b)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Pattern for single value + embedded unit e.g. "10 acres", "5 ha", "100 perches"
    private static readonly Regex SingleValueUnitRegex = new(
        @"^(?<value>\d+(?:\.\d+)?)\s*(?<unit>[a-zA-Z\s\^2]+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Normalizes land extent and optional unit to decimal hectares without rounding (routing thresholds must retain precision).
    /// Returns null if input is null, empty, or unparseable.
    /// </summary>
    public static decimal? TryNormalizeToHectares(string? extentStr, string? unitStr = null)
    {
        if (string.IsNullOrWhiteSpace(extentStr))
        {
            return null;
        }

        var cleanedExtent = extentStr.Trim();
        var cleanedUnit = unitStr?.Trim();

        // 1. If explicit separate unit is provided and extent is numeric
        if (!string.IsNullOrWhiteSpace(cleanedUnit) &&
            decimal.TryParse(cleanedExtent, NumberStyles.Number, CultureInfo.InvariantCulture, out var rawNumeric))
        {
            return ConvertUnitToHectares(rawNumeric, cleanedUnit);
        }

        // 2. Check for composite notation e.g. "2A 1R 10P" or "2 acres 1 rood 10 perches"
        var compositeMatch = CompositeExtentRegex.Match(cleanedExtent);
        if (compositeMatch.Success &&
            (compositeMatch.Groups["acres"].Success || compositeMatch.Groups["roods"].Success || compositeMatch.Groups["perches"].Success))
        {
            decimal totalHa = 0m;
            if (compositeMatch.Groups["acres"].Success &&
                decimal.TryParse(compositeMatch.Groups["acres"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var acres))
            {
                totalHa += acres * HectaresPerAcre;
            }

            if (compositeMatch.Groups["roods"].Success &&
                decimal.TryParse(compositeMatch.Groups["roods"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var roods))
            {
                totalHa += roods * HectaresPerRood;
            }

            if (compositeMatch.Groups["perches"].Success &&
                decimal.TryParse(compositeMatch.Groups["perches"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var perches))
            {
                totalHa += perches * HectaresPerPerch;
            }

            return totalHa;
        }

        // 3. Check for embedded value + unit in extent string e.g. "10 acres", "5 ha"
        var singleMatch = SingleValueUnitRegex.Match(cleanedExtent);
        if (singleMatch.Success &&
            decimal.TryParse(singleMatch.Groups["value"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var singleValue))
        {
            var unit = singleMatch.Groups["unit"].Value.Trim();
            return ConvertUnitToHectares(singleValue, unit);
        }

        // 4. If extent is purely numeric and unit was not specified, cannot assume unit
        return null;
    }

    private static decimal? ConvertUnitToHectares(decimal value, string unit)
    {
        if (value < 0)
        {
            return null;
        }

        var normUnit = unit.Trim().ToLowerInvariant();

        switch (normUnit)
        {
            case "ha":
            case "hectare":
            case "hectares":
                return value;

            case "a":
            case "ac":
            case "acre":
            case "acres":
                return value * HectaresPerAcre;

            case "r":
            case "rood":
            case "roods":
                return value * HectaresPerRood;

            case "p":
            case "perch":
            case "perches":
                return value * HectaresPerPerch;

            case "sqm":
            case "m2":
            case "sq m":
            case "square meter":
            case "square meters":
            case "square metres":
                return value * HectaresPerSquareMeter;

            default:
                return null;
        }
    }
}
