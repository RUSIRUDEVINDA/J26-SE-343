using System.Globalization;
using System.Text.RegularExpressions;
using StateLandGovernance.LeaseFeasibility.Application.DTOs;
using StateLandGovernance.LeaseFeasibility.Application.Interfaces;

namespace StateLandGovernance.LeaseFeasibility.Infrastructure.Services;

/// <summary>
/// Conservative parser of labelled CRIB evidence. Missing data remains unknown.
/// Facility tables require review; this parser never infers a monthly payment from a balance.
/// </summary>
public static class CribReportParser
{
    public static string? NormalizeGrade(string? grade)
    {
        var value = grade?.Trim().ToUpperInvariant();
        return value != null && Regex.IsMatch(value, "^[A-E][1-3]?$") ? value[..1] : null;
    }

    public static CribReportDataDto Parse(string text)
    {
        var gradeMatch = Regex.Match(text, @"(?:Credit\s*Risk\s*Grade|Risk\s*Grade)[\s:]+(XX|[A-E][1-3]?)(?![A-Za-z0-9])", RegexOptions.IgnoreCase);
        if (!gradeMatch.Success)
            throw new ValidationException(new[] { "Missing or unsupported CRIB Risk Grade. Review the report; do not infer a grade." });

        var rawGrade = gradeMatch.Groups[1].Value.ToUpperInvariant();
        var normalized = NormalizeGrade(rawGrade);
        var reasons = new List<string>();
        if (normalized == null) reasons.Add("CRIB XX means insufficient information, not poor credit or a lease rejection.");

        // The bureau report does not provide a standard Default History Indicator field.
        // Only an explicitly reviewed value may enter the existing scoring contract.
        var historyMatch = Regex.Match(text, @"(?:Reviewed\s+)?Default\s*History\s*Indicator[\s:]+(true|false|yes|no)\b", RegexOptions.IgnoreCase);
        bool? history = historyMatch.Success
            ? historyMatch.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase) || historyMatch.Groups[1].Value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            : null;
        if (!history.HasValue) reasons.Add("Default history requires review of facility status and repayment history under a documented research rule.");

        var scoreValue = Number(text, @"(?:Credit\s+Score|Scoring\s+Score|^Score)");
        int? score = scoreValue.HasValue && scoreValue >= 250m && scoreValue <= 900m && scoreValue == decimal.Truncate(scoreValue.Value) ? (int)scoreValue.Value : null;
        if (scoreValue.HasValue && (!score.HasValue || score < 250 || score > 900))
            throw new ValidationException(new[] { "CRIB score must be an integer between 250 and 900." });
        var probability = Number(text, @"Probability\s+of\s+Default");
        if (probability is < 0m or > 100m)
            throw new ValidationException(new[] { "CRIB default probability percentage must be between 0 and 100." });

        var monthly = Number(text, @"Reviewed\s+Monthly\s+Debt\s+Obligations(?:\s+LKR)?");
        if (!monthly.HasValue) reasons.Add("Monthly debt needs verified active borrower facilities and repayment schedules; balances and credit limits are not monthly payments.");
        var disputes = Count(text, @"Active\s+Disputes");
        if (disputes > 0) reasons.Add("Active disputes require review before using affected evidence.");

        return new CribReportDataDto(rawGrade, null, history,
            Count(text, @"(?:Lender\s+Inquiries\s+Last\s+6\s+Months|Recent\s+Credit\s+Inquiries)"))
        {
            NormalizedCreditRiskGrade = normalized,
            CreditScore = score,
            BureauProbabilityOfDefaultPercent = probability,
            OutstandingBalanceLkr = Number(text, @"(?:Total\s+Current\s+Balance|Total\s+Current\s+Balance\s+LKR)"),
            ReportedMonthlyPaymentsLkr = monthly,
            SelfInquiriesLastSixMonths = Count(text, @"Self[ -]?Inquiries\s+(?:During\s+the\s+)?Last\s+6\s+Months"),
            ActiveDisputes = disputes,
            RequiresManualReview = reasons.Count > 0,
            ReviewReasons = reasons
        };
    }

    private static decimal? Number(string text, string label)
    {
        // Reject ambiguous repeated labels (common in multi-facility tables).
        var matches = Regex.Matches(text, label + @"[\s:]+(?:LKR\s*)?([0-9]+(?:,[0-9]{3})*(?:\.[0-9]+)?)(?![0-9,])", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return matches.Count == 1 && decimal.TryParse(matches[0].Groups[1].Value.Replace(",", ""), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static int? Count(string text, string label)
    {
        var value = Number(text, label);
        return value.HasValue && value >= 0 && value <= int.MaxValue && value == decimal.Truncate(value.Value) ? (int)value.Value : null;
    }
}
