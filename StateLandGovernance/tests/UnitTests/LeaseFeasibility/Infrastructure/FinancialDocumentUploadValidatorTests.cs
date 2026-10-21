using Microsoft.Extensions.Configuration;
using PdfSharp.Pdf;
using StateLandGovernance.LeaseFeasibility.Infrastructure.Services;

namespace StateLandGovernance.UnitTests.LeaseFeasibility.Infrastructure;

public sealed class FinancialDocumentUploadValidatorTests
{
    private static readonly FinancialUploadLimits Limits = FinancialDocumentUploadValidator.Limits(new ConfigurationBuilder().Build());

    [Theory]
    [InlineData(1, 1)]
    [InlineData(13, 7)]
    [InlineData(200, 100)]
    public void ValidPdfReportsPageAndBatchCounts(int pages, int batches)
    {
        var result = FinancialDocumentUploadValidator.Validate(Pdf(pages), "document.pdf", Limits);
        Assert.Equal(pages, result.Pages);
        Assert.Equal(batches, result.Batches);
    }

    [Fact]
    public void PdfOverPageLimitIsRejected()
    {
        var error = Assert.Throws<FinancialDocumentInputException>(() => FinancialDocumentUploadValidator.Validate(Pdf(201), "document.pdf", Limits));
        Assert.Contains("201 pages", error.Message);
        Assert.Contains("200 pages", error.Message);
    }

    [Fact]
    public void EmptyCorruptAndDisguisedFilesAreRejected()
    {
        Assert.Throws<FinancialDocumentInputException>(() => FinancialDocumentUploadValidator.Validate([], "empty.pdf", Limits));
        Assert.Throws<FinancialDocumentInputException>(() => FinancialDocumentUploadValidator.Validate("%PDF-invalid"u8.ToArray(), "broken.pdf", Limits));
        Assert.Throws<FinancialDocumentInputException>(() => FinancialDocumentUploadValidator.Validate(Pdf(1), "disguised.jpg", Limits));
    }

    [Fact]
    public void ImagesAndPdfsHaveDifferentSizeLimits()
    {
        var image = new byte[4 * 1024 * 1024 + 1];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(image, 0);
        var error = Assert.Throws<FinancialDocumentInputException>(() => FinancialDocumentUploadValidator.Validate(image, "large.png", Limits));
        Assert.Contains("4 MB", error.Message);
        var pdf = new byte[10 * 1024 * 1024 + 1];
        "%PDF-"u8.CopyTo(pdf);
        error = Assert.Throws<FinancialDocumentInputException>(() => FinancialDocumentUploadValidator.Validate(pdf, "large.pdf", Limits));
        Assert.Contains("10 MB", error.Message);
    }

    [Fact]
    public void ConfiguredPageLimitIsUsedByValidation()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Azure:DocumentIntelligence:MaximumUploadPages"] = "12" }).Build();
        var limits = FinancialDocumentUploadValidator.Limits(configuration);
        Assert.Equal(12, limits.MaximumPdfPages);
        Assert.Throws<FinancialDocumentInputException>(() => FinancialDocumentUploadValidator.Validate(Pdf(13), "document.pdf", limits));
    }

    private static byte[] Pdf(int pages)
    {
        using var document = new PdfDocument();
        for (var index = 0; index < pages; index++) document.AddPage();
        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
    }
}
