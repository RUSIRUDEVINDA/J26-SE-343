using System.Collections.Generic;
using StateLandGovernance.LeaseFeasibility.Application.Commands;
using StateLandGovernance.LeaseFeasibility.Application.Interfaces;

namespace StateLandGovernance.LeaseFeasibility.Application.Validators;

public sealed class AssessFinancialFeasibilityCommandValidator : IRequestValidator<AssessFinancialFeasibilityCommand>
{
    public ValidationResult Validate(AssessFinancialFeasibilityCommand request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.ApplicationId))
        {
            errors.Add("Application ID is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ApplicantId))
        {
            errors.Add("Applicant ID is required.");
        }

        if (request.RequestedMonthlyLeasePaymentLkr <= 0m)
        {
            errors.Add("Requested monthly lease payment must be greater than zero LKR.");
        }

        if (request.MonthlyDebtObligationsLkr < 0m)
        {
            errors.Add("Monthly debt obligations cannot be negative.");
        }

        if (request.IncomeConsistencyRatio is < 0m or > 1m)
        {
            errors.Add("Income consistency ratio must be between 0 and 1.");
        }

        if (string.IsNullOrWhiteSpace(request.BankStatementUri))
        {
            errors.Add("Bank Statement URI is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SalarySlipUri))
        {
            errors.Add("Salary Slip URI is required.");
        }

        if (string.IsNullOrWhiteSpace(request.CribReportUri))
        {
            errors.Add("CRIB Report URI is required.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
