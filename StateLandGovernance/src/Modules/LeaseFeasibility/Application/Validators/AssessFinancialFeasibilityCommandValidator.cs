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

        if (request.Input is null)
        {
            errors.Add("Financial profile input cannot be null.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Input.ApplicantId))
            {
                errors.Add("Applicant ID is required.");
            }

            if (request.Input.AverageMonthlyIncome < 0)
            {
                errors.Add("Average monthly income cannot be negative.");
            }

            if (request.Input.IncomeConsistencyScore is < 0 or > 1)
            {
                errors.Add("Income consistency score must be between 0 and 1.");
            }

            if (request.Input.EmploymentTenureMonths < 0)
            {
                errors.Add("Employment tenure months cannot be negative.");
            }
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
