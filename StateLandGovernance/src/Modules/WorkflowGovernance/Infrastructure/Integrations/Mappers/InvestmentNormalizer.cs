namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.Mappers;

using System;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Result container for investment amount normalization including currency validation and diagnostic message.
/// </summary>
public sealed record InvestmentNormalizationResult(
    decimal? Amount,
    string? Currency,
    bool IsLkr,
    string? DiagnosticMessage
);

/// <summary>
/// Currency-safe normalizer for investment amounts from verified proposal text.
/// 
/// Governance Rules:
/// 1. LKR / Rs. verified currency -> Populates C4 numeric budget amount.
/// 2. Explicit non-LKR currencies (USD, EUR, GBP, etc.) -> Leaves C4 numeric budget unavailable and records diagnostic.
///    Never performs currency conversion or exchange rate lookups.
/// 3. Currency absent -> Leaves C4 numeric budget unavailable (never silently assumes LKR).
/// 4. Invalid or negative numbers -> Returns null.
/// </summary>
public static class InvestmentNormalizer
{
    private static readonly Regex LkrPrefixRegex = new(
        @"^(?:lkr|rs\.?|sri\s+lankan\s+rupees?|slr)\s*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LkrSuffixRegex = new(
        @"\s*(?:lkr|rs\.?|sri\s+lankan\s+rupees?|slr)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ForeignCurrencyPrefixRegex = new(
        @"^(?:usd|eur|gbp|aud|cad|inr|jpy|\$|€|£|¥)\s*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ForeignCurrencySuffixRegex = new(
        @"\s*(?:usd|eur|gbp|aud|cad|inr|jpy|\$|€|£|¥)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Currency-safe investment normalizer enforcing explicit Sri Lankan Rupee requirements for C4 budget.
    /// </summary>
    public static InvestmentNormalizationResult NormalizeInvestment(string? amountText, string? explicitCurrency = null)
    {
        if (string.IsNullOrWhiteSpace(amountText))
        {
            return new InvestmentNormalizationResult(null, null, false, "Investment amount is missing or empty.");
        }

        var raw = amountText.Trim();
        string? detectedCurrency = null;
        bool isLkr = false;
        bool isNonLkr = false;

        // 1. Inspect explicitCurrency first if provided
        if (!string.IsNullOrWhiteSpace(explicitCurrency))
        {
            var cur = explicitCurrency.Trim();
            if (IsLkrString(cur))
            {
                isLkr = true;
                detectedCurrency = "LKR";
            }
            else
            {
                isNonLkr = true;
                detectedCurrency = cur.ToUpperInvariant();
            }
        }
        else
        {
            // 2. Inspect source amountText for currency symbols or words
            if (LkrPrefixRegex.IsMatch(raw) || LkrSuffixRegex.IsMatch(raw))
            {
                isLkr = true;
                detectedCurrency = "LKR";
            }
            else
            {
                var foreignPrefix = ForeignCurrencyPrefixRegex.Match(raw);
                var foreignSuffix = ForeignCurrencySuffixRegex.Match(raw);
                if (foreignPrefix.Success)
                {
                    isNonLkr = true;
                    detectedCurrency = foreignPrefix.Value.Trim().ToUpperInvariant();
                }
                else if (foreignSuffix.Success)
                {
                    isNonLkr = true;
                    detectedCurrency = foreignSuffix.Value.Trim().ToUpperInvariant();
                }
            }
        }

        // 3. Clean numeric digits
        var cleaned = raw;
        cleaned = LkrPrefixRegex.Replace(cleaned, "");
        cleaned = LkrSuffixRegex.Replace(cleaned, "");
        cleaned = ForeignCurrencyPrefixRegex.Replace(cleaned, "");
        cleaned = ForeignCurrencySuffixRegex.Replace(cleaned, "");
        cleaned = cleaned.Replace(",", "").Trim();

        if (!decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var numericAmount) || numericAmount < 0)
        {
            return new InvestmentNormalizationResult(null, detectedCurrency, isLkr, "Investment amount could not be parsed as a numeric value.");
        }

        // 4. Enforce currency governance rules
        if (isNonLkr)
        {
            return new InvestmentNormalizationResult(
                Amount: null,
                Currency: detectedCurrency,
                IsLkr: false,
                DiagnosticMessage: $"Investment amount not mapped: source currency {detectedCurrency} is not represented by the C4 compliance contract.");
        }

        if (isLkr)
        {
            return new InvestmentNormalizationResult(
                Amount: numericAmount,
                Currency: "LKR",
                IsLkr: true,
                DiagnosticMessage: null);
        }

        // 5. Currency absent - do not assume LKR
        return new InvestmentNormalizationResult(
            Amount: null,
            Currency: null,
            IsLkr: false,
            DiagnosticMessage: "Investment amount not mapped: currency is absent and cannot be assumed.");
    }

    private static bool IsLkrString(string cur)
    {
        return string.Equals(cur, "LKR", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(cur, "Rs", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(cur, "Rs.", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(cur, "Rupees", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(cur, "Sri Lankan Rupees", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(cur, "SLR", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Helper parsing numeric investment amount into a nullable decimal if currency is LKR.
    /// Returns null if currency is missing or non-LKR.
    /// </summary>
    public static decimal? TryParseAmount(string? amountText)
    {
        return NormalizeInvestment(amountText).Amount;
    }

    /// <summary>
    /// Extracts detected currency string if explicitly present, or null.
    /// </summary>
    public static string? TryExtractCurrency(string? amountText, string? explicitCurrency = null)
    {
        return NormalizeInvestment(amountText, explicitCurrency).Currency;
    }
}
