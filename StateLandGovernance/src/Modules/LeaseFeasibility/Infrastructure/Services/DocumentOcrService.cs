using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Configuration;
using StateLandGovernance.LeaseFeasibility.Application.Interfaces;

namespace StateLandGovernance.LeaseFeasibility.Infrastructure.Services;

/// <summary>
/// Infrastructure service for OCR document extraction using Azure Document Intelligence (Form Recognizer).
/// </summary>
public sealed class DocumentOcrService : IDocumentExtractionService
{
    private readonly DocumentAnalysisClient _client;
    private readonly string _endpointHost;

    public DocumentOcrService(IConfiguration configuration)
    {
        var endpoint = configuration["Azure:DocumentIntelligence:Endpoint"] ?? "https://dummy.cognitiveservices.azure.com/";
        var key = configuration["Azure:DocumentIntelligence:Key"] ?? "dummy-key";
        
        _endpointHost = new Uri(endpoint).Host;
        _client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(key));
    }

    public async Task<string> ExtractTextAsync(string documentUri, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentUri))
        {
            throw new ArgumentException("Document URI cannot be empty.", nameof(documentUri));
        }

        // Mock mode for local testing if dummy keys are used or if the file is mock-statement.txt
        if (documentUri.Contains("mock-statement.txt") || _endpointHost == "dummy.cognitiveservices.azure.com")
        {
            return "MOCK OCR RESULT: [BANK STATEMENT] Average Monthly Income: 5000. Loan Obligation: 200. Verification: SUCCESS.";
        }

        try
        {
            // In a real scenario, this could be a URL or a local file path.
            if (Uri.TryCreate(documentUri, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                var operation = await _client.AnalyzeDocumentFromUriAsync(WaitUntil.Completed, "prebuilt-document", uri, null, cancellationToken);
                return operation.Value.Content;
            }
            else
            {
                using var stream = File.OpenRead(documentUri);
                var operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-document", stream, null, cancellationToken);
                return operation.Value.Content;
            }
        }
        catch (RequestFailedException ex)
        {
            throw new InvalidOperationException($"Failed to extract text from document using OCR: {ex.Message}", ex);
        }
    }
}
