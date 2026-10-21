using System.Globalization;
using System.Text.RegularExpressions;
using Azure;
using Microsoft.Extensions.Configuration;
using StateLandGovernance.LeaseFeasibility.Application.Interfaces;

namespace StateLandGovernance.LeaseFeasibility.Infrastructure.Services;

public sealed record FinancialBatchStatus(int StartPage, int EndPage, bool Complete, int Attempts, string? Error);
public sealed record UploadedFinancialDocumentResult(
    string Kind, string FileName, string Text, int PagesRead,
    IReadOnlyList<string[][]> Tables, IReadOnlyDictionary<string, string> Fields,
    IReadOnlyList<string> ReviewReasons)
{
    public int TotalPages { get; init; }
    public bool IsComplete { get; init; }
    public IReadOnlyList<int> PageNumbers { get; init; } = Array.Empty<int>();
    public IReadOnlyList<IReadOnlyList<int>> TablePageNumbers { get; init; } = Array.Empty<IReadOnlyList<int>>();
    public IReadOnlyList<FinancialBatchStatus> Batches { get; init; } = Array.Empty<FinancialBatchStatus>();
    public IReadOnlyDictionary<string, string> FieldSources { get; init; } = new Dictionary<string, string>();
}

/// <summary>Upload-only OCR path. Never invokes the synthetic URI test fixtures.</summary>
public sealed class UploadedFinancialDocumentService(IConfiguration configuration, IFinancialDocumentBatchOcr? batchOcr = null)
{
    public async Task<UploadedFinancialDocumentResult> ExtractAsync(
        Stream stream, string kind, string fileName, CancellationToken cancellationToken)
    {
        var reader = batchOcr ?? new AzureFinancialDocumentBatchOcr(configuration);
        using var input = new MemoryStream();
        await stream.CopyToAsync(input, cancellationToken);
        var bytes = input.ToArray();
        var pdf = bytes.AsSpan().StartsWith("%PDF-"u8);
        var maximumPages = FinancialDocumentUploadValidator.Limits(configuration).MaximumPdfPages;
        var plan = pdf ? FinancialPdfBatcher.Split(bytes, maximumPages)
            : new FinancialDocumentBatchPlan(1, new[] { new FinancialDocumentBatch(1, 1, bytes) });
        if (!pdf && bytes.Length > FinancialPdfBatcher.MaximumBatchBytes)
            throw new FinancialDocumentInputException("The image exceeds the 4 MB OCR request limit. Compress it and upload it again.");
        var texts = new List<string>();
        var pages = new List<int>();
        var tables = new List<string[][]>();
        var tablePages = new List<IReadOnlyList<int>>();
        var statuses = new List<FinancialBatchStatus>();
        string? stoppedReason = null;
        foreach (var batch in plan.Batches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stoppedReason != null)
            {
                statuses.Add(new(batch.StartPage, batch.EndPage, false, 0, "Not attempted: " + stoppedReason));
                continue;
            }
            FinancialBatchRead? result = null;
            string? error = null;
            var attempts = 0;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                attempts = attempt;
                var retryDelay = TimeSpan.FromSeconds(attempt);
                try
                {
                    using var batchStream = new MemoryStream(batch.Content, writable: false);
                    result = await reader.ReadAsync(batchStream, cancellationToken);
                    if (!result.Pages.Order().SequenceEqual(Enumerable.Range(1, batch.PageCount)))
                    {
                        result = null;
                        error = "OCR did not return every page in this batch.";
                    }
                    else break;
                }
                catch (RequestFailedException ex)
                {
                    var decision = FinancialOcrRetryPolicy.Evaluate(ex, attempt);
                    error = decision.Message;
                    retryDelay = decision.Delay;
                    if (decision.StopDocument || (ex.Status is 0 or 429 && attempt == 3)) stoppedReason = error;
                    if (!decision.CanRetry) break;
                }
                catch (FinancialOcrOperationException ex) { error = ex.Message; stoppedReason = error; break; }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is (HttpRequestException or IOException or TaskCanceledException))
                {
                    error = ex is TaskCanceledException ? "The OCR request timed out."
                        : "The connection to Azure OCR was interrupted. Retry after checking network connectivity.";
                    if (attempt == 3) stoppedReason = error;
                }
                if (attempt < 3) await Task.Delay(retryDelay, cancellationToken);
            }
            statuses.Add(new(batch.StartPage, batch.EndPage, result != null, attempts, result == null ? error : null));
            if (result == null) continue;
            texts.Add(result.Text);
            pages.AddRange(result.Pages.Select(page => batch.StartPage + page - 1));
            foreach (var table in result.Tables)
            {
                tables.Add(table.Rows);
                tablePages.Add(table.Pages.Select(page => batch.StartPage + page - 1).ToArray());
            }
        }
        var complete = pages.Order().SequenceEqual(Enumerable.Range(1, plan.TotalPages));
        var text = string.Join("\n\n", texts);
        // Partial evidence is viewable, but never auto-populates a financial profile.
        var mapped = complete ? FinancialTableFieldMapper.Enrich(MapFields(kind, text), tables) : MapFields(kind, "");
        var reasons = mapped.ReviewReasons.ToList();
        if (!complete) reasons.Add("Extraction is incomplete. Retry processing the document before confirming fields or calculating an assessment.");
        return mapped with { FileName = fileName, Text = text, PagesRead = pages.Count, TotalPages = plan.TotalPages,
            IsComplete = complete, PageNumbers = pages, Tables = tables, TablePageNumbers = tablePages,
            Batches = statuses, ReviewReasons = reasons };
    }

    public static UploadedFinancialDocumentResult MapFields(string kind, string text)
    {
        var fields = new Dictionary<string, string>();
        var reasons = new List<string>();
        void Read(string name, string label)
        {
            var matches = Regex.Matches(text, label + @"(?:\s*\(LKR\))?[\s:]+(?:LKR\s*)?([0-9]+(?:,[0-9]{3})*(?:\.[0-9]+)?)(?![0-9,])", RegexOptions.IgnoreCase);
            if (matches.Count == 1 && decimal.TryParse(matches[0].Groups[1].Value.Replace(",", ""),
                NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                fields[name] = value.ToString(CultureInfo.InvariantCulture);
        }
        if (kind == "crib")
        {
            try
            {
                var crib = CribReportParser.Parse(text);
                fields["grade"] = crib.CreditRiskGrade;
                if (crib.DefaultHistoryIndicator.HasValue)
                    fields["defaultHistory"] = crib.DefaultHistoryIndicator.Value ? "true" : "false";
                if (crib.ReportedMonthlyPaymentsLkr.HasValue)
                    fields["debt"] = crib.ReportedMonthlyPaymentsLkr.Value.ToString(CultureInfo.InvariantCulture);
                reasons.AddRange(crib.ReviewReasons);
            }
            catch (ValidationException) { reasons.Add("CRIB fields could not be reliably mapped. Review the extracted report and enter verified values."); }
        }
        else
        {
            Read("income", @"Average\s+Monthly\s+Income");
            if (kind == "bank")
            {
                Read("balance", @"Average\s+Account\s+Balance");
                Read("consistency", @"Income\s+Consistency\s+(?:Ratio|Score)");
                Read("overdrafts", @"Overdraft\s+Count\s+(?:Last\s+6\s+Months|In\s+Evidence\s+Window)");
                reasons.Add("Review transaction tables and the evidence period. Credits may include transfers or loans; they are not automatically income.");
            }
            if (!fields.ContainsKey("income")) reasons.Add("Monthly income was not explicitly identified. Verify recurring income against the document before entering it.");
        }
        if (string.IsNullOrWhiteSpace(text)) reasons.Add("No readable text was returned. Replace the document with a readable copy.");
        return new(kind, "", text, 0, Array.Empty<string[][]>(), fields, reasons);
    }
}
