using Microsoft.Extensions.Configuration;

namespace StateLandGovernance.LeaseFeasibility.Infrastructure.Services;

public sealed record FinancialUploadLimits(int MaximumPdfPages, int MaximumPdfBytes, int MaximumImageBytes);
public sealed record FinancialUploadValidation(string FileName, string Format, int Pages, int Batches, long Bytes);

public static class FinancialDocumentUploadValidator
{
    public const int MaximumPdfBytes = 10 * 1024 * 1024;

    public static FinancialUploadLimits Limits(IConfiguration configuration) => new(
        int.TryParse(configuration["Azure:DocumentIntelligence:MaximumUploadPages"], out var limit) && limit > 0 ? limit : 200,
        MaximumPdfBytes, FinancialPdfBatcher.MaximumBatchBytes);

    public static FinancialUploadValidation Validate(byte[] bytes, string fileName, FinancialUploadLimits limits)
    {
        if (bytes.Length == 0) throw new FinancialDocumentInputException("The file is empty. Choose a readable document.");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var pdf = bytes.AsSpan().StartsWith("%PDF-"u8);
        var png = bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var jpeg = bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255;
        if (!(pdf && extension == ".pdf") && !(png && extension == ".png") && !(jpeg && extension is ".jpg" or ".jpeg"))
            throw new FinancialDocumentInputException("Choose a PDF, PNG or JPEG whose file contents match its extension.");
        var maximumBytes = pdf ? limits.MaximumPdfBytes : limits.MaximumImageBytes;
        if (bytes.Length > maximumBytes)
            throw new FinancialDocumentInputException($"{(pdf ? "PDF" : "Image")} exceeds the {maximumBytes / (1024 * 1024)} MB limit. Compress the file before selecting it again.");
        if (!pdf) return new(Path.GetFileName(fileName), png ? "PNG" : "JPEG", 1, 1, bytes.Length);
        var plan = FinancialPdfBatcher.Split(bytes, limits.MaximumPdfPages);
        return new(Path.GetFileName(fileName), "PDF", plan.TotalPages, plan.Batches.Count, bytes.Length);
    }
}
