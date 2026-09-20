using System;

namespace StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;

public sealed record FinancialProfile(
    string ApplicantId,
    decimal AverageMonthlyIncome,
    decimal IncomeConsistencyScore,
    int EmploymentTenureMonths,
    string EmploymentType,
    string? EmployerOrBusinessName,
    decimal AverageAccountBalance,
    int OverdraftFrequency,
    decimal SavingsToIncomeRatio,
    string CreditRiskGrade,
    decimal ActiveLoanObligations,
    bool DefaultHistoryIndicator,
    int RecentCreditInquiries
);
