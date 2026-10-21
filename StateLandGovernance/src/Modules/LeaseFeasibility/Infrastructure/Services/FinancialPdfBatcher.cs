using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace StateLandGovernance.LeaseFeasibility.Infrastructure.Services;

public sealed class FinancialDocumentInputException(string message) : Exception(message);
public sealed record FinancialDocumentBatch(int StartPage, int PageCount, byte[] Content)
{
    public int EndPage => StartPage + PageCount - 1;
}
public sealed record FinancialDocumentBatchPlan(int TotalPages, IReadOnlyList<FinancialDocumentBatch> Batches);

public static class FinancialPdfBatcher
{
    public const int MaximumBatchBytes = 4 * 1024 * 1024;

    public static FinancialDocumentBatchPlan Split(byte[] content, int maximumPages = 200)
    {
        try
        {
            using var source = new MemoryStream(content, writable: false);
            using var pdf = PdfReader.Open(source, PdfDocumentOpenMode.Import);
            if (pdf.PageCount == 0)
                throw new FinancialDocumentInputException("The PDF has no pages. Choose a document containing at least one page.");
            if (pdf.PageCount > maximumPages)
                throw new FinancialDocumentInputException($"This PDF has {pdf.PageCount} pages; the limit is {maximumPages} pages per file. Split it into smaller PDFs before selecting it again.");
            var batches = new List<FinancialDocumentBatch>();
            for (var index = 0; index < pdf.PageCount;)
            {
                var count = Math.Min(2, pdf.PageCount - index);
                var bytes = WritePages(pdf, index, count);
                // Keep each request within F0's file-size limit as well as its page limit.
                if (bytes.Length > MaximumBatchBytes && count == 2)
                {
                    count = 1;
                    bytes = WritePages(pdf, index, count);
                }
                if (bytes.Length > MaximumBatchBytes)
                    throw new FinancialDocumentInputException($"Page {index + 1} exceeds the 4 MB OCR request limit. Compress the PDF and upload it again.");
                batches.Add(new(index + 1, count, bytes));
                index += count;
            }
            return new(pdf.PageCount, batches);
        }
        catch (Exception ex) when (ex is PdfReaderException or InvalidOperationException or FormatException or EndOfStreamException)
        {
            throw new FinancialDocumentInputException("The PDF cannot be read. Upload a valid, unlocked PDF without password protection.");
        }
    }

    private static byte[] WritePages(PdfDocument source, int start, int count)
    {
        using var batch = new PdfDocument();
        for (var index = start; index < start + count; index++) batch.AddPage(source.Pages[index]);
        using var output = new MemoryStream();
        batch.Save(output, closeStream: false);
        return output.ToArray();
    }
}
