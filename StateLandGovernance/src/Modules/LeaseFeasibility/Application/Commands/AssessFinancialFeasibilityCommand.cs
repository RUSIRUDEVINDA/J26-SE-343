using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StateLandGovernance.LeaseFeasibility.Application.DTOs;
using StateLandGovernance.LeaseFeasibility.Application.Interfaces;
using StateLandGovernance.LeaseFeasibility.Application.Utilities;
using StateLandGovernance.LeaseFeasibility.Domain.Enums;
using StateLandGovernance.LeaseFeasibility.Domain.Services;
using StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;

namespace StateLandGovernance.LeaseFeasibility.Application.Commands;

/// <summary>
/// Command to assess a lease application. Money values are LKR per month and
/// IncomeConsistencyRatio is a normalized 0-1 value supplied by an approved upstream calculation.
/// </summary>
public sealed record AssessFinancialFeasibilityCommand(
    string ApplicationId,
    string ApplicantId,
    decimal RequestedMonthlyLeasePaymentLkr,
    decimal MonthlyDebtObligationsLkr,
    decimal IncomeConsistencyRatio,
    string BankStatementUri,
    string SalarySlipUri,
    string CribReportUri);

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

    public async Task<FeasibilityAssessmentDto> HandleAsync(
        AssessFinancialFeasibilityCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = new Validators.AssessFinancialFeasibilityCommandValidator().Validate(command);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        var bankData = await _extractionService.ExtractBankStatementDataAsync(
            command.BankStatementUri,
            cancellationToken);
        var salaryData = await _extractionService.ExtractSalarySlipDataAsync(
            command.SalarySlipUri,
            cancellationToken);
        var cribData = await _extractionService.ExtractCribReportDataAsync(
            command.CribReportUri,
            cancellationToken);

        if (!Enum.TryParse<CreditRiskGrade>(cribData.CreditRiskGrade, true, out var creditRiskGrade) ||
            !Enum.IsDefined(creditRiskGrade))
        {
            throw new ValidationException(new[] { "CRIB credit risk grade must be A, B, C, D, or E." });
        }

        var input = new FinancialFeasibilityScoringInput(
            applicationId: command.ApplicationId,
            applicantId: command.ApplicantId,
            averageMonthlyIncomeLkr: salaryData.AverageMonthlyIncome,
            incomeConsistencyRatio: command.IncomeConsistencyRatio,
            requestedMonthlyLeasePaymentLkr: command.RequestedMonthlyLeasePaymentLkr,
            monthlyDebtObligationsLkr: command.MonthlyDebtObligationsLkr,
            averageAccountBalanceLkr: bankData.AverageAccountBalance,
            overdraftCountInEvidenceWindow: bankData.OverdraftCountInEvidenceWindow,
            creditRiskGrade: creditRiskGrade,
            hasDefaultHistory: cribData.DefaultHistoryIndicator);

        var maskedLogPayload = PiiMasker.GetMaskedLogPayload(input, salaryData.EmployerOrBusinessName);
        _logger.LogInformation(
            "Processing deterministic financial feasibility input: {MaskedPayload}",
            maskedLogPayload);

        var evaluationTimestamp = _timeProvider.GetUtcNow();
        var assessment = _scoringEngine.EvaluateFeasibility(input, evaluationTimestamp);

        await _repository.AddAsync(assessment, cancellationToken);

        var factors = new List<FeasibilityFactorDto>
        {
            new(
                "DSR",
                "DebtServiceRatio",
                assessment.ScoreBreakdown.DebtServiceRatioScore,
                $"Monthly debt plus requested lease payment is {assessment.ScoreBreakdown.DebtServiceRatio:P2} of monthly income.",
                false),
            new(
                "INC",
                "IncomeConsistency",
                assessment.ScoreBreakdown.IncomeConsistencyScore,
                $"Income consistency ratio is {command.IncomeConsistencyRatio:F2}.",
                false),
            new(
                "LIQ",
                "LiquidityBuffer",
                assessment.ScoreBreakdown.LiquidityBufferScore,
                $"Average account balance covers {assessment.ScoreBreakdown.LiquidityBufferMonths:F2} months of requested lease payments.",
                false),
            new(
                "CRD",
                "CreditHistory",
                assessment.ScoreBreakdown.CreditHistoryScore,
                $"Normalized CRIB credit grade is {creditRiskGrade}.",
                false)
        };

        if (assessment.ScoreBreakdown.PenaltyScore < 0m)
        {
            factors.Add(new FeasibilityFactorDto(
                "PEN",
                "Penalty",
                assessment.ScoreBreakdown.PenaltyScore,
                "Approved default-history and/or evidence-window overdraft penalties applied.",
                true));
        }

        return new FeasibilityAssessmentDto(
            ApplicationId: assessment.ApplicationId,
            ApplicantId: assessment.ApplicantId,
            ContractVersion: assessment.ContractVersion,
            TotalScore: assessment.ScoreBreakdown.TotalScore,
            EligibilityGrade: assessment.Grade.ToString(),
            RecommendedAction: assessment.Action.ToString(),
            RequiresManualReview: assessment.Action == FeasibilityAction.ManualReview,
            RequiresEscalation: assessment.Action == FeasibilityAction.Escalate,
            ContributingFactors: factors,
            EvaluationTimestamp: assessment.GeneratedAt);
    }
}
