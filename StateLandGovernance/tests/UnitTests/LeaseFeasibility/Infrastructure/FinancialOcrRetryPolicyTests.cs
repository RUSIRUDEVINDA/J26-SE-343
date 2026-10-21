using Azure;
using Microsoft.Extensions.Configuration;
using PdfSharp.Pdf;
using StateLandGovernance.LeaseFeasibility.Infrastructure.Services;

namespace StateLandGovernance.UnitTests.LeaseFeasibility.Infrastructure;

public sealed class FinancialOcrRetryPolicyTests
{
    [Fact]
    public void StatusZeroIsATransportFailureAndCanRetry()
    {
        var decision = FinancialOcrRetryPolicy.Evaluate(0, null, "connection reset", null, 1, DateTimeOffset.UtcNow);
        Assert.True(decision.CanRetry);
        Assert.Contains("connection", decision.Message);
        Assert.DoesNotContain("HTTP 0", decision.Message);
    }
    [Theory]
    [InlineData("12", 12)]
    [InlineData(null, 2)]
    public void RetryAfterAndBackoffAreRespected(string? header, int seconds)
    {
        var decision = FinancialOcrRetryPolicy.Evaluate(429, null, "Too many requests", header, 1, DateTimeOffset.UtcNow);
        Assert.True(decision.CanRetry);
        Assert.False(decision.StopDocument);
        Assert.Equal(seconds, decision.Delay.TotalSeconds);
    }

    [Fact]
    public void DateRetryAfterIsRespected()
    {
        var now = new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
        var decision = FinancialOcrRetryPolicy.Evaluate(429, null, "throttled", now.AddSeconds(30).ToString("R"), 1, now);
        Assert.Equal(30, decision.Delay.TotalSeconds);
    }

    [Theory]
    [InlineData("Out of call volume quota for FormRecognizer F0 pricing tier.")]
    [InlineData("Monthly page allowance exhausted")]
    public void ExhaustedQuotaDoesNotRetry(string message)
    {
        var decision = FinancialOcrRetryPolicy.Evaluate(429, null, message, null, 1, DateTimeOffset.UtcNow);
        Assert.False(decision.CanRetry);
        Assert.True(decision.StopDocument);
        Assert.Contains("quota", decision.Message);
    }

    [Fact]
    public void LongCooldownStopsWithoutBlockingForHours()
    {
        var decision = FinancialOcrRetryPolicy.Evaluate(429, null, "throttled", "86400", 1, DateTimeOffset.UtcNow);
        Assert.False(decision.CanRetry);
        Assert.True(decision.StopDocument);
        Assert.Contains("86400", decision.Message);
    }

    [Fact]
    public async Task ExhaustedQuotaStopsRemainingBatches()
    {
        using var pdf = new PdfDocument();
        for (var index = 0; index < 5; index++) pdf.AddPage();
        using var stream = new MemoryStream();
        pdf.Save(stream, closeStream: false);
        stream.Position = 0;
        var reader = new QuotaOcr();
        var result = await new UploadedFinancialDocumentService(new ConfigurationBuilder().Build(), reader)
            .ExtractAsync(stream, "crib", "test.pdf", CancellationToken.None);
        Assert.False(result.IsComplete);
        Assert.Equal(1, reader.Calls);
        Assert.Equal(new[] { 1, 0, 0 }, result.Batches.Select(batch => batch.Attempts));
        Assert.Empty(result.Fields);
    }

    private sealed class QuotaOcr : IFinancialDocumentBatchOcr
    {
        public int Calls { get; private set; }
        public Task<FinancialBatchRead> ReadAsync(Stream stream, CancellationToken cancellationToken)
        {
            Calls++;
            throw new RequestFailedException(429, "Out of call volume quota for FormRecognizer F0 pricing tier.");
        }
    }
}
