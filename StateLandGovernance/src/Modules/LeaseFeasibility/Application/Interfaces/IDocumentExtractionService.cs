using System;
using System.Threading;
using System.Threading.Tasks;

namespace StateLandGovernance.LeaseFeasibility.Application.Interfaces;

/// <summary>
/// Application-layer interface for extracting structured data from financial documents.
/// </summary>
public interface IDocumentExtractionService
{
    /// <summary>
    /// Extracts text from a document URI.
    /// </summary>
    Task<string> ExtractTextAsync(string documentUri, CancellationToken cancellationToken = default);
}
