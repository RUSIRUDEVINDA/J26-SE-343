using System;
using System.Text.Json;
using StateLandGovernance.LeaseFeasibility.Application.DTOs;
using StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;

namespace StateLandGovernance.LeaseFeasibility.Application.Utilities;

/// <summary>
/// Utility to mask PII (Personally Identifiable Information) before logging or persisting.
/// Follows the metadata-only convention for privacy protection.
/// </summary>
public static class PiiMasker
{
    public static string MaskName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        if (name.Length <= 2) return new string('*', name.Length);
        
        return $"{name[0]}{new string('*', name.Length - 2)}{name[^1]}";
    }

    public static string MaskId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return string.Empty;
        var parts = id.Split('-');
        if (parts.Length > 1)
        {
            return $"{parts[0]}-***";
        }
        
        return id.Length > 4 
            ? $"{id[..2]}***{id[^2..]}"
            : "***";
    }
    
    public static string MaskAccountNumber(string? account)
    {
        if (string.IsNullOrWhiteSpace(account)) return string.Empty;
        return account.Length > 4 
            ? new string('*', account.Length - 4) + account[^4..]
            : "***";
    }

    /// <summary>
    /// Masks a FinancialProfileDto for safe logging, ensuring no raw PII is exposed.
    /// Uses a metadata-only/masked approach.
    /// </summary>
    public static string GetMaskedLogPayload(FinancialProfileDto profile)
    {
        var maskedApplicantId = MaskId(profile.ApplicantId);
        var maskedEmployer = MaskName(profile.EmployerOrBusinessName);

        // Even though monetary values might not strictly be PII, we only log essential metadata 
        // and safely masked identifiers following the no-raw-payload discipline.
        var logObject = new
        {
            ApplicantId = maskedApplicantId,
            EmployerOrBusinessName = maskedEmployer,
            AverageMonthlyIncome = profile.AverageMonthlyIncome > 0 ? "[Provided]" : "[Missing]",
            CreditRiskGrade = profile.CreditRiskGrade,
            DataPointsEvaluated = 12,
            Status = "Processed"
        };

        return JsonSerializer.Serialize(logObject);
    }

    /// <summary>
    /// Produces a metadata-only log payload for the typed deterministic scoring input.
    /// </summary>
    public static string GetMaskedLogPayload(
        FinancialFeasibilityScoringInput input,
        string? employerOrBusinessName)
    {
        ArgumentNullException.ThrowIfNull(input);

        var logObject = new
        {
            ApplicationId = MaskId(input.ApplicationId),
            ApplicantId = MaskId(input.ApplicantId),
            EmployerOrBusinessName = MaskName(employerOrBusinessName),
            AverageMonthlyIncomeLkr = input.AverageMonthlyIncomeLkr > 0m ? "[Provided]" : "[Missing]",
            RequestedMonthlyLeasePaymentLkr = input.RequestedMonthlyLeasePaymentLkr > 0m ? "[Provided]" : "[Missing]",
            MonthlyDebtObligationsLkr = "[Provided]",
            CreditRiskGrade = input.CreditRiskGrade.ToString(),
            Status = "Processed"
        };

        return JsonSerializer.Serialize(logObject);
    }
}
