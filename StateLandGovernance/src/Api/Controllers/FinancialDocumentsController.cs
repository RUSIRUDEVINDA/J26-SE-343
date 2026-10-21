using Azure;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.LeaseFeasibility.Infrastructure.Services;

namespace StateLandGovernance.Api.Controllers;

[ApiController]
[Route("api/lease-feasibility/documents")]
public sealed class FinancialDocumentsController(IConfiguration configuration) : ControllerBase
{
    [HttpGet("limits")]
    public IActionResult Limits() => Ok(FinancialDocumentUploadValidator.Limits(configuration));

    [HttpPost("validate")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    public async Task<IActionResult> Validate([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length > FinancialDocumentUploadValidator.MaximumPdfBytes)
            return Problem(statusCode: 400, detail: "The file exceeds the 10 MB maximum. Compress it before selecting it again.");
        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        try
        {
            var result = FinancialDocumentUploadValidator.Validate(buffer.ToArray(), file.FileName,
                FinancialDocumentUploadValidator.Limits(configuration));
            Response.Headers.CacheControl = "no-store";
            return Ok(result);
        }
        catch (FinancialDocumentInputException ex) { return Problem(statusCode: 400, detail: ex.Message); }
    }

    [HttpPost("map")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public IActionResult Map(UploadedFinancialDocumentResult evidence)
    {
        if (evidence.Kind is not ("bank" or "salary" or "crib") || !evidence.IsComplete || evidence.TotalPages < 1 ||
            !evidence.PageNumbers.Order().SequenceEqual(Enumerable.Range(1, evidence.TotalPages)))
            return Problem(statusCode: 400, detail: "Complete extracted document evidence is required before mapping financial fields.");
        var mapped = FinancialTableFieldMapper.Enrich(UploadedFinancialDocumentService.MapFields(evidence.Kind, evidence.Text), evidence.Tables);
        Response.Headers.CacheControl = "no-store";
        return Ok(evidence with { Fields = mapped.Fields, FieldSources = mapped.FieldSources, ReviewReasons = mapped.ReviewReasons });
    }

    [HttpPost("extract")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    public async Task<IActionResult> Extract([FromForm] IFormFile file, [FromForm] string kind, CancellationToken cancellationToken)
    {
        if (kind is not ("bank" or "salary" or "crib"))
            return Problem(statusCode: 400, detail: "Choose bank, salary or crib document type.");
        if (file.Length is <= 0 or > 10 * 1024 * 1024)
            return Problem(statusCode: 400, detail: "Choose a non-empty document no larger than 10 MB.");
        await using var stream = file.OpenReadStream();
        var header = new byte[8];
        var read = await stream.ReadAsync(header.AsMemory(), cancellationToken);
        var pdf = read >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8);
        var png = read == 8 && header.AsSpan().SequenceEqual(new byte[] {137, 80, 78, 71, 13, 10, 26, 10});
        var jpeg = read >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255;
        if (!pdf && !png && !jpeg) return Problem(statusCode: 400, detail: "The file must contain a PDF, PNG or JPEG document.");
        // Reopen rather than relying on the upload stream being seekable.
        await using var document = file.OpenReadStream();
        try
        {
            using var buffer = new MemoryStream();
            await document.CopyToAsync(buffer, cancellationToken);
            FinancialDocumentUploadValidator.Validate(buffer.ToArray(), file.FileName, FinancialDocumentUploadValidator.Limits(configuration));
            buffer.Position = 0;
            var result = await new UploadedFinancialDocumentService(configuration)
                .ExtractAsync(buffer, kind, Path.GetFileName(file.FileName), cancellationToken);
            Response.Headers.CacheControl = "no-store";
            return Ok(result);
        }
        catch (FinancialDocumentInputException ex) { return Problem(statusCode: 400, detail: ex.Message); }
        catch (RequestFailedException)
        {
            return Problem(statusCode: 502, detail: "Document OCR failed. Check Azure credentials, service availability, page limits and document readability. No sample data was substituted.");
        }
        catch (InvalidOperationException ex) { return Problem(statusCode: 503, detail: ex.Message); }
    }
}
