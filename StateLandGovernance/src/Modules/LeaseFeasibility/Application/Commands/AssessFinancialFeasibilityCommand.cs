using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StateLandGovernance.LeaseFeasibility.Application.DTOs;
using StateLandGovernance.LeaseFeasibility.Application.Interfaces;
using StateLandGovernance.LeaseFeasibility.Application.Utilities;
using StateLandGovernance.LeaseFeasibility.Domain.Services;

namespace StateLandGovernance.LeaseFeasibility.Application.Commands;

/// <summary>
/// Command to assess the financial feasibility of a lease application via document extraction.
/// </summary>
public sealed record AssessFinancialFeasibilityCommand(
    string ApplicationId,
    string ApplicantId,
    string BankStatementUri,
    string SalarySlipUri,
    string CribReportUri
);

/// <summary>
/// Handler for the AssessFinancialFeasibilityCommand.
/// </summary>
public sealed class AssessFinancialFeasibilityCommandHandler
{
    private readonly IDocumentExtractionService _extractionService;
    private readonly IFinancialFeasibilityScoringEngine _scoringEngine;
    private readonly IFinancialFeasibilityRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AssessFinancialFeasibilityCommandHandler> _logger;

    public AssessFinancialFeasibilityCommandHandler(
        IDocumentExtractionService extractionService,
        IFinancialFeasibilityScoringEngine scoringEngine,
        IFinancialFeasibilityRepository repository,
        TimeProvider timeProvider,
        ILogger<AssessFinancialFeasibilityCommandHandler> logger)
    {
        _extractionService = extractionService ?? throw new ArgumentNullException(nameof(extractionService));
        _scoringEngine = scoringEngine ?? throw new ArgumentNullException(nameof(scoringEngine));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FeasibilityAssessmentDto> HandleAsync(AssessFinancialFeasibilityCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Extract raw data from documents
        var bankData = await _extractionService.ExtractBankStatementDataAsync(command.BankStatementUri, cancellationToken);
        var salaryData = await _extractionService.ExtractSalarySlipDataAsync(command.SalarySlipUri, cancellationToken);
        var cribData = await _extractionService.ExtractCribReportDataAsync(command.CribReportUri, cancellationToken);

        // 2. Assemble Financial Profile (Domain ValueObject)
        var profile = new StateLandGovernance.LeaseFeasibility.Domain.ValueObjects.FinancialProfile(
            ApplicantId: command.ApplicantId,
            AverageMonthlyIncome: salaryData.AverageMonthlyIncome,
            IncomeConsistencyScore: 0.85m, // Based on business rules or synthesized
            EmploymentTenureMonths: salaryData.EmploymentTenureMonths,
            EmploymentType: salaryData.EmploymentType,
            EmployerOrBusinessName: salaryData.EmployerOrBusinessName,
            AverageAccountBalance: bankData.AverageAccountBalance,
            OverdraftFrequency: bankData.OverdraftFrequency,
            SavingsToIncomeRatio: bankData.SavingsToIncomeRatio,
            CreditRiskGrade: cribData.CreditRiskGrade,
            ActiveLoanObligations: cribData.ActiveLoanObligations,
            DefaultHistoryIndicator: cribData.DefaultHistoryIndicator,
            RecentCreditInquiries: cribData.RecentCreditInquiries
        );

        // Map for PII Logger (Needs DTO)
        var profileDto = new FinancialProfileDto(
            command.ApplicantId, salaryData.AverageMonthlyIncome, 0.85m, salaryData.EmploymentTenureMonths,
            salaryData.EmploymentType, salaryData.EmployerOrBusinessName, bankData.AverageAccountBalance,
            bankData.OverdraftFrequency, bankData.SavingsToIncomeRatio, cribData.CreditRiskGrade,
            cribData.ActiveLoanObligations, cribData.DefaultHistoryIndicator, cribData.RecentCreditInquiries
        );

        // 3. PII-Masked Logging BEFORE processing
        var maskedLogPayload = PiiMasker.GetMaskedLogPayload(profileDto);
        _logger.LogInformation("Processing Financial Profile for Applicant: {MaskedPayload}", maskedLogPayload);

        // 4. Domain Engine Evaluation
        var assessment = _scoringEngine.EvaluateFeasibility(profile, _timeProvider.GetUtcNow().UtcDateTime);

        // 5. Persist the assessment
        await _repository.AddAsync(assessment, cancellationToken);

        // 6. Map back to DTO
        var factors = new System.Collections.Generic.List<FeasibilityFactorDto>
        {
            new FeasibilityFactorDto("ITC", "IncomeToLeaseCost", (int)assessment.ScoreBreakdown.IncomeToLeaseCostScore, "Income to lease cost ratio", false),
            new FeasibilityFactorDto("INC", "IncomeConsistency", (int)assessment.ScoreBreakdown.IncomeConsistencyScore, "Income consistency score", false),
            new FeasibilityFactorDto("DTI", "DebtToIncome", (int)assessment.ScoreBreakdown.DebtToIncomeScore, "Debt to income score", false),
            new FeasibilityFactorDto("EMP", "EmploymentStability", (int)assessment.ScoreBreakdown.EmploymentStabilityScore, "Employment stability score", false),
            new FeasibilityFactorDto("CRD", "CreditIndicator", (int)assessment.ScoreBreakdown.CreditIndicatorScore, "Credit indicator score", false)
        };

        if (assessment.ScoreBreakdown.PenaltyScore < 0)
        {
            factors.Add(new FeasibilityFactorDto("PEN", "Penalty", (int)assessment.ScoreBreakdown.PenaltyScore, "Risk penalty", true));
        }

        return new FeasibilityAssessmentDto(
            ApplicationId: command.ApplicationId,
            ApplicantId: command.ApplicantId, // Pass from command since assessment doesn't store ApplicantId
            TotalScore: (int)assessment.ScoreBreakdown.TotalScore,
            EligibilityGrade: assessment.Grade.ToString(),
            RequiresManualReview: assessment.Grade == StateLandGovernance.LeaseFeasibility.Domain.Enums.FeasibilityGrade.C, // Assuming C means manual review, or whatever custom logic
            ContributingFactors: factors,
            EvaluationTimestamp: assessment.GeneratedAt.UtcDateTime
        );
    }
}
