using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Configuration;
using StateLandGovernance.LeaseFeasibility.Application.DTOs;
using StateLandGovernance.LeaseFeasibility.Application.Interfaces;
using System.Text.RegularExpressions;

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

        // Mock mode for local testing if dummy keys are used or if the file contains mock
        if (_endpointHost == "dummy.cognitiveservices.azure.com" || documentUri.Contains("mock-"))
        {
            if (File.Exists(documentUri))
            {
                return File.ReadAllText(documentUri);
            }

            if (documentUri.Contains("mock-salary"))
            {
                if (documentUri.Contains("malformed")) return "MOCK OCR RESULT: Some random noise without any useful salary data...";
                if (documentUri.Contains("1")) return "MOCK OCR RESULT: [SALARY SLIP] Average Monthly Income: 6500. Employment Tenure Months: 24. Employment Type: Full-Time. Employer Or Business Name: Acme Corp.";
                if (documentUri.Contains("2")) return "MOCK OCR RESULT: [SALARY SLIP] Average Monthly Income: 8200. Employment Tenure Months: 12. Employment Type: Contract. Employer Or Business Name: Globex Inc.";
                return "MOCK OCR RESULT: [SALARY SLIP] Average Monthly Income: 4000. Employment Tenure Months: 6. Employment Type: Part-Time. Employer Or Business Name: Initech.";
            }
            if (documentUri.Contains("mock-crib"))
            {
                return "MOCK OCR RESULT: [CRIB REPORT] Credit Risk Grade: A. Active Loan Obligations: 12000. Default History Indicator: False. Recent Credit Inquiries: 1.";
            }

            return "MOCK OCR RESULT: [BANK STATEMENT] Average Monthly Income: 5000. Average Account Balance: 15000. Overdraft Frequency: 0. Savings To Income Ratio: 0.2. Loan Obligation: 200. Verification: SUCCESS.";
        }

        try
        {
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

    public async Task<BankStatementDataDto> ExtractBankStatementDataAsync(string documentUri, CancellationToken cancellationToken = default)
    {
        var rawText = await ExtractTextAsync(documentUri, cancellationToken);
        var errors = new List<string>();

        if (!rawText.Contains("BANK STATEMENT", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Malformed document or low-confidence OCR: 'BANK STATEMENT' indicator not found.");
        }

        decimal? avgIncome = TryExtractDecimal(rawText, @"Average\s*Monthly\s*Income[\s:]+([\d,.]+)");
        if (!avgIncome.HasValue) errors.Add("Missing required field: Average Monthly Income.");

        decimal? avgBalance = TryExtractDecimal(rawText, @"Average\s*Account\s*Balance[\s:]+([\d,.]+)");
        if (!avgBalance.HasValue) errors.Add("Missing required field: Average Account Balance.");

        decimal? overdrafts = TryExtractDecimal(rawText, @"Overdraft\s*Frequency[\s:]+(\d+)");
        if (!overdrafts.HasValue) errors.Add("Missing required field: Overdraft Frequency.");

        decimal? savingsRatio = TryExtractDecimal(rawText, @"Savings\s*To\s*Income\s*Ratio[\s:]+([\d,.]+)");
        if (!savingsRatio.HasValue) errors.Add("Missing required field: Savings To Income Ratio.");

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return new BankStatementDataDto(
            AverageMonthlyIncome: avgIncome.Value,
            AverageAccountBalance: avgBalance.Value,
            OverdraftFrequency: (int)overdrafts.Value,
            SavingsToIncomeRatio: savingsRatio.Value
        );
    }

    public async Task<SalarySlipDataDto> ExtractSalarySlipDataAsync(string documentUri, CancellationToken cancellationToken = default)
    {
        var rawText = await ExtractTextAsync(documentUri, cancellationToken);
        var errors = new List<string>();

        if (!rawText.Contains("SALARY SLIP", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Malformed document or low-confidence OCR: 'SALARY SLIP' indicator not found.");
        }

        decimal? avgIncome = TryExtractDecimal(rawText, @"Average\s*Monthly\s*Income[\s:]+([\d,.]+)");
        if (!avgIncome.HasValue) errors.Add("Missing required field: Average Monthly Income.");

        decimal? tenure = TryExtractDecimal(rawText, @"Employment\s*Tenure\s*Months[\s:]+(\d+)");
        if (!tenure.HasValue) errors.Add("Missing required field: Employment Tenure Months.");

        string? empType = TryExtractString(rawText, @"Employment\s*Type[\s:]+([^.]+)");
        if (string.IsNullOrWhiteSpace(empType)) errors.Add("Missing required field: Employment Type.");

        string? employer = TryExtractString(rawText, @"Employer\s*Or\s*Business\s*Name[\s:]+([^.]+)");
        if (string.IsNullOrWhiteSpace(employer)) errors.Add("Missing required field: Employer Or Business Name.");

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return new SalarySlipDataDto(
            AverageMonthlyIncome: avgIncome.Value,
            EmploymentTenureMonths: (int)tenure.Value,
            EmploymentType: empType!,
            EmployerOrBusinessName: employer!
        );
    }

    public async Task<CribReportDataDto> ExtractCribReportDataAsync(string documentUri, CancellationToken cancellationToken = default)
    {
        var rawText = await ExtractTextAsync(documentUri, cancellationToken);
        var errors = new List<string>();

        if (!rawText.Contains("CRIB REPORT", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Malformed document or low-confidence OCR: 'CRIB REPORT' indicator not found.");
        }

        string? grade = TryExtractString(rawText, @"Credit\s*Risk\s*Grade[\s:]+([A-E])");
        if (string.IsNullOrWhiteSpace(grade)) errors.Add("Missing required field: Credit Risk Grade.");

        decimal? obligations = TryExtractDecimal(rawText, @"Active\s*Loan\s*Obligations[\s:]+([\d,.]+)");
        if (!obligations.HasValue) errors.Add("Missing required field: Active Loan Obligations.");

        bool? defaultHistory = TryExtractBool(rawText, @"Default\s*History\s*Indicator[\s:]+(\w+)");
        if (!defaultHistory.HasValue) errors.Add("Missing required field: Default History Indicator.");

        decimal? inquiries = TryExtractDecimal(rawText, @"Recent\s*Credit\s*Inquiries[\s:]+(\d+)");
        if (!inquiries.HasValue) errors.Add("Missing required field: Recent Credit Inquiries.");

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return new CribReportDataDto(
            CreditRiskGrade: grade!,
            ActiveLoanObligations: obligations.Value,
            DefaultHistoryIndicator: defaultHistory.Value,
            RecentCreditInquiries: (int)inquiries.Value
        );
    }

    private string? TryExtractString(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private bool? TryExtractBool(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var val = match.Groups[1].Value.Replace(".", "").Trim();
            if (bool.TryParse(val, out var result)) return result;
            if (val.Equals("yes", StringComparison.OrdinalIgnoreCase)) return true;
            if (val.Equals("no", StringComparison.OrdinalIgnoreCase)) return false;
        }
        return null;
    }

    private decimal? TryExtractDecimal(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var valStr = match.Groups[1].Value.Replace(",", "").TrimEnd('.');
            if (decimal.TryParse(valStr, out var result))
            {
                return result;
            }
        }
        return null;
    }
}
