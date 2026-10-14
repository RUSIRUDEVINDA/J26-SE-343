using System;
using StateLandGovernance.LeaseFeasibility.Domain.Enums;
using StateLandGovernance.LeaseFeasibility.Domain.Exceptions;

namespace StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;

/// <summary>
/// Typed, unit-explicit input to the deterministic Component 2 scorer.
/// Monetary values are monthly LKR amounts unless the property states otherwise.
/// </summary>
public sealed record FinancialFeasibilityScoringInput
{
    public string ApplicationId { get; }
    public string ApplicantId { get; }
    public decimal AverageMonthlyIncomeLkr { get; }
    public decimal IncomeConsistencyRatio { get; }
    public decimal RequestedMonthlyLeasePaymentLkr { get; }
    public decimal MonthlyDebtObligationsLkr { get; }
    public decimal AverageAccountBalanceLkr { get; }
    public int OverdraftCountInEvidenceWindow { get; }
    public CreditRiskGrade CreditRiskGrade { get; }
    public bool HasDefaultHistory { get; }

    public FinancialFeasibilityScoringInput(
        string applicationId,
        string applicantId,
        decimal averageMonthlyIncomeLkr,
        decimal incomeConsistencyRatio,
        decimal requestedMonthlyLeasePaymentLkr,
        decimal monthlyDebtObligationsLkr,
        decimal averageAccountBalanceLkr,
        int overdraftCountInEvidenceWindow,
        CreditRiskGrade creditRiskGrade,
        bool hasDefaultHistory)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            throw new InvalidFinancialProfileException(applicationId ?? string.Empty, "Application ID is required.");
        }

        if (string.IsNullOrWhiteSpace(applicantId))
        {
            throw new InvalidFinancialProfileException(applicationId, "Applicant ID is required.");
        }

        if (averageMonthlyIncomeLkr <= 0)
        {
            throw new InvalidFinancialProfileException(applicationId, "Average monthly income must be greater than zero LKR.");
        }

        if (incomeConsistencyRatio is < 0m or > 1m)
        {
            throw new InvalidFinancialProfileException(applicationId, "Income consistency ratio must be between 0 and 1.");
        }

        if (requestedMonthlyLeasePaymentLkr <= 0)
        {
            throw new InvalidFinancialProfileException(applicationId, "Requested monthly lease payment must be greater than zero LKR.");
        }

        if (monthlyDebtObligationsLkr < 0)
        {
            throw new InvalidFinancialProfileException(applicationId, "Monthly debt obligations cannot be negative.");
        }

        if (averageAccountBalanceLkr < 0)
        {
            throw new InvalidFinancialProfileException(applicationId, "Average account balance cannot be negative.");
        }

        if (overdraftCountInEvidenceWindow < 0)
        {
            throw new InvalidFinancialProfileException(applicationId, "Six-month overdraft count cannot be negative.");
        }

        if (!Enum.IsDefined(creditRiskGrade))
        {
            throw new InvalidFinancialProfileException(applicationId, "Credit risk grade must be A, B, C, D, or E.");
        }

        ApplicationId = applicationId.Trim();
        ApplicantId = applicantId.Trim();
        AverageMonthlyIncomeLkr = averageMonthlyIncomeLkr;
        IncomeConsistencyRatio = incomeConsistencyRatio;
        RequestedMonthlyLeasePaymentLkr = requestedMonthlyLeasePaymentLkr;
        MonthlyDebtObligationsLkr = monthlyDebtObligationsLkr;
        AverageAccountBalanceLkr = averageAccountBalanceLkr;
        OverdraftCountInEvidenceWindow = overdraftCountInEvidenceWindow;
        CreditRiskGrade = creditRiskGrade;
        HasDefaultHistory = hasDefaultHistory;
    }
}
