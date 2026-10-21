using System.Globalization;
using Azure;

namespace StateLandGovernance.LeaseFeasibility.Infrastructure.Services;

public sealed record FinancialOcrRetryDecision(bool CanRetry, bool StopDocument, TimeSpan Delay, string Message);
public sealed class FinancialOcrOperationException(string message) : Exception(message);

public static class FinancialOcrRetryPolicy
{
    public static FinancialOcrRetryDecision Evaluate(RequestFailedException error, int attempt)
    {
        var response = error.GetRawResponse();
        string? retryAfter = null;
        if (response != null && response.Headers.TryGetValue("Retry-After", out var value)) retryAfter = value;
        return Evaluate(error.Status, error.ErrorCode, error.Message, retryAfter, attempt, DateTimeOffset.UtcNow);
    }

    public static FinancialOcrRetryDecision Evaluate(int status, string? code, string message,
        string? retryAfter, int attempt, DateTimeOffset now)
    {
        var quota = status == 429 && (code?.Contains("Quota", StringComparison.OrdinalIgnoreCase) == true ||
            message.Contains("out of call volume", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("quota exceeded", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("monthly", StringComparison.OrdinalIgnoreCase));
        var seconds = double.TryParse(retryAfter, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number : DateTimeOffset.TryParse(retryAfter, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
                ? (date - now).TotalSeconds : 0;
        var delay = TimeSpan.FromSeconds(Math.Max(seconds, Math.Min(30, 2 * Math.Pow(2, attempt - 1))));
        if (quota)
            return new(false, true, delay, "Azure OCR's resource quota is exhausted. Wait for the quota reset or switch to a resource with available quota (for example S0). Splitting and repeated retries cannot restore quota.");
        if (status == 429 && delay > TimeSpan.FromMinutes(2))
            return new(false, true, delay, $"Azure OCR requires a cooldown of at least {Math.Ceiling(delay.TotalSeconds)} seconds. Processing stopped; retry after the cooldown or check the resource quota.");
        return new(status is 0 or 408 or 429 or >= 500, false, delay, status == 0
            ? "The connection to Azure OCR failed before an HTTP response was received. Retry after checking network connectivity."
            : status == 429
            ? "Azure OCR is temporarily throttling requests (HTTP 429). Wait before retrying extraction."
            : $"OCR service rejected this batch (HTTP {status}). Check service availability, credentials and document readability.");
    }
}
