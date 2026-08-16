using System;
using StateLandGovernance.LeaseFeasibility.Domain.Enums;

namespace StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;

public sealed record IncomeProfile
{
    public string ApplicantId { get; }
    public decimal AverageMonthlyIncome { get; }
    public decimal IncomeConsistencyScore { get; }
    public int EmploymentTenureMonths { get; }
    public EmploymentType EmploymentType { get; }
    public string? EmployerOrBusinessName { get; }

    public IncomeProfile(
        string applicantId,
        decimal averageMonthlyIncome,
        decimal incomeConsistencyScore,
        int employmentTenureMonths,
        EmploymentType employmentType,
        string? employerOrBusinessName = null)
    {
        if (string.IsNullOrWhiteSpace(applicantId))
        {
            throw new ArgumentException("Applicant ID is required.", nameof(applicantId));
        }

        if (averageMonthlyIncome < 0)
        {
            throw new ArgumentException("Average monthly income cannot be negative.", nameof(averageMonthlyIncome));
        }

        if (incomeConsistencyScore < 0 || incomeConsistencyScore > 1)
        {
            throw new ArgumentException("Income consistency score must be between 0 and 1.", nameof(incomeConsistencyScore));
        }

        if (employmentTenureMonths < 0)
        {
            throw new ArgumentException("Employment tenure months cannot be negative.", nameof(employmentTenureMonths));
        }

        ApplicantId = applicantId.Trim();
        AverageMonthlyIncome = averageMonthlyIncome;
        IncomeConsistencyScore = incomeConsistencyScore;
        EmploymentTenureMonths = employmentTenureMonths;
        EmploymentType = employmentType;
        EmployerOrBusinessName = string.IsNullOrWhiteSpace(employerOrBusinessName) 
            ? null 
            : employerOrBusinessName.Trim();
    }
}
