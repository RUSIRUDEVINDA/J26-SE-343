using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.LeaseFeasibility.Application.DTOs;

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

    /// <summary>
    /// Extracts structured financial fields from a bank statement document.
    /// </summary>
    Task<BankStatementDataDto> ExtractBankStatementDataAsync(string documentUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts structured employment and income fields from a salary slip document.
    /// </summary>
    Task<SalarySlipDataDto> ExtractSalarySlipDataAsync(string documentUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts structured credit risk fields from a CRIB report document.
    /// </summary>
    Task<CribReportDataDto> ExtractCribReportDataAsync(string documentUri, CancellationToken cancellationToken = default);
}
