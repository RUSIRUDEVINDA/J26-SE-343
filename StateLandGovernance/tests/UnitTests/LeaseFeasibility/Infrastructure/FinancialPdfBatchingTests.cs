using Azure;
using Microsoft.Extensions.Configuration;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using StateLandGovernance.LeaseFeasibility.Infrastructure.Services;

namespace StateLandGovernance.UnitTests.LeaseFeasibility.Infrastructure;

public sealed class FinancialPdfBatchingTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(13)]
    public void SplitKeepsEveryPageInOrderIncludingOddFinalPage(int pageCount)
    {
        var plan = FinancialPdfBatcher.Split(CreatePdf(pageCount));
        Assert.Equal(pageCount, plan.TotalPages);
        Assert.Equal((pageCount + 1) / 2, plan.Batches.Count);
        var originalPages = new List<int>();
        foreach (var batch in plan.Batches)
        {
            using var stream = new MemoryStream(batch.Content);
            using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
            Assert.InRange(pdf.PageCount, 1, 2);
            Assert.Equal(batch.PageCount, pdf.PageCount);
            for (var index = 0; index < pdf.PageCount; index++)
            {
                // Unique page widths verify actual imported page order, not just batch labels.
                var number = (int)pdf.Pages[index].Width.Point - 100;
                Assert.Equal(batch.StartPage + index, number);
                originalPages.Add(number);
            }
        }
        Assert.Equal(Enumerable.Range(1, pageCount), originalPages);
    }

    [Fact]
    public async Task ThirteenPagesAreMergedWithOriginalTablePageNumbers()
    {
        var reader = new TestOcr();
        var result = await Extract(CreatePdf(13), reader);
        Assert.True(result.IsComplete);
        Assert.Equal(13, result.TotalPages);
        Assert.Equal(13, result.PagesRead);
        Assert.Equal(Enumerable.Range(1, 13), result.PageNumbers);
        Assert.Equal(7, reader.Calls);
        Assert.Equal(Enumerable.Range(1, 13), result.TablePageNumbers.SelectMany(pages => pages));
        Assert.True(result.Text.IndexOf("PAGE 1\n", StringComparison.Ordinal) < result.Text.IndexOf("PAGE 13\n", StringComparison.Ordinal));
        Assert.Equal("43210", result.Fields["income"]);
    }

    [Fact]
    public async Task PermanentFailurePreservesLaterBatchesAndBlocksFieldMapping()
    {
        var reader = new TestOcr((firstPage, _) => firstPage == 3 ? throw new RequestFailedException(401, "test failure") : null);
        var result = await Extract(CreatePdf(5), reader);
        Assert.False(result.IsComplete);
        Assert.Equal(new[] { 1, 2, 5 }, result.PageNumbers);
        Assert.Equal(3, result.PagesRead);
        Assert.Equal(5, result.TotalPages);
        Assert.Empty(result.Fields);
        Assert.False(result.Batches[1].Complete);
        Assert.Equal(1, result.Batches[1].Attempts);
        Assert.True(result.Batches[2].Complete);
        Assert.Contains("PAGE 5", result.Text);
    }

    [Fact]
    public async Task RateLimitRetriesTheSameBatchWithAFreshReadableStream()
    {
        var reader = new TestOcr((_, attempt) => attempt == 1 ? throw new RequestFailedException(429, "rate limit") : null);
        var result = await Extract(CreatePdf(3), reader);
        Assert.True(result.IsComplete);
        Assert.Equal(2, result.Batches[0].Attempts);
        Assert.Equal(1, result.Batches[1].Attempts);
        Assert.Equal(3, reader.Calls);
    }

    [Fact]
    public async Task MissingOrDuplicateOcrPagesNeverCountAsComplete()
    {
        var reader = new TestOcr((_, _) => new("partial", new[] { 1, 1 }, Array.Empty<FinancialBatchTable>()));
        var result = await Extract(CreatePdf(2), reader);
        Assert.False(result.IsComplete);
        Assert.Equal(0, result.PagesRead);
        Assert.Equal(3, result.Batches.Single().Attempts);
        Assert.Empty(result.Fields);
    }

    [Fact]
    public async Task ConnectionResetIsRetriedRatherThanEscapingAsAnUnhandledError()
    {
        var reader = new TestOcr((_, attempt) => attempt == 1 ? throw new IOException("Connection reset by peer") : null);
        var result = await Extract(CreatePdf(2), reader);
        Assert.True(result.IsComplete);
        Assert.Equal(2, result.Batches.Single().Attempts);
    }

    [Fact]
    public async Task PersistentTransportFailureStopsRemainingBatches()
    {
        var reader = new TestOcr((_, _) => throw new IOException("Connection reset by peer"));
        var result = await Extract(CreatePdf(5), reader);
        Assert.False(result.IsComplete);
        Assert.Equal(3, reader.Calls);
        Assert.Equal(new[] { 3, 0, 0 }, result.Batches.Select(batch => batch.Attempts));
        Assert.Contains("connection", result.Batches.First().Error!.ToLowerInvariant());
    }

    [Fact]
    public async Task CancellationStopsBeforeSendingMoreBatches()
    {
        using var cancellation = new CancellationTokenSource();
        var reader = new TestOcr((_, _) => { cancellation.Cancel(); return null; });
        using var stream = new MemoryStream(CreatePdf(5));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new UploadedFinancialDocumentService(new ConfigurationBuilder().Build(), reader)
                .ExtractAsync(stream, "bank", "test.pdf", cancellation.Token));
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public void InvalidAndOversizedPageCountPdfsAreRejected()
    {
        Assert.Throws<FinancialDocumentInputException>(() => FinancialPdfBatcher.Split("%PDF-invalid"u8.ToArray()));
        Assert.Throws<FinancialDocumentInputException>(() => FinancialPdfBatcher.Split(CreatePdf(3), maximumPages: 2));
    }

    [Fact]
    public void PasswordProtectedPdfIsRejectedBeforeOcr()
    {
        using var pdf = new PdfDocument();
        pdf.AddPage();
        pdf.SecuritySettings.UserPassword = "test-only-password";
        using var stream = new MemoryStream();
        pdf.Save(stream, closeStream: false);
        Assert.Throws<FinancialDocumentInputException>(() => FinancialPdfBatcher.Split(stream.ToArray()));
    }

    [Fact]
    public void LargeTwoPageBatchFallsBackToSinglePages()
    {
        var plan = FinancialPdfBatcher.Split(CreateLargePdf(2, 3 * 1024 * 1024));
        Assert.Equal(2, plan.Batches.Count);
        Assert.All(plan.Batches, batch =>
        {
            Assert.Equal(1, batch.PageCount);
            Assert.True(batch.Content.Length <= FinancialPdfBatcher.MaximumBatchBytes);
        });
    }

    [Fact]
    public void IndividualPageOverTheRequestLimitIsRejected()
    {
        Assert.Throws<FinancialDocumentInputException>(() => FinancialPdfBatcher.Split(CreateLargePdf(1, 5 * 1024 * 1024)));
    }

    private static byte[] CreateLargePdf(int pageCount, int contentBytes)
    {
        using var pdf = new PdfDocument();
        pdf.Options.CompressContentStreams = false;
        var random = new Random(343);
        for (var page = 0; page < pageCount; page++)
        {
            var bytes = new byte[contentBytes];
            random.NextBytes(bytes);
            var content = pdf.AddPage().Contents.AppendContent();
            if (content.Stream == null) content.CreateStream(bytes);
            else content.Stream.Value = bytes;
        }
        using var stream = new MemoryStream();
        pdf.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static async Task<UploadedFinancialDocumentResult> Extract(byte[] pdf, IFinancialDocumentBatchOcr reader)
    {
        using var stream = new MemoryStream(pdf);
        return await new UploadedFinancialDocumentService(new ConfigurationBuilder().Build(), reader)
            .ExtractAsync(stream, "bank", "test.pdf", CancellationToken.None);
    }

    private static byte[] CreatePdf(int pages)
    {
        using var pdf = new PdfDocument();
        for (var index = 1; index <= pages; index++) pdf.AddPage().Width = PdfSharp.Drawing.XUnit.FromPoint(100 + index);
        using var stream = new MemoryStream();
        pdf.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private sealed class TestOcr(Func<int, int, FinancialBatchRead?>? behavior = null) : IFinancialDocumentBatchOcr
    {
        public int Calls { get; private set; }
        public Task<FinancialBatchRead> ReadAsync(Stream stream, CancellationToken cancellationToken)
        {
            Calls++;
            using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
            var first = (int)pdf.Pages[0].Width.Point - 100;
            var replacement = behavior?.Invoke(first, Calls);
            if (replacement != null) return Task.FromResult(replacement);
            var pages = Enumerable.Range(1, pdf.PageCount).ToArray();
            var text = string.Join("", pages.Select(page => $"PAGE {first + page - 1}\n"));
            if (first + pdf.PageCount - 1 is 5 or 13) text += "Average Monthly Income: 43210\n";
            var tables = pages.Select(page => new FinancialBatchTable(new[] { new[] { $"Row {first + page - 1}" } }, new[] { page })).ToArray();
            return Task.FromResult(new FinancialBatchRead(text, pages, tables));
        }
    }
}
