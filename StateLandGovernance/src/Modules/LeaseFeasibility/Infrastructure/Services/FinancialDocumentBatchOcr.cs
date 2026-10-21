using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Configuration;

namespace StateLandGovernance.LeaseFeasibility.Infrastructure.Services;

public sealed record FinancialBatchTable(string[][] Rows, IReadOnlyList<int> Pages);
public sealed record FinancialBatchRead(string Text, IReadOnlyList<int> Pages, IReadOnlyList<FinancialBatchTable> Tables);
public interface IFinancialDocumentBatchOcr
{
    Task<FinancialBatchRead> ReadAsync(Stream stream, CancellationToken cancellationToken);
}

public sealed class AzureFinancialDocumentBatchOcr : IFinancialDocumentBatchOcr
{
    private readonly DocumentAnalysisClient _client;
    private static readonly SemaphoreSlim ResourceGate = new(1, 1);
    private static DateTimeOffset _nextSubmission;

    public AzureFinancialDocumentBatchOcr(IConfiguration configuration)
    {
        var endpoint = configuration["Azure:DocumentIntelligence:Endpoint"];
        var key = configuration["Azure:DocumentIntelligence:Key"];
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.Host == "dummy.cognitiveservices.azure.com" || string.IsNullOrWhiteSpace(key) || key == "dummy-key")
            throw new InvalidOperationException("Document OCR is not configured. Set Azure:DocumentIntelligence:Endpoint and Key on the API server.");
        var options = new DocumentAnalysisClientOptions();
        // The upload orchestrator retries entire batches, with a fresh stream per attempt.
        options.Retry.MaxRetries = 0;
        _client = new(uri, new AzureKeyCredential(key), options);
    }

    public async Task<FinancialBatchRead> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        await ResourceGate.WaitAsync(cancellationToken);
        try
        {
            var wait = _nextSubmission - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, cancellationToken);
            var operation = await _client.AnalyzeDocumentAsync(WaitUntil.Started, "prebuilt-document", stream,
                cancellationToken: cancellationToken);
            // Poll the same accepted operation. Never resubmit a document because a GET was throttled.
            while (!operation.HasCompleted)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                for (var attempt = 1; ; attempt++)
                {
                    try { await operation.UpdateStatusAsync(cancellationToken); break; }
                    catch (RequestFailedException ex) when (ex.Status is 0 or 408 or 429 or >= 500)
                    {
                        var decision = FinancialOcrRetryPolicy.Evaluate(ex, attempt);
                        if (decision.StopDocument) throw;
                        if (attempt >= 3) throw new FinancialOcrOperationException("The accepted OCR operation could not be checked. " + decision.Message);
                        await Task.Delay(decision.Delay, cancellationToken);
                    }
                    catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is (HttpRequestException or IOException or TaskCanceledException))
                    {
                        if (attempt >= 3) throw new FinancialOcrOperationException("The accepted OCR operation could not be checked because the service is unreachable.");
                        await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken);
                    }
                }
            }
            var result = operation.Value;
            var tables = result.Tables.Select(table =>
            {
                var rows = Enumerable.Range(0, table.RowCount)
                    .Select(_ => Enumerable.Repeat("", table.ColumnCount).ToArray()).ToArray();
                foreach (var cell in table.Cells) rows[cell.RowIndex][cell.ColumnIndex] = cell.Content;
                return new FinancialBatchTable(rows, table.BoundingRegions.Select(region => region.PageNumber).Distinct().ToArray());
            }).ToArray();
            return new(result.Content, result.Pages.Select(page => page.PageNumber).ToArray(), tables);
        }
        finally
        {
            _nextSubmission = DateTimeOffset.UtcNow.AddSeconds(2);
            ResourceGate.Release();
        }
    }
}
